using System.Data;
using Dapper;
using POS2SAP.API.Common;
using POS2SAP.API.DTOs.Sap;
using POS2SAP.API.Services.Interfaces;

namespace POS2SAP.API.Services.Implementations;

/// <summary>
/// Reads POS transaction data from HQ_FAMTIME using Dapper.
/// NOTE: POS source table names/columns must be verified against actual DB schema.
///       Adjust queries in this class once schema is confirmed.
/// </summary>
public class PosDataService : IPosDataService
{
    private readonly IDbConnection _db;

    public PosDataService(IDbConnection db)
    {
        _db = db;
    }

    /// <summary>
    /// AR head columns + full-tax join (orderfulltaxinvoicelink / ordertransactionfulltaxinvoice, FullTaxStatus=2).
    /// CardCode=SLOC, BranchCode=PTTShopCode, BranchName/CardName=ShopName, VatBranch=BranchNo.
    /// Tax fields still come from full-tax when present.
    /// </summary>
    private const string ArInvoiceHeadSelect = @"
                a.ReceiptNumber                                          AS PosDocNo,
                a.SaleDate                                               AS DocDate,
                ISNULL(NULLIF(s.PTTShopCode, ''), s.shopcode)            AS BranchCode,
                ISNULL(s.shopname, '')                                   AS BranchName,
                CAST(a.ComputerID AS NVARCHAR(20))                       AS PosId,
                ISNULL(s.SLOC, '')                                       AS CardCode,
                CASE WHEN ft.FullTaxInvoiceID IS NOT NULL
                     THEN ISNULL(ft.InvoiceName, '')
                     ELSE ISNULL(s.shopname, '') END                     AS CardName,
                CASE WHEN ft.FullTaxInvoiceID IS NOT NULL THEN ft.InvoiceTaxID ELSE NULL END
                                                                         AS CustTaxId,
                CASE WHEN ft.FullTaxInvoiceID IS NOT NULL THEN
                    NULLIF(LTRIM(RTRIM(CONCAT(
                        ISNULL(ft.InvoiceAddress1, N''),
                        CASE WHEN NULLIF(LTRIM(RTRIM(ISNULL(ft.InvoiceAddress2, N''))), N'') IS NOT NULL
                             THEN N' ' + ft.InvoiceAddress2 ELSE N'' END,
                        CASE WHEN NULLIF(LTRIM(RTRIM(ISNULL(ft.InvoiceCity, N''))), N'') IS NOT NULL
                             THEN N' ' + ft.InvoiceCity ELSE N'' END,
                        CASE WHEN NULLIF(LTRIM(RTRIM(ISNULL(ft.InvoiceZipCode, N''))), N'') IS NOT NULL
                             THEN N' ' + ft.InvoiceZipCode ELSE N'' END
                    ))), N'')
                ELSE NULL END                                            AS Address,
                CASE WHEN ft.FullTaxInvoiceID IS NOT NULL THEN ft.InvoiceCompanyBranchNo ELSE NULL END
                                                                         AS CustVatBranch,
                CASE WHEN ft.FullTaxInvoiceID IS NOT NULL THEN ft.InvoiceTelephone
                     ELSE ISNULL(NULLIF(m.MemberMobile, ''), m.MemberTelephone) END
                                                                         AS CustTel,
                CASE WHEN ISNULL(a.MemberID, 0) > 0
                     THEN ISNULL(NULLIF(LTRIM(RTRIM(m.MemberCode)), ''), CAST(a.MemberID AS NVARCHAR(20)))
                     ELSE '' END                                         AS CustMemberNo,
                ISNULL(s.BranchNo, '')                                   AS VatBranch,
                a.TransactionNote                                        AS Comments,
                ISNULL(sm.SaleModeName, CAST(a.SaleMode AS NVARCHAR(20))) AS Channel,
                mpoint.EarnPoint                                         AS CustBillPoint,
                mpoint.RedeemPoint                                       AS CustRedeemPoing,
                rps.TotalPoint                                           AS CustBalancePoint,
                -- VATable (ex-VAT) per receipt; DiscPrcnt still uses VAT-inc ReceiptRetailPrice
                ISNULL(a.TranBeforeVAT, 0)                               AS TotalAmtBefDis,
                ISNULL(a.ReceiptRetailPrice, 0)                          AS ReceiptRetailPrice,
                ISNULL(a.ReceiptDiscount, 0)                             AS ReceiptDiscount,
                ISNULL(a.ReceiptPayPrice, 0)                             AS DocTotal,
                ISNULL(dep.DownPaymentNo, '')                            AS DownPaymentNo,
                ISNULL(dep.DownPaymentAmt, 0)                            AS DownPaymentAmt,
                ISNULL(a.ServiceCharge, 0)                               AS ServiceCharge,
                ISNULL(a.ServiceChargeVAT, 0)                            AS ServiceChargeVat,
                ISNULL(a.SCBeforeVAT, 0)                                 AS ServiceChargeBeforeVat,
                a.TranKey";

    private const string ArInvoiceHeadJoins = @"
            FROM ordertransaction a
            LEFT JOIN shop_data s ON s.ShopID = a.ShopID
            LEFT JOIN salemode sm ON sm.SaleModeID = TRY_CAST(a.SaleMode AS INT)
            OUTER APPLY (
                SELECT TOP 1
                    l.FullTaxInvoiceID,
                    l.FullTaxInvoiceComputerID
                FROM orderfulltaxinvoicelink l
                WHERE l.TranKey = a.TranKey AND l.FullTaxStatus = 2
                ORDER BY l.UpdateDate DESC
            ) ftlink
            LEFT JOIN ordertransactionfulltaxinvoice ft
                ON ft.FullTaxInvoiceID = ftlink.FullTaxInvoiceID
                AND ft.FullTaxInvoiceComputerID = ftlink.FullTaxInvoiceComputerID
                AND ft.FullTaxStatus = 2
            LEFT JOIN members m
                ON m.MemberID = a.MemberID AND a.MemberID > 0 AND ISNULL(m.Deleted, 0) = 0
            OUTER APPLY (
                SELECT TOP 1 rp.TotalPoint
                FROM RewardPointSummary rp
                WHERE rp.MemberID = a.MemberID AND a.MemberID > 0
                ORDER BY rp.UpdateDate DESC
            ) rps
            OUTER APPLY (
                -- RewardPointType: 1=Earn 2=Redeem 3=Void Earn 4=Void Redeem 5=Redeem to eCoupon 6=Adjust
                -- TranPoint = points for this bill (TotalPointPrice is the bill amount, not points)
                SELECT
                    SUM(CASE WHEN ph.PointType = 1 THEN ph.TranPoint ELSE 0 END)            AS EarnPoint,
                    ABS(SUM(CASE WHEN ph.PointType IN (2, 5) THEN ph.TranPoint ELSE 0 END)) AS RedeemPoint
                FROM RewardPointHistory ph
                WHERE ph.TranKey = a.TranKey AND a.MemberID > 0
                  AND ph.MemberID = a.MemberID
            ) mpoint
            OUTER APPLY (
                SELECT
                    ISNULL(NULLIF(LTRIM(RTRIM(dp.ReceiptNumber)), ''), CAST(dp.TransactionID AS NVARCHAR(30))) AS DownPaymentNo,
                    ROUND(ISNULL((
                        SELECT SUM(ISNULL(p.PayAmount, 0))
                        FROM DownPayment_PayDetail p
                        WHERE p.TransactionID = dp.TransactionID AND p.ComputerID = dp.ComputerID
                    ), 0) / 1.07, 2) AS DownPaymentAmt
                FROM DownPayment_Transaction dp
                WHERE ISNULL(a.FromDepositTransactionID, 0) > 0
                  AND dp.TransactionID = a.FromDepositTransactionID
                  AND dp.ComputerID = a.FromDepositComputerID
            ) dep";

    /// <summary>POS orderdetail rows that belong on AR/Delivery lines (paid, real product, not void/comment).</summary>
    private const string BillableOrderDetailWhere = @"
              AND b.OrderStatusID = 2
              AND b.ProductID > 0
              AND ISNULL(b.IsComment, 0) = 0
              AND ISNULL(b.VoidStaffID, 0) = 0";

    // DiscPricePercent = item promo %; else amount disc when DiscPercent=0 (bill-share DiscPercent stays head-only).
    // HasLineDisc → Price before disc, PriceAfVat/GTotal/LineTotal after (WeightPrice). Else case-4: full GTotal, VatSum after.
    private const string ArInvoiceLineSelect = @"
                b.TranKey,
                ISNULL(b.OrderDetailID, 0)                                     AS OrderDetailID,
                LTRIM(RTRIM(ISNULL(c.ProductCode, CAST(b.ProductID AS NVARCHAR(50))))) AS ItemCode,
                COALESCE(
                    NULLIF(NULLIF(LTRIM(RTRIM(pgm.SapItemGroupCode)), ''), '[SAP-PENDING]'),
                    ISNULL(pg.ProductGroupCode, '')
                )                                                          AS ItemCategory,
                ''                                                         AS Dscription,
                ISNULL(c.ProductName, '')                                  AS Text,
                ISNULL(b.TotalQty, 0)                                      AS Quantity,
                ISNULL(c.ProductUnitName, '')                              AS UomCode,
                ISNULL(b.TotalRetailPrice, 0)                              AS TotalRetailPrice,
                ISNULL(b.WeightPrice, 0)                                   AS WeightPrice,
                CASE
                    WHEN ISNULL(b.DiscPricePercent, 0) > 0 THEN ISNULL(b.DiscPricePercent, 0)
                    WHEN ISNULL(b.DiscPercent, 0) = 0
                         AND ISNULL(b.TotalRetailPrice, 0) > 0
                         AND ISNULL(b.TotalItemDisc, 0) > 0
                         AND ISNULL(b.WeightPrice, 0) > 0
                    THEN ROUND(b.TotalItemDisc / b.TotalRetailPrice * 100, 2)
                    ELSE 0
                END                                                        AS DiscPrcnt,
                ROUND(
                    CASE WHEN ISNULL(b.TotalQty, 0) = 0 THEN 0
                         ELSE ISNULL(b.TotalRetailPrice, 0) / 1.07 / b.TotalQty
                    END, 2)                                                AS Price,
                ROUND(
                    CASE WHEN ISNULL(b.TotalQty, 0) = 0 THEN 0
                         WHEN (
                                ISNULL(b.DiscPricePercent, 0) > 0
                             OR (ISNULL(b.DiscPercent, 0) = 0
                                 AND ISNULL(b.TotalItemDisc, 0) > 0
                                 AND ISNULL(b.WeightPrice, 0) > 0)
                              )
                         THEN ISNULL(b.WeightPrice, 0) / b.TotalQty
                         ELSE ISNULL(b.TotalRetailPrice, 0) / b.TotalQty
                    END, 2)                                                AS PriceAfVat,
                ROUND(ISNULL(b.WeightPrice, 0) * 7.0 / 107.0, 2)           AS VatSum,
                ROUND(
                    CASE WHEN (
                                ISNULL(b.DiscPricePercent, 0) > 0
                             OR (ISNULL(b.DiscPercent, 0) = 0
                                 AND ISNULL(b.TotalItemDisc, 0) > 0
                                 AND ISNULL(b.WeightPrice, 0) > 0)
                              )
                         THEN ISNULL(b.WeightPrice, 0) / 1.07
                         ELSE ISNULL(b.TotalRetailPrice, 0) / 1.07
                    END, 2)                                                AS LineTotal,
                ROUND(
                    CASE WHEN (
                                ISNULL(b.DiscPricePercent, 0) > 0
                             OR (ISNULL(b.DiscPercent, 0) = 0
                                 AND ISNULL(b.TotalItemDisc, 0) > 0
                                 AND ISNULL(b.WeightPrice, 0) > 0)
                              )
                         THEN ISNULL(b.WeightPrice, 0)
                         ELSE ISNULL(b.TotalRetailPrice, 0)
                    END, 2)                                                AS GTotal,
                ISNULL(CAST(b.InventoryID AS NVARCHAR(20)), '')            AS WhsCode";

    private const string ArInvoiceLineFrom = @"
            FROM orderdetail b
            LEFT JOIN products c ON c.ProductID = b.ProductID
            LEFT JOIN productdept pd ON pd.ProductDeptID = c.ProductDeptID
            LEFT JOIN productgroup pg ON pg.ProductGroupID = pd.ProductGroupID AND ISNULL(pg.Deleted, 0) = 0
            LEFT JOIN productgroup_sap_mapping pgm
                ON pgm.ProductGroupID = pg.ProductGroupID AND pgm.IsActive = 1";

    public Task<List<SapArInvoiceHeadDto>> GetPendingBillsAsync(DateTime dateFrom, DateTime dateTo, int batchSize = 500)
        => GetBillsByFilterAsyncImpl(dateFrom, dateTo, null, batchSize);

    public Task<List<SapArInvoiceHeadDto>> GetBillsByDocNosAsync(IEnumerable<string> docNos)
        => GetBillsByDocNosAsyncImpl(docNos);

    public Task<List<SapArInvoiceHeadDto>> GetBillsByFilterAsync(DateTime dateFrom, DateTime dateTo, string? branchCode, int batchSize = 500)
        => GetBillsByFilterAsyncImpl(dateFrom, dateTo, branchCode, batchSize);

    private async Task<List<SapArInvoiceHeadDto>> GetBillsByFilterAsyncImpl(DateTime dateFrom, DateTime dateTo, string? branchCode, int batchSize = 500)
    {
        batchSize = Math.Clamp(batchSize, 1, 1000);
        var dateToExclusive = dateTo.Date.AddDays(1); // include the whole dateTo day

        // Optional branch filter — appended as a literal string (safe: branchCode is parameterised separately)
        var branchClause = string.IsNullOrWhiteSpace(branchCode)
            ? ""
            : "AND (s.shopcode = @BranchCode OR s.PTTShopCode = @BranchCode OR s.SLOC = @BranchCode)";

        var headSql = $@"
            SELECT TOP {batchSize}
                {ArInvoiceHeadSelect}
            {ArInvoiceHeadJoins}
            WHERE a.TransactionStatusID = 2
              AND ISNULL(a.Deleted, 0) = 0
              AND a.SaleDate >= @DateFrom
              AND a.SaleDate <  @DateToExclusive
              {branchClause}
            ORDER BY a.SaleDate, a.ReceiptNumber";

        var headCmd = new CommandDefinition(
            headSql,
            new { DateFrom = dateFrom, DateToExclusive = dateToExclusive, BranchCode = branchCode },
            commandTimeout: 300);

        var heads = (await _db.QueryAsync<dynamic>(headCmd)).ToList();
        if (!heads.Any()) return new();

        var tranKeys = heads.Select(h => (string)h.TranKey).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var linesByKey = await LoadArLinesByTranKeyAsync(tranKeys);
        var synthByKey = await LoadArSynthExtrasAsync(tranKeys);

        var results = new List<SapArInvoiceHeadDto>();
        foreach (var h in heads)
        {
            results.Add(BuildArInvoice(h, linesByKey, synthByKey));
        }
        return results;
    }

    private async Task<List<SapArInvoiceHeadDto>> GetBillsByDocNosAsyncImpl(IEnumerable<string> docNos)
    {
        var requested = docNos
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Select(d => d.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (requested.Count == 0) return new();

        var receiptNums = PosDocNumHelper.ExtractReceiptNumbers(requested);
        if (receiptNums.Count == 0) return new();

        var headSql = $@"
            SELECT
                {ArInvoiceHeadSelect}
            {ArInvoiceHeadJoins}
            WHERE a.ReceiptNumber IN @DocNos
              AND a.TransactionStatusID = 2
              AND ISNULL(a.Deleted, 0) = 0
            ORDER BY a.SaleDate, a.ReceiptNumber";

        var headCmd = new CommandDefinition(headSql, new { DocNos = receiptNums }, commandTimeout: 180);
        var heads = (await _db.QueryAsync<dynamic>(headCmd)).ToList();

        heads = heads
            .Where(h => requested.Any(r => PosDocNumHelper.Matches(r, Str(h.BranchCode), Str(h.PosDocNo))))
            .ToList();

        if (!heads.Any()) return new();

        var tranKeys = heads.Select(h => (string)h.TranKey).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var linesByKey = await LoadArLinesByTranKeyAsync(tranKeys);
        var synthByKey = await LoadArSynthExtrasAsync(tranKeys);

        var results = new List<SapArInvoiceHeadDto>();
        foreach (var h in heads)
        {
            results.Add(BuildArInvoice(h, linesByKey, synthByKey));
        }

        return results;
    }

    private async Task<Dictionary<string, List<dynamic>>> LoadArLinesByTranKeyAsync(List<string> tranKeys)
    {
        if (tranKeys.Count == 0)
            return new Dictionary<string, List<dynamic>>(StringComparer.OrdinalIgnoreCase);

        var tranKeyCsv = string.Join(",", tranKeys.Select(k => $"'{k}'"));
        var allLinesSql = $@"
            SELECT
                {ArInvoiceLineSelect}
            {ArInvoiceLineFrom}
            WHERE b.TranKey IN ({tranKeyCsv})
              {BillableOrderDetailWhere}
            ORDER BY b.TranKey, b.DisplayOrdering, b.OrderDetailID";

        var allLines = (await _db.QueryAsync<dynamic>(allLinesSql, commandTimeout: 180)).ToList();
        return allLines.GroupBy(l => (string)l.TranKey)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
    }

    private async Task<Dictionary<string, ArSynthExtras>> LoadArSynthExtrasAsync(List<string> tranKeys)
    {
        var result = new Dictionary<string, ArSynthExtras>(StringComparer.OrdinalIgnoreCase);
        if (tranKeys.Count == 0) return result;

        foreach (var k in tranKeys)
            result[k] = new ArSynthExtras();

        var tranKeyCsv = string.Join(",", tranKeys.Select(k => $"'{k}'"));

        // Gift voucher / eVoucher / coupon paytypes → negative AR lines
        var paySql = $@"
            SELECT
                opd.TranKey,
                opd.PayTypeID,
                ISNULL(opd.PayAmount, 0) AS PayAmount,
                ISNULL(NULLIF(LTRIM(RTRIM(opd.VoucherNo)), ''), '') AS VoucherNo
            FROM orderpaydetail opd
            WHERE opd.TranKey IN ({tranKeyCsv})
              AND ISNULL(opd.PayAmount, 0) > 0
              AND opd.PayTypeID IN (6, 99, 100, 152, 153, 154)";
        foreach (var row in await _db.QueryAsync<dynamic>(paySql, commandTimeout: 120))
        {
            var key = (string)row.TranKey;
            if (!result.TryGetValue(key, out var extras)) continue;
            var payTypeId = Convert.ToInt32(row.PayTypeID);
            var amount = Dec(row.PayAmount);
            var voucherNo = Str(row.VoucherNo);
            if (payTypeId == 6)
                extras.Coupons.Add((amount, voucherNo));
            else
                extras.GiftVouchers.Add((amount, voucherNo));
        }

        // vsmart coupon/discount codes (when present)
        var discSql = $@"
            SELECT
                d.TranKey,
                ISNULL(NULLIF(LTRIM(RTRIM(d.CODE)), ''), '') AS Code,
                ISNULL(d.Price_Used, ISNULL(d.Price, 0)) AS Amount
            FROM vsmart_orderdiscountdata d
            WHERE d.TranKey IN ({tranKeyCsv})
              AND ISNULL(d.Price_Used, ISNULL(d.Price, 0)) > 0";
        try
        {
            foreach (var row in await _db.QueryAsync<dynamic>(discSql, commandTimeout: 120))
            {
                var key = (string)row.TranKey;
                if (!result.TryGetValue(key, out var extras)) continue;
                extras.Coupons.Add((Dec(row.Amount), Str(row.Code)));
            }
        }
        catch
        {
            // ponytail: table may be missing on some HQ DBs — coupon still comes from paytype 6
        }

        // Promotions: LEFT JOIN so PromotionID=0 (bill-end disc) still loads with null name
        var promoSql = $@"
            SELECT
                opd.TranKey,
                ISNULL(opd.OrderDetailID, 0) AS OrderDetailID,
                ISNULL(opd.PromotionID, 0)   AS PromotionID,
                ISNULL(opd.DiscountPrice, 0) AS DiscountPrice,
                ISNULL(NULLIF(LTRIM(RTRIM(p.PromotionName)), ''), '') AS PromotionName
            FROM orderpromotiondetail opd
            LEFT JOIN promotion p
                ON p.PromotionID = opd.PromotionID
               AND ISNULL(p.Deleted, 0) = 0
            WHERE opd.TranKey IN ({tranKeyCsv})";
        try
        {
            foreach (var row in await _db.QueryAsync<dynamic>(promoSql, commandTimeout: 120))
            {
                var key = (string)row.TranKey;
                if (!result.TryGetValue(key, out var extras)) continue;
                extras.Promos.Add(new ArPromoRow(
                    IntZero(row.OrderDetailID),
                    IntZero(row.PromotionID),
                    Dec(row.DiscountPrice),
                    Str(row.PromotionName)));
            }
        }
        catch
        {
            // ponytail: older HQ DBs may lack orderpromotiondetail — freebie Text falls back to product name
        }

        return result;
    }

    internal static SapArInvoiceHeadDto BuildArInvoice(
        dynamic h,
        Dictionary<string, List<dynamic>> linesByKey,
        Dictionary<string, ArSynthExtras> synthByKey)
    {
        var tranKey = (string)h.TranKey;
        var lines = linesByKey.GetValueOrDefault(tranKey) ?? new List<dynamic>();
        var extras = synthByKey.GetValueOrDefault(tranKey) ?? new ArSynthExtras();

        // ponytail: POS stores uniform bill promos (e.g. KTC 13%) as DiscPricePercent on every line;
        // SAP expects head DiscPrcnt only with full-retail lines. Mixed line % stays line-level.
        TryNormalizeUniformBillPromo(lines, Dec(h.ReceiptDiscount), Dec(h.ReceiptRetailPrice));

        // Dec(dynamic) returns dynamic → force decimal so LINQ Sum binds the decimal overload
        // (dynamic selectors otherwise resolve to Sum<int> and throw decimal→int at runtime).
        decimal freeRetail = lines
            .Where(l => Dec(l.TotalRetailPrice) > 0 && Dec(l.WeightPrice) == 0)
            .Sum(l => (decimal)Dec(l.TotalRetailPrice));

        // Line-level item discounts must not also appear as head DiscPrcnt
        decimal lineItemDisc = lines
            .Where(l => Dec(l.DiscPrcnt) > 0)
            .Sum(l =>
            {
                decimal retailLine = Dec(l.TotalRetailPrice);
                decimal after = Dec(l.GTotal);
                // when HasLineDisc, GTotal is WeightPrice (after); discount amt ≈ retail - after
                return Math.Max(0m, retailLine - after);
            });

        decimal receiptDiscount = Dec(h.ReceiptDiscount);
        // DiscPrcnt basis stays VAT-inclusive (ReceiptRetailPrice); TotalAmtBefDis is ex-VAT VATable
        decimal retail = Dec(h.ReceiptRetailPrice);
        var billDiscAmt = Math.Max(0m, receiptDiscount - freeRetail - lineItemDisc);
        var headDiscPrcnt = retail > 0 && billDiscAmt > 0
            ? Math.Round(billDiscAmt / retail * 100m, 2, MidpointRounding.AwayFromZero)
            : 0m;

        var voucherTotal = extras.GiftVouchers.Sum(x => x.Amount) + extras.Coupons.Sum(x => x.Amount);
        var docTotal = Math.Max(0m, (decimal)Dec(h.DocTotal) - voucherTotal);

        var headDto = MapHead(h, headDiscPrcnt, docTotal);
        var docNum = headDto.DocNum;
        var whs = headDto.BranchCode;
        var dl = new List<SapArInvoiceLineDto>();
        foreach (var l in lines)
        {
            if (!IsBillableItemCode(Str(l.ItemCode))) continue;
            dl.Add(MapLine(l, docNum, whs, dl.Count));
        }

        AddServiceChargeLine(dl, h, docNum, whs);
        AddFreebieOrRedeemLines(dl, lines, headDto.CustRedeemPoing, docNum, whs, extras.Promos);
        AddNegativePayLines(dl, extras.Coupons, gbVar.SapArItemCoupon, gbVar.SapArCatCoupon, "ส่วนลดโปรโมชั่น", docNum, whs);
        AddNegativePayLines(dl, extras.GiftVouchers, gbVar.SapArItemGiftVoucher, gbVar.SapArCatGiftVoucher, "Gift voucher", docNum, whs);

        // Enrich Comments with voucher/coupon codes when head note empty or append codes
        var codes = extras.Coupons.Select(c => c.VoucherNo)
            .Concat(extras.GiftVouchers.Select(c => c.VoucherNo))
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (codes.Count > 0)
        {
            var note = headDto.Comments?.Trim() ?? string.Empty;
            var codeText = string.Join(" ", codes);
            headDto.Comments = string.IsNullOrEmpty(note) ? codeText : $"{note} {codeText}".Trim();
        }

        headDto.DocumentLines = dl;
        return headDto;
    }

    internal sealed class ArPromoRow
    {
        public ArPromoRow(int orderDetailId, int promotionId, decimal discountPrice, string promotionName)
        {
            OrderDetailId = orderDetailId;
            PromotionId = promotionId;
            DiscountPrice = discountPrice;
            PromotionName = promotionName ?? string.Empty;
        }

        public int OrderDetailId { get; }
        public int PromotionId { get; }
        public decimal DiscountPrice { get; }
        public string PromotionName { get; }
    }

    internal sealed class ArSynthExtras
    {
        public List<(decimal Amount, string VoucherNo)> Coupons { get; } = new();
        public List<(decimal Amount, string VoucherNo)> GiftVouchers { get; } = new();
        public List<ArPromoRow> Promos { get; } = new();
    }

    private const string IncomingPaymentHeadJoins = @"
            LEFT JOIN shop_data s  ON s.ShopID = a.ShopID
            LEFT JOIN salemode sm  ON sm.SaleModeID = TRY_CAST(a.SaleMode AS INT)";

    /// CardCode=SLOC, BranchCode=PTTShopCode, BranchName/CardName=ShopName, VatBranch=BranchNo.
    private const string IncomingPaymentHeadSelect = @"
                    a.ReceiptNumber                                              AS PosDocNo,
                    CONVERT(varchar(10), a.SaleDate, 23)                        AS DocDate,
                    CONVERT(varchar(10), a.SaleDate, 23)                        AS SettlementDate,
                    CASE WHEN a.PaidTime IS NULL THEN '' ELSE CONVERT(varchar(8), a.PaidTime, 108) END AS SettlementTime,
                    ISNULL(s.SLOC, '')                                          AS CardCode,
                    ISNULL(s.shopname, '')                                      AS CardName,
                    CAST(a.ComputerID AS NVARCHAR(20))                          AS PosId,
                    ISNULL(NULLIF(s.PTTShopCode, ''), s.shopcode)               AS BranchCode,
                    ISNULL(s.shopname, '')                                      AS BranchName,
                    ISNULL(s.BranchNo, '')                                      AS VatBranch,
                    ISNULL(sm.SaleModeName, CAST(a.SaleMode AS NVARCHAR(20)))   AS Channel,
                    ISNULL(a.TransactionNote, '')                               AS Comments,
                    ISNULL(a.ReceiptPayPrice, 0)                                AS DocTotal,
                    a.TranKey";

    public Task<List<SapIncomingPaymentDto>> GetPendingPaymentsAsync(DateTime dateFrom, DateTime dateTo, int batchSize = 500)
        => GetPaymentsAsyncImpl(null, dateFrom, dateTo, batchSize, requireArSuccess: true);

    public Task<List<SapIncomingPaymentDto>> GetPaymentsByDocNosAsync(IEnumerable<string> docNos)
        => GetPaymentsAsyncImpl(docNos.ToList(), null, null, 1000, requireArSuccess: false);

    public Task<List<SapIncomingPaymentDto>> GetPaymentsByFilterAsync(DateTime dateFrom, DateTime dateTo, string? branchCode, int batchSize = 500)
        => GetPaymentsAsyncImpl(null, dateFrom, dateTo, batchSize, requireArSuccess: false, branchCode: branchCode);

    /// <summary>
    /// Core payment query:
    ///   requireArSuccess=true  → only receipts that have AR SUCCESS in interface_logs
    ///                            and no AP PENDING/PROCESSING/SUCCESS yet (used by scheduler)
    ///   requireArSuccess=false → fetch by docNos or date range regardless of AR status
    ///                            (used for manual preview / retry)
    /// </summary>
    private async Task<List<SapIncomingPaymentDto>> GetPaymentsAsyncImpl(
        List<string>? docNos,
        DateTime? dateFrom,
        DateTime? dateTo,
        int batchSize,
        bool requireArSuccess,
        string? branchCode = null)
    {
        batchSize = Math.Clamp(batchSize, 1, 1000);

        // ---------------------------------------------------------------- Head SQL
        string headSql;
        object headParam;
        List<string>? requestedDocNos = null;

        if (docNos is { Count: > 0 })
        {
            requestedDocNos = docNos
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Select(d => d.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var receiptNums = PosDocNumHelper.ExtractReceiptNumbers(requestedDocNos);
            if (receiptNums.Count == 0) return new();

            headSql = $@"
                SELECT
                    {IncomingPaymentHeadSelect}
                FROM ordertransaction a
                {IncomingPaymentHeadJoins}
                WHERE a.ReceiptNumber IN @DocNos
                  AND a.TransactionStatusID = 2
                  AND ISNULL(a.Deleted, 0) = 0
                ORDER BY a.SaleDate, a.ReceiptNumber";
            headParam = new { DocNos = receiptNums };
        }
        else
        {
            var df = dateFrom?.Date ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var dtExcl = (dateTo?.Date ?? df).AddDays(1);

            var arSuccessClause = requireArSuccess ? @"
                  AND EXISTS (
                    SELECT 1 FROM interface_logs il
                    WHERE il.interface_type = 'AR'
                      AND il.status = 'SUCCESS'
                      AND il.is_deleted = 0
                      AND (
                        il.pos_doc_no = (ISNULL(NULLIF(s.PTTShopCode, ''), s.shopcode) + N'|' + a.ReceiptNumber)
                        OR (
                          il.pos_doc_no = a.ReceiptNumber
                          AND (
                            il.branch_code = ISNULL(NULLIF(s.PTTShopCode, ''), s.shopcode)
                            OR il.branch_code = ISNULL(s.SLOC, '')
                            OR ISNULL(il.branch_code, '') = ''
                          )
                        )
                      )
                  )" : "";

            var branchClause = string.IsNullOrWhiteSpace(branchCode)
                ? ""
                : "AND (s.shopcode = @BranchCode OR s.PTTShopCode = @BranchCode OR s.SLOC = @BranchCode)";

            headSql = $@"
                SELECT TOP {batchSize}
                    {IncomingPaymentHeadSelect}
                FROM ordertransaction a
                {IncomingPaymentHeadJoins}
                WHERE a.TransactionStatusID = 2
                  AND ISNULL(a.Deleted, 0) = 0
                  AND a.SaleDate >= @DateFrom
                  AND a.SaleDate <  @DateToExcl
                  {branchClause}
                  {arSuccessClause}
                ORDER BY a.SaleDate, a.ReceiptNumber";
            headParam = new { DateFrom = df, DateToExcl = dtExcl, BranchCode = branchCode };
        }

        var headCmd = new CommandDefinition(headSql, headParam, commandTimeout: 120);
        var heads = (await _db.QueryAsync<dynamic>(headCmd)).ToList();
        if (requestedDocNos is { Count: > 0 })
        {
            heads = heads
                .Where(h => requestedDocNos.Any(r => PosDocNumHelper.Matches(r, Str(h.BranchCode), Str(h.PosDocNo))))
                .ToList();
        }
        if (!heads.Any()) return new();

        // ---------------------------------------------------------------- Payment detail SQL
        var tranKeyCsv = string.Join(",", heads.Select(h => $"'{(string)h.TranKey}'").Distinct());

        var payDetailSql = $@"
            SELECT
                opd.TranKey,
                opd.PayTypeID,
                ISNULL(p.PayTypeName, '')                                    AS PayTypeName,
                UPPER(LTRIM(RTRIM(ISNULL(glm.SapPayCategory, 'SKIP'))))          AS SapPayCategory,
                ISNULL(glm.SapGlAccount, '')                                AS SapGlAccount,
                ISNULL(glm.SapPayTypeName, ISNULL(p.PayTypeName, ''))       AS SapPayTypeName,
                opd.PayAmount,
                ISNULL(opd.CreditCardNo, '')                                AS CreditCardNo,
                ISNULL(opd.CCApproveCode, '')                               AS CCApproveCode,
                ISNULL(opd.ExpireMonth, 0)                                  AS ExpireMonth,
                ISNULL(opd.ExpireYear,  0)                                  AS ExpireYear,
                ISNULL(opd.PayRemark, '')                                   AS PayRemark,
                ISNULL(opd.VoucherNo, '')                                   AS VoucherNo
            FROM orderpaydetail opd
            JOIN paytype p ON p.PayTypeID = opd.PayTypeID AND ISNULL(p.Deleted, 0) = 0
            LEFT JOIN paytype_gl_mapping glm
                ON glm.PayTypeID = opd.PayTypeID AND glm.IsActive = 1
            WHERE opd.TranKey IN ({tranKeyCsv})
            ORDER BY opd.TranKey, opd.PayDetailID";

        var allPays = (await _db.QueryAsync<dynamic>(
            new CommandDefinition(payDetailSql, commandTimeout: 120))).ToList();

        var paysByKey = allPays
            .GroupBy(p => (string)p.TranKey)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var arInvoiceDocByKey = await LoadArInvoiceDocNumsAsync(heads);

        // ---------------------------------------------------------------- Assemble DTOs
        var results = new List<SapIncomingPaymentDto>();
        foreach (var h in heads)
        {
            var key = PosDocNumHelper.Build(Str(h.BranchCode), Str(h.PosDocNo));
            arInvoiceDocByKey.TryGetValue(key, out string? arInvoiceNum);
            if (arInvoiceNum is null)
                arInvoiceDocByKey.TryGetValue(Str(h.PosDocNo), out arInvoiceNum);

            var pays = paysByKey.GetValueOrDefault((string)h.TranKey) ?? new List<dynamic>();
            results.Add(BuildIncomingPaymentDto(h, pays, arInvoiceNum));
        }

        return results;
    }

    private async Task<Dictionary<string, string>> LoadArInvoiceDocNumsAsync(List<dynamic> heads)
    {
        if (heads.Count == 0)
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var compoundKeys = heads
            .Select(h => (string)PosDocNumHelper.Build(Str(h.BranchCode), Str(h.PosDocNo)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var receiptNums = heads
            .Select(h => (string)Str(h.PosDocNo))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        const string sql = @"
            SELECT il.pos_doc_no AS PosDocNo, il.branch_code AS BranchCode,
                   il.sap_request AS SapRequest, il.sent_at AS SentAt
            FROM interface_logs il
            WHERE il.interface_type = 'AR'
              AND il.status = 'SUCCESS'
              AND il.is_deleted = 0
              AND (il.pos_doc_no IN @CompoundKeys OR il.pos_doc_no IN @ReceiptNums)
            ORDER BY il.sent_at DESC";

        var rows = (await _db.QueryAsync<dynamic>(
            new CommandDefinition(sql, new { CompoundKeys = compoundKeys, ReceiptNums = receiptNums }, commandTimeout: 120))).ToList();

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var posDocNo = Str(row.PosDocNo);
            var arDocNum = SapIncomingPaymentJsonHelper.TryExtractArInvoiceDocNum(Str(row.SapRequest));
            if (string.IsNullOrWhiteSpace(arDocNum)) continue;

            if (!map.ContainsKey(posDocNo))
                map[posDocNo] = arDocNum;

            if (PosDocNumHelper.TryParse(posDocNo, out string parsedBranch, out string parsedReceipt))
            {
                var compound = PosDocNumHelper.Build(parsedBranch, parsedReceipt);
                if (!map.ContainsKey(compound))
                    map[compound] = arDocNum;
            }
            else
            {
                var legBranch = Str(row.BranchCode);
                if (!string.IsNullOrEmpty(legBranch))
                {
                    var compound = PosDocNumHelper.Build(legBranch, posDocNo);
                    if (!map.ContainsKey(compound))
                        map[compound] = arDocNum;
                }
            }
        }

        return map;
    }

    private static SapIncomingPaymentDto BuildIncomingPaymentDto(
        dynamic h,
        List<dynamic> pays,
        string? arInvoiceDocNum)
    {
        var receipt = Str(h.PosDocNo);
        var branch  = Str(h.BranchCode);
        var docNum  = PosDocNumHelper.Build(branch, receipt);
        var docDate = Str(h.DocDate);

        // Sum by SapPayCategory from paytype_gl_mapping (GL Mapping UI config)
        var activePays = pays.Where(p => Dec(p.PayAmount) > 0 && !IsSkipPayRow(p)).ToList();

        var cashRows  = activePays.Where(p => PayCategory(p) == gbVar.SapPayCategoryCash).ToList();
        var trsfrRows = activePays.Where(p => PayCategory(p) == gbVar.SapPayCategoryTransfer).ToList();
        var ccRows    = activePays.Where(p => PayCategory(p) == gbVar.SapPayCategoryCreditCard).ToList();

        var cashSum  = SumPayAmount(cashRows);
        var trsfrSum = SumPayAmount(trsfrRows);

        var trsfrRef = trsfrSum > 0
            ? trsfrRows
                .Select(p => !string.IsNullOrEmpty((string)p.CCApproveCode)
                    ? (string)p.CCApproveCode
                    : (string)p.PayRemark)
                .FirstOrDefault(r => !string.IsNullOrEmpty(r)) ?? string.Empty
            : string.Empty;

        var invoiceNum = !string.IsNullOrWhiteSpace(arInvoiceDocNum) ? arInvoiceDocNum! : docNum;

        var dto = new SapIncomingPaymentDto
        {
            DocNum     = docNum,
            DocDate    = docDate,
            SettlementDate = Str(h.SettlementDate),
            SettlementTime = Str(h.SettlementTime),
            DocType    = "C",
            CardCode   = Str(h.CardCode),
            CardName   = Str(h.CardName),
            POSID      = Str(h.PosId),

            CashAcct   = cashSum > 0 ? PickGlAccount(cashRows) : string.Empty,
            CashSum    = cashSum,

            TrsfrAcct  = trsfrSum > 0 ? PickGlAccount(trsfrRows) : string.Empty,
            TrsfrSum   = trsfrSum,
            TrsfrDate  = trsfrSum > 0 ? docDate : string.Empty,
            TrsfrRef   = trsfrRef,

            PayNoDoc   = "N",
            NoDocSum   = 0m,
            DocCur     = gbVar.SapDocCur,
            BranchCode = Str(h.BranchCode),
            BranchName = Str(h.BranchName),
            VatBranch  = Str(h.VatBranch),
            Channel    = Str(h.Channel),
            Comments   = Str(h.Comments),

            PaymentInvoices = new List<SapPaymentInvoiceLineDto>
            {
                new()
                {
                    DocNum     = docNum,
                    LineNum    = 0,
                    InvType    = 13,
                    InvoiceNum = invoiceNum,
                    Dcount     = 0,
                    SumApplied = Dec(h.DocTotal)
                }
            },

            paymentCreditCards = ccRows
                .Select((p, i) => new SapPaymentCreditCardDto
                {
                    DocNum         = docNum,
                    LineNum        = i,
                    CreditCard     = Str(p.SapPayTypeName),
                    CreditAcct     = string.Empty,
                    CrCardNum      = Str(p.CreditCardNo),
                    CardValid      = FormatCardExpiry(Convert.ToInt32(p.ExpireMonth), Convert.ToInt32(p.ExpireYear)),
                    CreditCardBank = "BAY",
                    CreditSum      = Dec(p.PayAmount),
                    VoucherNum     = !string.IsNullOrEmpty((string)p.CCApproveCode)
                        ? (string)p.CCApproveCode
                        : (string)p.VoucherNo
                }).ToList()
        };

        return SapIncomingPaymentJsonHelper.Normalize(dto);
    }

    private static string PayCategory(dynamic row) =>
        NormalizePayCategory(Str(row.SapPayCategory));

    private static string NormalizePayCategory(string? category) =>
        string.IsNullOrWhiteSpace(category)
            ? gbVar.SapPayCategorySkip
            : category.Trim().ToUpperInvariant();

    private static bool IsSkipPayRow(dynamic row) =>
        PayCategory(row) == gbVar.SapPayCategorySkip;

    private static decimal SumPayAmount(IEnumerable<dynamic> rows) =>
        rows.Aggregate(0m, (sum, p) => sum + Dec(p.PayAmount));

    /// <summary>GL account from paytype_gl_mapping — prefer row with largest PayAmount in category.</summary>
    private static string PickGlAccount(IEnumerable<dynamic> rows) =>
        rows
            .Where(p => !string.IsNullOrWhiteSpace(Str(p.SapGlAccount)))
            .OrderByDescending(p => Dec(p.PayAmount))
            .Select(p => Str(p.SapGlAccount))
            .FirstOrDefault() ?? string.Empty;

    private static string FormatCardExpiry(int month, int year)
    {
        if (month == 0 || year == 0) return "";
        var fullYear = year < 100 ? 2000 + year : year;
        return $"{fullYear:D4}-{month:D2}-01";
    }

    // ------------------------------------------------------------------ Delivery
    // Same POS sales source as AR (ordertransaction / orderdetail); map into SapDeliveryDto schema.

    public async Task<List<SapDeliveryDto>> GetDeliveriesByDocNosAsync(IEnumerable<string> docNos)
        => MapArListToDeliveries(await GetBillsByDocNosAsync(docNos));

    public async Task<List<SapDeliveryDto>> GetDeliveriesByFilterAsync(
        DateTime dateFrom, DateTime dateTo, string? branchCode, int batchSize = 500)
        => MapArListToDeliveries(await GetBillsByFilterAsync(dateFrom, dateTo, branchCode, batchSize));

    public async Task<List<SapDeliveryDto>> GetPendingDeliveriesAsync(
        DateTime dateFrom, DateTime dateTo, int batchSize = 500)
        => MapArListToDeliveries(await GetPendingBillsAsync(dateFrom, dateTo, batchSize));

    private static List<SapDeliveryDto> MapArListToDeliveries(List<SapArInvoiceHeadDto> bills)
        => bills.Select(MapArToDelivery).ToList();

    private static SapDeliveryDto MapArToDelivery(SapArInvoiceHeadDto bill)
    {
        var lines = (bill.DocumentLines ?? new List<SapArInvoiceLineDto>())
            .Where(l => IsBillableItemCode(l.ItemCode))
            .ToList();
        var dto = new SapDeliveryDto
        {
            DocNum              = bill.DocNum ?? string.Empty,
            DocDate             = bill.DocDate ?? string.Empty,
            POSID               = bill.POSID ?? string.Empty,
            CardCode            = bill.CardCode ?? string.Empty,
            CardName            = bill.CardName ?? string.Empty,
            BranchCode          = bill.BranchCode ?? string.Empty,
            BranchName          = bill.BranchName ?? string.Empty,
            Channel             = bill.Channel ?? string.Empty,
            VatBranch           = bill.VatBranch ?? string.Empty,
            DeliveryReason      = "เบิกเพื่อขาย",
            DeliveryReasonOther = string.Empty,
            Comments            = bill.Comments ?? string.Empty,
            DocumentLines = lines.Select((line, i) =>
            {
                var uom = line.UomCode ?? string.Empty;
                return new SapDeliveryLineDto
                {
                    DocNum     = bill.DocNum ?? string.Empty,
                    LineNum    = i,
                    ItemCode   = (line.ItemCode ?? string.Empty).Trim(),
                    Dscription = line.Text ?? string.Empty,
                    FreeTxt    = string.Empty,
                    Quantity   = SapDeliveryJsonHelper.FormatQuantity(line.Quantity),
                    UomCode    = uom,
                    unitMsr    = uom,
                    WhsCode    = line.WhsCode ?? string.Empty
                };
            }).ToList()
        };
        return SapDeliveryJsonHelper.Normalize(dto);
    }

    // ------------------------------------------------------------------ Mappers

    internal static string Str(object? val) => val?.ToString() ?? string.Empty;
    /// <summary>
    /// Uniform bill promo: every discounted line shares the same DiscPrcnt and sums to ReceiptDiscount.
    /// SAP wants head DiscPrcnt only; reset lines to full retail before head/line split.
    /// </summary>
    internal static bool TryNormalizeUniformBillPromo(
        List<dynamic> lines,
        decimal receiptDiscount,
        decimal receiptRetail)
    {
        if (receiptDiscount <= 0 || receiptRetail <= 0)
            return false;

        var billable = lines.Where(l => IsBillableItemCode(Str(l.ItemCode))).ToList();
        var billableWithRetail = billable.Where(l => Dec(l.TotalRetailPrice) > 0).ToList();
        if (billableWithRetail.Count < 2)
            return false;

        var promoPrcnt = Dec(billableWithRetail[0].DiscPrcnt);
        if (promoPrcnt <= 0 || billableWithRetail.Any(l => Dec(l.DiscPrcnt) != promoPrcnt))
            return false;

        var itemDiscTotal = billable.Sum(l =>
            (decimal)Math.Max(0m, Dec(l.TotalRetailPrice) - Dec(l.GTotal)));
        if (Math.Abs(itemDiscTotal - receiptDiscount) > 0.05m)
            return false;

        var headPrcnt = Math.Round(receiptDiscount / receiptRetail * 100m, 2, MidpointRounding.AwayFromZero);
        if (Math.Abs(promoPrcnt - headPrcnt) > 0.05m)
            return false;

        foreach (var l in billable)
            ResetLineToFullRetail(l);

        return true;
    }

    internal static void ResetLineToFullRetail(dynamic l)
    {
        var qty = Dec(l.Quantity);
        var retail = Dec(l.TotalRetailPrice);
        l.DiscPrcnt = 0m;
        l.GTotal = retail;
        l.LineTotal = Math.Round(retail / 1.07m, 2, MidpointRounding.AwayFromZero);
        l.PriceAfVat = qty == 0 ? 0m : Math.Round(retail / qty, 2, MidpointRounding.AwayFromZero);
        l.Price = qty == 0 ? 0m : Math.Round(retail / 1.07m / qty, 2, MidpointRounding.AwayFromZero);
        l.VatSum = Math.Round(retail * 7m / 107m, 2, MidpointRounding.AwayFromZero);
    }

    internal static decimal Dec(object? val) => val == null || val is DBNull ? 0m : Convert.ToDecimal(val);
    private static decimal? DecNull(object? val) => val == null || val is DBNull ? null : Convert.ToDecimal(val);

    internal static bool IsBillableItemCode(string? itemCode)
    {
        var code = (itemCode ?? string.Empty).Trim();
        return code.Length > 0 && code != "0";
    }

    private static int? IntNull(object? val) => val == null || val is DBNull ? null : Convert.ToInt32(Convert.ToDecimal(val));
    internal static int IntZero(object? val) => val == null || val is DBNull ? 0 : Convert.ToInt32(Convert.ToDecimal(val));

    internal static SapArInvoiceHeadDto MapHead(dynamic h, decimal? discPrcntOverride = null, decimal? docTotalOverride = null)
    {
        string docDate;
        if (h.DocDate is DateTime dt)
            docDate = dt.ToString("yyyy-MM-dd");
        else if (DateTime.TryParse(h.DocDate?.ToString() as string, out DateTime parsed))
            docDate = parsed.ToString("yyyy-MM-dd");
        else
            docDate = h.DocDate?.ToString() ?? string.Empty;

        return new SapArInvoiceHeadDto
        {
            DocNum           = PosDocNumHelper.Build(Str(h.BranchCode), Str(h.PosDocNo)),
            DocDate          = docDate,
            DocDueDate       = docDate,
            PymntGroup       = "Cash",
            POSID            = Str(h.PosId),
            CardCode         = Str(h.CardCode),
            CardName         = Str(h.CardName),
            CustTaxId        = Str(h.CustTaxId),
            Address          = Str(h.Address),
            CustVatBranch    = Str(h.CustVatBranch),
            CustTel          = Str(h.CustTel),
            CustMemberNo     = Str(h.CustMemberNo),
            DocCur           = "THB",
            BranchCode       = Str(h.BranchCode),
            BranchName       = Str(h.BranchName),
            VatBranch        = Str(h.VatBranch),
            Comments         = Str(h.Comments),
            Channel          = Str(h.Channel),
            CustBillPoint    = IntZero(h.CustBillPoint),
            CustRedeemPoing  = IntZero(h.CustRedeemPoing),
            CustBalancePoint = IntZero(h.CustBalancePoint),
            TotalAmtBefDis   = Dec(h.TotalAmtBefDis),
            DiscPrcnt        = discPrcntOverride ?? Dec(h.DiscPrcnt),
            DownPaymentNo    = Str(h.DownPaymentNo),
            DownPaymentAmt   = Dec(h.DownPaymentAmt),
            DocTotal         = docTotalOverride ?? Dec(h.DocTotal),
            DocumentLines    = new()
        };
    }

    internal static SapArInvoiceLineDto MapLine(dynamic l, string docNum, string whsCode, int idx)
    {
        return new SapArInvoiceLineDto
        {
            DocNum       = docNum,
            LineNum      = idx,
            ItemCode     = Str(l.ItemCode).Trim(),
            ItemCategory = Str(l.ItemCategory),
            Dscription   = string.Empty,
            Text         = Str(l.Text),
            Quantity     = Dec(l.Quantity),
            UomCode      = Str(l.UomCode),
            DiscPrcnt    = Dec(l.DiscPrcnt),
            Price        = Dec(l.Price),
            PriceAfVat   = Dec(l.PriceAfVat),
            VatPrcnt     = gbVar.SapVatPrcnt,
            VatGroup     = gbVar.SapVatGroup,
            VatSum       = Dec(l.VatSum),
            LineTotal    = Dec(l.LineTotal),
            GTotal       = Dec(l.GTotal),
            WhsCode      = whsCode,
            CouponNo     = new List<object>()
        };
    }

    internal static void AddServiceChargeLine(
        List<SapArInvoiceLineDto> lines,
        dynamic head,
        string docNum,
        string whsCode)
    {
        var total = Dec(head.ServiceCharge);
        if (total <= 0) return;

        lines.Add(new SapArInvoiceLineDto
        {
            DocNum       = docNum,
            LineNum      = lines.Count,
            ItemCode     = gbVar.SapArItemServiceCharge,
            ItemCategory = gbVar.SapArCatServiceCharge,
            Dscription   = string.Empty,
            Text         = "Service Charge",
            Quantity     = 1,
            UomCode      = string.Empty,
            DiscPrcnt    = 0,
            Price        = Dec(head.ServiceChargeBeforeVat),
            PriceAfVat   = total,
            VatPrcnt     = gbVar.SapVatPrcnt,
            VatGroup     = gbVar.SapVatGroup,
            VatSum       = Dec(head.ServiceChargeVat),
            LineTotal    = Dec(head.ServiceChargeBeforeVat),
            GTotal       = total,
            WhsCode      = whsCode,
            CouponNo     = new List<object>()
        });
    }

    /// <summary>
    /// Free items (WeightPrice=0, TotalRetailPrice&gt;0) → negative OP line (or RV-RD when bill redeemed points).
    /// Product line stays at full retail; this line offsets the free value.
    /// PromotionID=0 on orderpromotiondetail = bill-end discount (not used for OP Text).
    /// </summary>
    internal static void AddFreebieOrRedeemLines(
        List<SapArInvoiceLineDto> lines,
        List<dynamic> rawLines,
        int redeemPoints,
        string docNum,
        string whsCode,
        IReadOnlyList<ArPromoRow>? promos = null)
    {
        var freeLines = rawLines
            .Where(l => Dec(l.TotalRetailPrice) > 0 && Dec(l.WeightPrice) == 0 && IsBillableItemCode(Str(l.ItemCode)))
            .ToList();
        if (freeLines.Count == 0) return;

        var useRedeem = redeemPoints > 0;
        var itemCode = useRedeem ? gbVar.SapArItemRedeem : gbVar.SapArItemFreebie;
        var itemCat = useRedeem ? gbVar.SapArCatRedeem : gbVar.SapArCatFreebie;
        var promoList = promos ?? Array.Empty<ArPromoRow>();

        foreach (var fl in freeLines)
        {
            var gross = Dec(fl.TotalRetailPrice);
            var exVat = Math.Round(gross / 1.07m, 2, MidpointRounding.AwayFromZero);
            var vat = Math.Round(gross * 7m / 107m, 2, MidpointRounding.AwayFromZero);
            var text = useRedeem
                ? $"แลก {redeemPoints} คะแนน"
                : ResolveFreebiePromoText(fl, promoList);

            lines.Add(MakeNegativeLine(lines.Count, docNum, whsCode, itemCode, itemCat, text, exVat, gross, vat));
        }
    }

    /// <summary>
    /// Name for OP freebie Text: PromotionID&gt;0 only. Match OrderDetailID, else bill-level (0) by DiscountPrice.
    /// </summary>
    internal static string ResolveFreebiePromoText(dynamic fl, IReadOnlyList<ArPromoRow> promos)
    {
        var named = promos
            .Where(p => p.PromotionId > 0 && !string.IsNullOrWhiteSpace(p.PromotionName))
            .ToList();

        var orderDetailId = IntZero(fl.OrderDetailID);
        var byDetail = named.FirstOrDefault(p => p.OrderDetailId == orderDetailId);
        if (byDetail != null)
            return byDetail.PromotionName;

        var gross = Dec(fl.TotalRetailPrice);
        var byAmt = named.FirstOrDefault(p =>
            p.OrderDetailId == 0 && Math.Abs(p.DiscountPrice - gross) <= 0.05m);
        if (byAmt != null)
            return byAmt.PromotionName;

        return string.IsNullOrWhiteSpace(Str(fl.Text)) ? "ของแถมพร้อมการขาย" : Str(fl.Text);
    }

    internal static void AddNegativePayLines(
        List<SapArInvoiceLineDto> lines,
        List<(decimal Amount, string VoucherNo)> pays,
        string itemCode,
        string itemCategory,
        string defaultText,
        string docNum,
        string whsCode)
    {
        foreach (var pay in pays)
        {
            if (pay.Amount <= 0) continue;
            var gross = pay.Amount;
            var exVat = Math.Round(gross / 1.07m, 2, MidpointRounding.AwayFromZero);
            var vat = Math.Round(gross * 7m / 107m, 2, MidpointRounding.AwayFromZero);
            var text = string.IsNullOrWhiteSpace(pay.VoucherNo)
                ? defaultText
                : $"{defaultText} {pay.VoucherNo}".Trim();
            lines.Add(MakeNegativeLine(lines.Count, docNum, whsCode, itemCode, itemCategory, text, exVat, gross, vat));
        }
    }

    internal static SapArInvoiceLineDto MakeNegativeLine(
        int lineNum,
        string docNum,
        string whsCode,
        string itemCode,
        string itemCategory,
        string text,
        decimal exVat,
        decimal grossVatInc,
        decimal vat)
    {
        return new SapArInvoiceLineDto
        {
            DocNum       = docNum,
            LineNum      = lineNum,
            ItemCode     = itemCode,
            ItemCategory = itemCategory,
            Dscription   = string.Empty,
            Text         = text,
            Quantity     = -1,
            UomCode      = string.Empty,
            DiscPrcnt    = 0,
            Price        = exVat,
            PriceAfVat   = grossVatInc,
            VatPrcnt     = gbVar.SapVatPrcnt,
            VatGroup     = gbVar.SapVatGroup,
            VatSum       = -vat,
            LineTotal    = -exVat,
            GTotal       = -grossVatInc,
            WhsCode      = whsCode,
            CouponNo     = new List<object>()
        };
    }
}
