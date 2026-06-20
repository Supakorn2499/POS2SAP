using System.Data;
using Dapper;
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

    public Task<List<SapArInvoiceHeadDto>> GetPendingBillsAsync(int batchSize = 500)
        => GetPendingBillsAsyncImpl(batchSize);

    private async Task<List<SapArInvoiceHeadDto>> GetPendingBillsAsyncImpl(int batchSize = 500)
    {
        // Head: ordertransaction — TransactionStatusID=2 (ชำระแล้ว)
        // NOTE: ตรวจสอบ ProductCode/ProductName ในตาราง products หากชื่อ column ต่างกัน
        var today    = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var monthEnd   = monthStart.AddMonths(1); // exclusive upper bound

        var headSql = $@"
            SELECT TOP {batchSize}
                a.ReceiptNumber                                          AS PosDocNo,
                a.SaleDate                                               AS DocDate,
                ISNULL(NULLIF(s.PTTShopCode, ''), s.shopcode)            AS BranchCode,
                ISNULL(s.BranchName, '')                                 AS BranchName,
                CAST(a.ComputerID AS NVARCHAR(20))                       AS PosId,
                ISNULL(s.SLOC, '')                                       AS CardCode,
                ISNULL(a.MemberName, '')                                 AS CardName,
                NULL                                                     AS CustTaxId,
                NULL                                                     AS Address,
                NULL                                                     AS CustVatBranch,
                NULL                                                     AS CustTel,
                a.MemberID                                               AS CustMemberNo,
                ''                                                       AS VatBranch,
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
                a.TranKey
            FROM ordertransaction a
            LEFT JOIN shop_data s ON s.ShopID = a.ShopID
            LEFT JOIN salemode sm ON sm.SaleModeID = TRY_CAST(a.SaleMode AS INT)
            WHERE a.TransactionStatusID = 2
              AND ISNULL(a.Deleted, 0) = 0
              AND a.SaleDate >= @MonthStart
              AND a.SaleDate <  @MonthEnd
            ORDER BY a.SaleDate, a.ReceiptNumber";

        var heads = (await _db.QueryAsync<dynamic>(headSql, new { MonthStart = monthStart, MonthEnd = monthEnd },
            commandTimeout: 120)).ToList();

        if (!heads.Any()) return new();

        // Batch fetch all lines in one query — join by TranKey
        var tranKeyCsv = string.Join(",", heads.Select(h => $"'{h.TranKey}'").Distinct());

        var allLinesSql = $@"
            SELECT
                b.TranKey,
                ISNULL(c.ProductCode, CAST(b.ProductID AS NVARCHAR(50)))  AS ItemCode,
                ISNULL(pg.ProductGroupCode, '')                            AS ItemCategory,
                ISNULL(c.ProductName, '')                                  AS Dscription,
                ISNULL(b.Comment, '')                                      AS FreeTxt,
                ISNULL(b.TotalQty, 0)                                      AS Quantity,
                ISNULL(c.ProductUnitName, '')                              AS UomCode,
                ISNULL(b.DiscPricePercent, 0)                             AS DiscPrcnt,
                ISNULL(ISNULL(b.ProductBeforeVAT, 0) / NULLIF(ISNULL(b.TotalQty, 0), 0), 0) AS Price,
                ISNULL((ISNULL(b.ProductBeforeVAT, 0) + ISNULL(b.ProductVAT, 0)) / NULLIF(ISNULL(b.TotalQty, 0), 0), 0) AS PriceAfVat,
                ISNULL(b.ProductVAT, 0)                                    AS VatSum,
                ISNULL(b.ProductBeforeVAT, 0)                              AS LineTotal,
                ISNULL(b.ProductBeforeVAT, 0) + ISNULL(b.ProductVAT, 0)   AS GTotal,
                ISNULL(CAST(b.InventoryID AS NVARCHAR(20)), '')            AS WhsCode
            FROM orderdetail b
            LEFT JOIN products c ON c.ProductID = b.ProductID
            LEFT JOIN productdept pd ON pd.ProductDeptID = c.ProductDeptID
            LEFT JOIN productgroup pg ON pg.ProductGroupID = pd.ProductGroupID
            WHERE b.TranKey IN ({tranKeyCsv})
            ORDER BY b.TranKey, b.DisplayOrdering, b.OrderDetailID";

        var allLines = (await _db.QueryAsync<dynamic>(allLinesSql, commandTimeout: 120)).ToList();

        // Group lines by TranKey
        var linesByKey = allLines.GroupBy(l => (string)l.TranKey)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var results = new List<SapArInvoiceHeadDto>();
        foreach (var h in heads)
        {
            SapArInvoiceHeadDto headDto = MapHead(h);
            var lines = linesByKey.GetValueOrDefault((string)h.TranKey) ?? new List<dynamic>();
            var docNum0 = (string)Str(h.PosDocNo);
            var dl0 = new List<SapArInvoiceLineDto>(); int li0 = 0;
            foreach (var l in lines) dl0.Add(MapLine(l, docNum0, li0++));
            headDto.DocumentLines = dl0;
            results.Add(headDto);
        }

        return results;
    }

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
            : "AND (s.shopcode = @BranchCode OR s.PTTShopCode = @BranchCode)";

        var headSql = $@"
            SELECT TOP {batchSize}
                a.ReceiptNumber                                          AS PosDocNo,
                a.SaleDate                                               AS DocDate,
                ISNULL(NULLIF(s.PTTShopCode, ''), s.shopcode)            AS BranchCode,
                ISNULL(s.BranchName, '')                                 AS BranchName,
                CAST(a.ComputerID AS NVARCHAR(20))                       AS PosId,
                ISNULL(s.SLOC, '')                                       AS CardCode,
                ISNULL(a.MemberName, '')                                 AS CardName,
                NULL                                                     AS CustTaxId,
                NULL                                                     AS Address,
                NULL                                                     AS CustVatBranch,
                NULL                                                     AS CustTel,
                a.MemberID                                               AS CustMemberNo,
                ''                                                       AS VatBranch,
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
                a.TranKey
            FROM ordertransaction a
            LEFT JOIN shop_data s ON s.ShopID = a.ShopID
            LEFT JOIN salemode sm ON sm.SaleModeID = TRY_CAST(a.SaleMode AS INT)
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
                ISNULL(c.ProductCode, CAST(b.ProductID AS NVARCHAR(50)))  AS ItemCode,
                ISNULL(pg.ProductGroupCode, '')                            AS ItemCategory,
                ISNULL(c.ProductName, '')                                  AS Dscription,
                ISNULL(b.Comment, '')                                      AS FreeTxt,
                ISNULL(b.TotalQty, 0)                                      AS Quantity,
                ISNULL(c.ProductUnitName, '')                              AS UomCode,
                ISNULL(b.DiscPricePercent, 0)                             AS DiscPrcnt,
                ISNULL(ISNULL(b.ProductBeforeVAT, 0) / NULLIF(ISNULL(b.TotalQty, 0), 0), 0) AS Price,
                ISNULL((ISNULL(b.ProductBeforeVAT, 0) + ISNULL(b.ProductVAT, 0)) / NULLIF(ISNULL(b.TotalQty, 0), 0), 0) AS PriceAfVat,
                ISNULL(b.ProductVAT, 0)                                    AS VatSum,
                ISNULL(b.ProductBeforeVAT, 0)                              AS LineTotal,
                ISNULL(b.ProductBeforeVAT, 0) + ISNULL(b.ProductVAT, 0)   AS GTotal,
                ISNULL(CAST(b.InventoryID AS NVARCHAR(20)), '')            AS WhsCode
            FROM orderdetail b
            LEFT JOIN products c ON c.ProductID = b.ProductID
            LEFT JOIN productdept pd ON pd.ProductDeptID = c.ProductDeptID
            LEFT JOIN productgroup pg ON pg.ProductGroupID = pd.ProductGroupID
            WHERE b.TranKey IN ({tranKeyCsv})
            ORDER BY b.TranKey, b.DisplayOrdering, b.OrderDetailID";

        var allLines = (await _db.QueryAsync<dynamic>(allLinesSql, commandTimeout: 120)).ToList();
        var linesByKey = allLines.GroupBy(l => (string)l.TranKey)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var results = new List<SapArInvoiceHeadDto>();
        foreach (var h in heads)
        {
            SapArInvoiceHeadDto headDto = MapHead(h);
            var lines = linesByKey.GetValueOrDefault((string)h.TranKey) ?? new List<dynamic>();
            var docNum1 = (string)Str(h.PosDocNo);
            var dl1 = new List<SapArInvoiceLineDto>(); int li1 = 0;
            foreach (var l in lines) dl1.Add(MapLine(l, docNum1, li1++));
            headDto.DocumentLines = dl1;
            results.Add(headDto);
        }
        return results;
    }

    private async Task<List<SapArInvoiceHeadDto>> GetBillsByDocNosAsyncImpl(IEnumerable<string> docNos)
    {
        var docNoList = docNos.ToList();
        if (!docNoList.Any()) return new();

        var headSql = @"
            SELECT
                a.ReceiptNumber                                          AS PosDocNo,
                a.SaleDate                                               AS DocDate,
                ISNULL(NULLIF(s.PTTShopCode, ''), s.shopcode)            AS BranchCode,
                ISNULL(s.BranchName, '')                                 AS BranchName,
                CAST(a.ComputerID AS NVARCHAR(20))                       AS PosId,
                ISNULL(s.SLOC, '')                                       AS CardCode,
                ISNULL(a.MemberName, '')                                 AS CardName,
                NULL                                                     AS CustTaxId,
                NULL                                                     AS Address,
                NULL                                                     AS CustVatBranch,
                NULL                                                     AS CustTel,
                a.MemberID                                             AS CustMemberNo,
                ''                                                       AS VatBranch,
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
                a.TranKey
            FROM ordertransaction a
            LEFT JOIN shop_data s ON s.ShopID = a.ShopID
            LEFT JOIN salemode sm ON sm.SaleModeID = TRY_CAST(a.SaleMode AS INT)
            WHERE a.ReceiptNumber IN @DocNos
              AND a.TransactionStatusID = 2
              AND ISNULL(a.Deleted, 0) = 0
            ORDER BY a.SaleDate, a.ReceiptNumber";

        var headCmd = new CommandDefinition(headSql, new { DocNos = docNoList }, commandTimeout: 180);
        var heads = (await _db.QueryAsync<dynamic>(headCmd)).ToList();

        if (!heads.Any()) return new();

        // Batch fetch all lines using the same approach as GetPendingBillsAsync
        var tranKeyCsv = string.Join(",", heads.Select(h => $"'{h.TranKey}'").Distinct());

        var allLinesSql = $@"
            SELECT
                b.TranKey,
                ISNULL(c.ProductCode, CAST(b.ProductID AS NVARCHAR(50)))  AS ItemCode,
                ISNULL(pg.ProductGroupCode, '')                            AS ItemCategory,
                ISNULL(c.ProductName, '')                                  AS Dscription,
                ISNULL(b.Comment, '')                                      AS FreeTxt,
                ISNULL(b.TotalQty, 0)                                      AS Quantity,
                ISNULL(c.ProductUnitName, '')                              AS UomCode,
                ISNULL(b.DiscPricePercent, 0)                             AS DiscPrcnt,
                ISNULL(ISNULL(b.ProductBeforeVAT, 0) / NULLIF(ISNULL(b.TotalQty, 0), 0), 0) AS Price,
                ISNULL((ISNULL(b.ProductBeforeVAT, 0) + ISNULL(b.ProductVAT, 0)) / NULLIF(ISNULL(b.TotalQty, 0), 0), 0) AS PriceAfVat,
                ISNULL(b.ProductVAT, 0)                                    AS VatSum,
                ISNULL(b.ProductBeforeVAT, 0)                              AS LineTotal,
                ISNULL(b.ProductBeforeVAT, 0) + ISNULL(b.ProductVAT, 0)   AS GTotal,
                ISNULL(CAST(b.InventoryID AS NVARCHAR(20)), '')            AS WhsCode
            FROM orderdetail b
            LEFT JOIN products c ON c.ProductID = b.ProductID
            LEFT JOIN productdept pd ON pd.ProductDeptID = c.ProductDeptID
            LEFT JOIN productgroup pg ON pg.ProductGroupID = pd.ProductGroupID
            WHERE b.TranKey IN ({tranKeyCsv})
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
            var docNum2 = (string)Str(h.PosDocNo);
            var dl2 = new List<SapArInvoiceLineDto>(); int li2 = 0;
            foreach (var l in lines) dl2.Add(MapLine(l, docNum2, li2++));
            headDto.DocumentLines = dl2;
            results.Add(headDto);
        }

        return results;
    }

    // ------------------------------------------------------------------ Incoming Payment queries

    public Task<List<SapIncomingPaymentDto>> GetPendingPaymentsAsync(int batchSize = 500)
        => GetPaymentsAsyncImpl(null, null, null, batchSize, requireArSuccess: true);

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

        if (docNos is { Count: > 0 })
        {
            headSql = @"
                SELECT
                    a.ReceiptNumber                                              AS PosDocNo,
                    CONVERT(varchar(10), a.SaleDate, 23)                        AS DocDate,
                    ISNULL(s.SLOC, '')                                          AS CardCode,
                    ISNULL(a.MemberName, '')                                    AS CardName,
                    CAST(a.ComputerID AS NVARCHAR(20))                          AS PosId,
                    ISNULL(NULLIF(s.PTTShopCode, ''), s.shopcode)               AS BranchCode,
                    ISNULL(s.BranchName, '')                                    AS BranchName,
                    ISNULL(sm.SaleModeName, CAST(a.SaleMode AS NVARCHAR(20)))   AS Channel,
                    ISNULL(a.TransactionNote, '')                               AS Comments,
                    ISNULL(a.ReceiptPayPrice, 0)                                AS DocTotal,
                    ISNULL(il.sap_doc_num, '')                                  AS ArSapDocNum,
                    a.TranKey
                FROM ordertransaction a
                LEFT JOIN shop_data s  ON s.ShopID = a.ShopID
                LEFT JOIN salemode sm  ON sm.SaleModeID = TRY_CAST(a.SaleMode AS INT)
                LEFT JOIN interface_logs il
                    ON il.pos_doc_no = a.ReceiptNumber
                    AND il.interface_type = 'AR'
                    AND il.status = 'SUCCESS'
                    AND il.is_deleted = 0
                WHERE a.ReceiptNumber IN @DocNos
                  AND a.TransactionStatusID = 2
                  AND ISNULL(a.Deleted, 0) = 0
                ORDER BY a.SaleDate, a.ReceiptNumber";
            headParam = new { DocNos = docNos };
        }
        else
        {
            var df = dateFrom?.Date ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var dtExcl = (dateTo?.Date ?? df).AddDays(1);

            // requireArSuccess=true: only receipts with SUCCESS AR invoice
            // (AP duplicate-send prevention is handled in RunIncomingPaymentBatchAsync — not in SQL)
            var arSuccessClause = requireArSuccess ? @"
                  AND EXISTS (
                    SELECT 1 FROM interface_logs il
                    WHERE il.pos_doc_no = a.ReceiptNumber
                      AND il.interface_type = 'AR'
                      AND il.status = 'SUCCESS'
                      AND il.is_deleted = 0
                  )" : "";

            var branchClause = string.IsNullOrWhiteSpace(branchCode)
                ? ""
                : "AND (s.shopcode = @BranchCode OR s.PTTShopCode = @BranchCode)";

            headSql = $@"
                SELECT TOP {batchSize}
                    a.ReceiptNumber                                              AS PosDocNo,
                    CONVERT(varchar(10), a.SaleDate, 23)                        AS DocDate,
                    ISNULL(s.SLOC, '')                                          AS CardCode,
                    ISNULL(a.MemberName, '')                                    AS CardName,
                    CAST(a.ComputerID AS NVARCHAR(20))                          AS PosId,
                    ISNULL(NULLIF(s.PTTShopCode, ''), s.shopcode)               AS BranchCode,
                    ISNULL(s.BranchName, '')                                    AS BranchName,
                    ISNULL(sm.SaleModeName, CAST(a.SaleMode AS NVARCHAR(20)))   AS Channel,
                    ISNULL(a.TransactionNote, '')                               AS Comments,
                    ISNULL(a.ReceiptPayPrice, 0)                                AS DocTotal,
                    ISNULL(il.sap_doc_num, '')                                  AS ArSapDocNum,
                    a.TranKey
                FROM ordertransaction a
                LEFT JOIN shop_data s  ON s.ShopID = a.ShopID
                LEFT JOIN salemode sm  ON sm.SaleModeID = TRY_CAST(a.SaleMode AS INT)
                LEFT JOIN interface_logs il
                    ON il.pos_doc_no = a.ReceiptNumber
                    AND il.interface_type = 'AR'
                    AND il.status = 'SUCCESS'
                    AND il.is_deleted = 0
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
        if (!heads.Any()) return new();

        // ---------------------------------------------------------------- Payment detail SQL
        var tranKeyCsv = string.Join(",", heads.Select(h => $"'{(string)h.TranKey}'").Distinct());

        var payDetailSql = $@"
            SELECT
                opd.TranKey,
                opd.PayTypeID,
                ISNULL(p.PayTypeName, '')                                    AS PayTypeName,
                ISNULL(glm.SapPayCategory, 'SKIP')                          AS SapPayCategory,
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
            JOIN paytype p ON p.PayTypeID = opd.PayTypeID
            LEFT JOIN paytype_gl_mapping glm
                ON glm.PayTypeID = opd.PayTypeID AND glm.IsActive = 1
            WHERE opd.TranKey IN ({tranKeyCsv})
            ORDER BY opd.TranKey, opd.PayDetailID";

        var allPays = (await _db.QueryAsync<dynamic>(
            new CommandDefinition(payDetailSql, commandTimeout: 120))).ToList();

        var paysByKey = allPays
            .GroupBy(p => (string)p.TranKey)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        // ---------------------------------------------------------------- Assemble DTOs
        var results = new List<SapIncomingPaymentDto>();
        foreach (var h in heads)
        {
            var docNum  = Str(h.PosDocNo);
            var docDate = Str(h.DocDate);
            var pays    = paysByKey.GetValueOrDefault((string)h.TranKey) ?? new List<dynamic>();

            var cashRows  = pays.Where(p => (string)p.SapPayCategory == "CASH").ToList();
            var trsfrRows = pays.Where(p => (string)p.SapPayCategory == "TRANSFER").ToList();
            var ccRows    = pays.Where(p => (string)p.SapPayCategory == "CREDIT_CARD").ToList();

            // TRANSFER reference = first non-empty CCApproveCode or PayRemark
            var trsfrRef = trsfrRows
                .Select(p => !string.IsNullOrEmpty((string)p.CCApproveCode)
                    ? (string)p.CCApproveCode
                    : (string)p.PayRemark)
                .FirstOrDefault(r => !string.IsNullOrEmpty(r)) ?? "";

            var dto = new SapIncomingPaymentDto
            {
                DocNum     = docNum,
                DocDate    = docDate,
                DocType    = "C",
                CardCode   = Str(h.CardCode),
                CardName   = Str(h.CardName),
                POSID      = Str(h.PosId),

                CashAcct   = cashRows.Select(p => (string)p.SapGlAccount).FirstOrDefault() ?? "",
                CashSum    = cashRows.Aggregate(0m, (sum, p) => sum + Dec(p.PayAmount)),

                TrsfrAcct  = trsfrRows.Select(p => (string)p.SapGlAccount).FirstOrDefault() ?? "",
                TrsfrSum   = trsfrRows.Aggregate(0m, (sum, p) => sum + Dec(p.PayAmount)),
                TrsfrDate  = docDate,
                TrsfrRef   = trsfrRef,

                PayNoDoc   = "N",
                NoDocSum   = 0m,
                DocCur     = "THB",
                BranchCode = Str(h.BranchCode),
                BranchName = Str(h.BranchName),
                Channel    = Str(h.Channel),
                Comments   = Str(h.Comments),

                PaymentInvoices = new List<SapPaymentInvoiceLineDto>
                {
                    new()
                    {
                        DocNum      = docNum,
                        LineNum     = 0,
                        InvType     = 13,
                        InvoiceNum  = Str(h.ArSapDocNum),
                        Dcount      = 0,
                        SumApplied  = Dec(h.DocTotal)
                    }
                },

                paymentCreditCards = ccRows.Select((p, i) => new SapPaymentCreditCardDto
                {
                    DocNum         = docNum,
                    LineNum        = i,
                    CreditCard     = Str(p.SapPayTypeName),
                    CreditAcct     = Str(p.SapGlAccount),
                    CrCardNum      = Str(p.CreditCardNo),
                    CardValid      = FormatCardExpiry(Convert.ToInt32(p.ExpireMonth), Convert.ToInt32(p.ExpireYear)),
                    CreditCardBank = "",
                    CreditSum      = Dec(p.PayAmount),
                    VoucherNum     = !string.IsNullOrEmpty((string)p.CCApproveCode)
                                        ? (string)p.CCApproveCode
                                        : (string)p.VoucherNo
                }).ToList()
            };

            results.Add(dto);
        }

        return results;
    }

    private static string FormatCardExpiry(int month, int year)
    {
        if (month == 0 || year == 0) return "";
        var fullYear = year < 100 ? 2000 + year : year;
        return $"{fullYear:D4}-{month:D2}-01";
    }

    // ------------------------------------------------------------------ Mappers

    private static string Str(object? val) => val?.ToString() ?? string.Empty;
    private static decimal Dec(object? val) => val == null || val is DBNull ? 0m : Convert.ToDecimal(val);
    private static decimal? DecNull(object? val) => val == null || val is DBNull ? null : Convert.ToDecimal(val);

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
            DocNum           = Str(h.PosDocNo),
            DocDate          = docDate,
            DocDueDate       = docDate,
            PymntGroup       = "Cash",
            POSID            = Str(h.PosId),
            CardCode         = Str(h.CardCode),
            CardName         = string.Empty,
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

    private static SapArInvoiceLineDto MapLine(dynamic l, string docNum, int idx)
    {
        return new SapArInvoiceLineDto
        {
            DocNum       = docNum,
            LineNum      = idx,
            ItemCode     = Str(l.ItemCode),
            ItemCategory = Str(l.ItemCategory),
            Dscription   = Str(l.Dscription),
            FreeTxt      = Str(l.FreeTxt),
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
            WhsCode      = Str(l.WhsCode),
            CouponNo     = new List<object>()
        };
    }
}
