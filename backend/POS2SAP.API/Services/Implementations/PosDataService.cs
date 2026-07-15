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
                CASE WHEN ft.FullTaxInvoiceID IS NOT NULL THEN ft.InvoiceTelephone ELSE NULL END
                                                                         AS CustTel,
                a.MemberID                                               AS CustMemberNo,
                ISNULL(s.BranchNo, '')                                   AS VatBranch,
                a.TransactionNote                                        AS Comments,
                ISNULL(sm.SaleModeName, CAST(a.SaleMode AS NVARCHAR(20))) AS Channel,
                NULL                                                     AS CustBillPoint,
                NULL                                                     AS CustRedeemPoing,
                NULL                                                     AS CustBalancePoint,
                ISNULL(a.ReceiptRetailPrice, 0)                          AS TotalAmtBefDis,
                0                                                        AS DiscPrcnt,
                NULL                                                     AS DownPaymentNo,
                NULL                                                     AS DownPaymentAmt,
                ISNULL(a.ReceiptPayPrice, 0)                             AS DocTotal,
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
                AND ft.FullTaxStatus = 2";

    /// <summary>POS orderdetail rows that belong on AR/Delivery lines (paid, real product, not void/comment).</summary>
    private const string BillableOrderDetailWhere = @"
              AND b.OrderStatusID = 2
              AND b.ProductID > 0
              AND ISNULL(b.IsComment, 0) = 0
              AND ISNULL(b.VoidStaffID, 0) = 0";

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

        var tranKeyCsv = string.Join(",", heads.Select(h => $"'{h.TranKey}'").Distinct());
        var allLinesSql = $@"
            SELECT
                b.TranKey,
                LTRIM(RTRIM(ISNULL(c.ProductCode, CAST(b.ProductID AS NVARCHAR(50))))) AS ItemCode,
                COALESCE(
                    NULLIF(NULLIF(LTRIM(RTRIM(pgm.SapItemGroupCode)), ''), '[SAP-PENDING]'),
                    ISNULL(pg.ProductGroupCode, '')
                )                                                          AS ItemCategory,
                ''                                                         AS Dscription,
                ISNULL(c.ProductName, '')                                  AS Text,
                ISNULL(b.TotalQty, 0)                                      AS Quantity,
                ISNULL(c.ProductUnitName, '')                              AS UomCode,
                ISNULL(b.DiscPricePercent, 0)                             AS DiscPrcnt,
                ROUND(
                    CASE WHEN ISNULL(b.TotalQty, 0) = 0 THEN 0
                         ELSE (ISNULL(b.WeightPrice, 0) - ISNULL(b.WeightPriceVAT, 0)) / b.TotalQty
                    END, 2)                                                AS Price,
                ROUND(
                    CASE WHEN ISNULL(b.TotalQty, 0) = 0 THEN 0
                         ELSE ISNULL(b.WeightPrice, 0) / b.TotalQty
                    END, 2)                                                AS PriceAfVat,
                ROUND(ISNULL(b.WeightPriceVAT, 0), 2)                      AS VatSum,
                ROUND(ISNULL(b.WeightPrice, 0) - ISNULL(b.WeightPriceVAT, 0), 2) AS LineTotal,
                ROUND(ISNULL(b.WeightPrice, 0), 2)                         AS GTotal,
                ISNULL(CAST(b.InventoryID AS NVARCHAR(20)), '')            AS WhsCode
            FROM orderdetail b
            LEFT JOIN products c ON c.ProductID = b.ProductID
            LEFT JOIN productdept pd ON pd.ProductDeptID = c.ProductDeptID
            LEFT JOIN productgroup pg ON pg.ProductGroupID = pd.ProductGroupID AND ISNULL(pg.Deleted, 0) = 0
            LEFT JOIN productgroup_sap_mapping pgm
                ON pgm.ProductGroupID = pg.ProductGroupID AND pgm.IsActive = 1
            WHERE b.TranKey IN ({tranKeyCsv})
              {BillableOrderDetailWhere}
            ORDER BY b.TranKey, b.DisplayOrdering, b.OrderDetailID";

        var allLines = (await _db.QueryAsync<dynamic>(allLinesSql, commandTimeout: 120)).ToList();
        var linesByKey = allLines.GroupBy(l => (string)l.TranKey)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var results = new List<SapArInvoiceHeadDto>();
        foreach (var h in heads)
        {
            SapArInvoiceHeadDto headDto = MapHead(h);
            var lines = linesByKey.GetValueOrDefault((string)h.TranKey) ?? new List<dynamic>();
            var docNum1 = headDto.DocNum;
            var whs1 = headDto.BranchCode; // PTTShopCode
            var dl1 = new List<SapArInvoiceLineDto>(); int li1 = 0;
            foreach (var l in lines)
            {
                if (!IsBillableItemCode(Str(l.ItemCode))) continue;
                dl1.Add(MapLine(l, docNum1, whs1, li1++));
            }
            headDto.DocumentLines = dl1;
            results.Add(headDto);
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

        // Batch fetch all lines using the same approach as GetPendingBillsAsync
        var tranKeyCsv = string.Join(",", heads.Select(h => $"'{h.TranKey}'").Distinct());

        var allLinesSql = $@"
            SELECT
                b.TranKey,
                LTRIM(RTRIM(ISNULL(c.ProductCode, CAST(b.ProductID AS NVARCHAR(50))))) AS ItemCode,
                COALESCE(
                    NULLIF(NULLIF(LTRIM(RTRIM(pgm.SapItemGroupCode)), ''), '[SAP-PENDING]'),
                    ISNULL(pg.ProductGroupCode, '')
                )                                                          AS ItemCategory,
                ''                                                         AS Dscription,
                ISNULL(c.ProductName, '')                                  AS Text,
                ISNULL(b.TotalQty, 0)                                      AS Quantity,
                ISNULL(c.ProductUnitName, '')                              AS UomCode,
                ISNULL(b.DiscPricePercent, 0)                             AS DiscPrcnt,
                ROUND(
                    CASE WHEN ISNULL(b.TotalQty, 0) = 0 THEN 0
                         ELSE (ISNULL(b.WeightPrice, 0) - ISNULL(b.WeightPriceVAT, 0)) / b.TotalQty
                    END, 2)                                                AS Price,
                ROUND(
                    CASE WHEN ISNULL(b.TotalQty, 0) = 0 THEN 0
                         ELSE ISNULL(b.WeightPrice, 0) / b.TotalQty
                    END, 2)                                                AS PriceAfVat,
                ROUND(ISNULL(b.WeightPriceVAT, 0), 2)                      AS VatSum,
                ROUND(ISNULL(b.WeightPrice, 0) - ISNULL(b.WeightPriceVAT, 0), 2) AS LineTotal,
                ROUND(ISNULL(b.WeightPrice, 0), 2)                         AS GTotal,
                ISNULL(CAST(b.InventoryID AS NVARCHAR(20)), '')            AS WhsCode
            FROM orderdetail b
            LEFT JOIN products c ON c.ProductID = b.ProductID
            LEFT JOIN productdept pd ON pd.ProductDeptID = c.ProductDeptID
            LEFT JOIN productgroup pg ON pg.ProductGroupID = pd.ProductGroupID AND ISNULL(pg.Deleted, 0) = 0
            LEFT JOIN productgroup_sap_mapping pgm
                ON pgm.ProductGroupID = pg.ProductGroupID AND pgm.IsActive = 1
            WHERE b.TranKey IN ({tranKeyCsv})
              {BillableOrderDetailWhere}
            ORDER BY b.TranKey, b.DisplayOrdering, b.OrderDetailID";

        var allLines = (await _db.QueryAsync<dynamic>(allLinesSql, commandTimeout: 180)).ToList();

        // Group lines by TranKey
        var linesByKey = allLines.GroupBy(l => (string)l.TranKey)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var results = new List<SapArInvoiceHeadDto>();
        foreach (var h in heads)
        {
            SapArInvoiceHeadDto headDto = MapHead(h);
            var lines = linesByKey.GetValueOrDefault((string)h.TranKey) ?? new List<dynamic>();
            var docNum2 = headDto.DocNum;
            var whs2 = headDto.BranchCode; // PTTShopCode
            var dl2 = new List<SapArInvoiceLineDto>(); int li2 = 0;
            foreach (var l in lines)
            {
                if (!IsBillableItemCode(Str(l.ItemCode))) continue;
                dl2.Add(MapLine(l, docNum2, whs2, li2++));
            }
            headDto.DocumentLines = dl2;
            results.Add(headDto);
        }

        return results;
    }

    private const string IncomingPaymentHeadJoins = @"
            LEFT JOIN shop_data s  ON s.ShopID = a.ShopID
            LEFT JOIN salemode sm  ON sm.SaleModeID = TRY_CAST(a.SaleMode AS INT)";

    /// CardCode=SLOC, BranchCode=PTTShopCode, BranchName/CardName=ShopName.
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
                    CreditAcct     = Str(p.SapGlAccount),
                    CrCardNum      = Str(p.CreditCardNo),
                    CardValid      = FormatCardExpiry(Convert.ToInt32(p.ExpireMonth), Convert.ToInt32(p.ExpireYear)),
                    CreditCardBank = string.Empty,
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

    private static string Str(object? val) => val?.ToString() ?? string.Empty;
    private static decimal Dec(object? val) => val == null || val is DBNull ? 0m : Convert.ToDecimal(val);
    private static decimal? DecNull(object? val) => val == null || val is DBNull ? null : Convert.ToDecimal(val);

    private static bool IsBillableItemCode(string? itemCode)
    {
        var code = (itemCode ?? string.Empty).Trim();
        return code.Length > 0 && code != "0";
    }

    private static int? IntNull(object? val) => val == null || val is DBNull ? null : Convert.ToInt32(Convert.ToDecimal(val));
    private static int IntZero(object? val) => val == null || val is DBNull ? 0 : Convert.ToInt32(Convert.ToDecimal(val));

    private static SapArInvoiceHeadDto MapHead(dynamic h)
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
            DiscPrcnt        = Dec(h.DiscPrcnt),
            DownPaymentNo    = Str(h.DownPaymentNo),
            DownPaymentAmt   = Dec(h.DownPaymentAmt),
            DocTotal         = Dec(h.DocTotal),
            DocumentLines    = new()
        };
    }

    private static SapArInvoiceLineDto MapLine(dynamic l, string docNum, string whsCode, int idx)
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
            VatPrcnt     = 7m,
            VatGroup     = "S07",
            VatSum       = Dec(l.VatSum),
            LineTotal    = Dec(l.LineTotal),
            GTotal       = Dec(l.GTotal),
            WhsCode      = whsCode,
            CouponNo     = new List<object>()
        };
    }
}
