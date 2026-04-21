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

    public Task<List<SapArInvoiceRequestDto>> GetPendingBillsAsync(int batchSize = 500)
        => GetPendingBillsAsyncImpl(batchSize);

    private async Task<List<SapArInvoiceRequestDto>> GetPendingBillsAsyncImpl(int batchSize = 500)
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
                ISNULL(s.BranchNo, CAST(a.ShopID AS NVARCHAR(20)))       AS BranchCode,
                ISNULL(s.BranchName, '')                                 AS BranchName,
                CAST(a.ComputerID AS NVARCHAR(20))                       AS PosId,
                ISNULL(a.MemberID, 'C0000001')                           AS CardCode,
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
                NULL                                                     AS CustRedeemPoint,
                NULL                                                     AS CustBalancePoint,
                a.ReceiptRetailPrice                                     AS TotalAmtBefDis,
                0                                                        AS DiscPrcnt,
                a.TotalDiscount                                          AS DiscSum,
                NULL                                                     AS DownPaymentNo,
                NULL                                                     AS DownPaymentAmt,
                a.TransactionVAT                                         AS VatSum,
                a.ReceiptPayPrice                                        AS DocTotal,
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
                ISNULL(c.ProductName, '')                                  AS Dscription,
                b.Comment                                                  AS FreeTxt,
                b.TotalQty                                                 AS Quantity,
                ISNULL(c.ProductUnitName, '')                              AS UomCode,
                ISNULL(c.ProductUnitName, '')                              AS UnitMsr,
                b.PricePerUnit                                             AS PriceBefDi,
                ISNULL(b.DiscPricePercent, 0)                             AS DiscPrcnt,
                b.ProductBeforeVAT / NULLIF(b.TotalQty, 0)                AS Price,
                (b.ProductBeforeVAT + b.ProductVAT) / NULLIF(b.TotalQty, 0) AS PriceAfVat,
                b.ProductVAT                                               AS VatSum,
                b.ProductBeforeVAT                                         AS LineTotal,
                b.ProductBeforeVAT + b.ProductVAT                         AS GTotal,
                ISNULL(CAST(b.InventoryID AS NVARCHAR(20)), '')            AS WhsCode
            FROM orderdetail b
            LEFT JOIN products c ON c.ProductID = b.ProductID
            WHERE b.TranKey IN ({tranKeyCsv})
            ORDER BY b.TranKey, b.DisplayOrdering, b.OrderDetailID";

        var allLines = (await _db.QueryAsync<dynamic>(allLinesSql, commandTimeout: 120)).ToList();

        // Group lines by TranKey
        var linesByKey = allLines.GroupBy(l => (string)l.TranKey)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var results = new List<SapArInvoiceRequestDto>();
        foreach (var h in heads)
        {
            var dto = MapHead(h);
            var lines = linesByKey.GetValueOrDefault((string)h.TranKey) ?? new List<dynamic>();
            dto.Lines = lines.Select<dynamic, SapArInvoiceLineDto>((l, idx) => MapLine(l, Str(h.PosDocNo), idx, Str(h.BranchCode))).ToList();
            results.Add(dto);
        }

        return results;
    }

    public Task<List<SapArInvoiceRequestDto>> GetBillsByDocNosAsync(IEnumerable<string> docNos)
        => GetBillsByDocNosAsyncImpl(docNos);

    private async Task<List<SapArInvoiceRequestDto>> GetBillsByDocNosAsyncImpl(IEnumerable<string> docNos)
    {
        var docNoList = docNos.ToList();
        if (!docNoList.Any()) return new();

        var headSql = @"
            SELECT
                a.ReceiptNumber                                          AS PosDocNo,
                a.SaleDate                                               AS DocDate,
                ISNULL(s.BranchNo, CAST(a.ShopID AS NVARCHAR(20)))       AS BranchCode,
                ISNULL(s.BranchName, '')                                 AS BranchName,
                CAST(a.ComputerID AS NVARCHAR(20))                       AS PosId,
                ISNULL(a.MemberID, 'C0000001')                         AS CardCode,
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
                NULL                                                     AS CustRedeemPoint,
                NULL                                                     AS CustBalancePoint,
                a.ReceiptRetailPrice                                     AS TotalAmtBefDis,
                0                                                        AS DiscPrcnt,
                a.TotalDiscount                                          AS DiscSum,
                NULL                                                     AS DownPaymentNo,
                NULL                                                     AS DownPaymentAmt,
                a.TransactionVAT                                         AS VatSum,
                a.ReceiptPayPrice                                        AS DocTotal,
                a.TranKey
            FROM ordertransaction a
            LEFT JOIN shop_data s ON s.ShopID = a.ShopID
            LEFT JOIN salemode sm ON sm.SaleModeID = TRY_CAST(a.SaleMode AS INT)
            WHERE a.ReceiptNumber IN @DocNos
              AND a.TransactionStatusID = 2
              AND ISNULL(a.Deleted, 0) = 0
            ORDER BY a.SaleDate, a.ReceiptNumber";

        var heads = (await _db.QueryAsync<dynamic>(headSql, new { DocNos = docNoList })).ToList();
        var results = new List<SapArInvoiceRequestDto>();

        foreach (var h in heads)
        {
            var dto = MapHead(h);

            var lineSql = @"
                SELECT
                    ISNULL(c.ProductCode, CAST(b.ProductID AS NVARCHAR(50)))  AS ItemCode,
                    ISNULL(c.ProductName, '')                                  AS Dscription,
                    b.Comment                                                  AS FreeTxt,
                    b.TotalQty                                                 AS Quantity,
                    ISNULL(c.ProductUnitName, '')                              AS UomCode,
                    ISNULL(c.ProductUnitName, '')                              AS UnitMsr,
                    b.PricePerUnit                                             AS PriceBefDi,
                    ISNULL(b.DiscPricePercent, 0)                             AS DiscPrcnt,
                    b.ProductBeforeVAT / NULLIF(b.TotalQty, 0)                AS Price,
                    (b.ProductBeforeVAT + b.ProductVAT) / NULLIF(b.TotalQty, 0) AS PriceAfVat,
                    b.ProductVAT                                               AS VatSum,
                    b.ProductBeforeVAT                                         AS LineTotal,
                    b.ProductBeforeVAT + b.ProductVAT                         AS GTotal,
                    ISNULL(CAST(b.InventoryID AS NVARCHAR(20)), '')            AS WhsCode
                FROM orderdetail b
                LEFT JOIN products c ON c.ProductID = b.ProductID
                WHERE b.TranKey = @TranKey
                  AND ISNULL(b.Deleted, 0) = 0
                  AND ISNULL(b.ComponentLevel, 0) = 0
                ORDER BY b.DisplayOrdering, b.OrderDetailID";

            var lines = (await _db.QueryAsync<dynamic>(lineSql, new {
                TranKey = (string)h.TranKey
            })).ToList();
            dto.Lines = lines.Select<dynamic, SapArInvoiceLineDto>((l, idx) => MapLine(l, Str(h.PosDocNo), idx, Str(h.BranchCode))).ToList();
            results.Add(dto);
        }

        return results;
    }

    // ------------------------------------------------------------------ Mappers

    private static string Str(object? val) => val?.ToString() ?? string.Empty;
    private static decimal Dec(object? val) => val == null || val is DBNull ? 0m : Convert.ToDecimal(val);
    private static decimal? DecNull(object? val) => val == null || val is DBNull ? null : Convert.ToDecimal(val);

    private static SapArInvoiceRequestDto MapHead(dynamic h)
    {
        var docDate = h.DocDate is DateTime dt ? dt.ToString("yyyyMMdd") : h.DocDate?.ToString() ?? string.Empty;
        return new SapArInvoiceRequestDto
        {
            Head = new()
            {
                DocNum          = Str(h.PosDocNo),
                DocDate         = docDate,
                DocDueDate      = docDate,
                PymntGroup      = "Cash",
                POSID           = Str(h.PosId),
                CardCode        = Str(h.CardCode),
                CardName        = Str(h.CardName),
                CustTaxId       = Str(h.CustTaxId),
                Address         = Str(h.Address),
                CustVatBranch   = Str(h.CustVatBranch),
                CustTel         = Str(h.CustTel),
                CustMemberNo    = Str(h.CustMemberNo),
                DocCur          = "THB",
                BranchCode      = Str(h.BranchCode),
                BranchName      = Str(h.BranchName),
                VatBranch       = Str(h.VatBranch),
                Comments        = Str(h.Comments),
                Channel         = Str(h.Channel),
                CustBillPoint   = DecNull(h.CustBillPoint),
                CustRedeemPoint = DecNull(h.CustRedeemPoint),
                CustBalancePoint = DecNull(h.CustBalancePoint),
                TotalAmtBefDis  = Dec(h.TotalAmtBefDis),
                DiscPrcnt       = Dec(h.DiscPrcnt),
                DiscSum         = Dec(h.DiscSum),
                DownPaymentNo   = Str(h.DownPaymentNo),
                DownPaymentAmt  = DecNull(h.DownPaymentAmt),
                VatSum          = Dec(h.VatSum),
                DocTotal        = Dec(h.DocTotal)
            },
            Lines = new()
        };
    }

    private static SapArInvoiceLineDto MapLine(dynamic l, string docNum, int idx, string branchCode)
    {
        return new SapArInvoiceLineDto
        {
            DocNum      = docNum,
            LineNum     = idx,
            ItemCode    = Str(l.ItemCode),
            Dscription  = Str(l.Dscription),
            FreeTxt     = Str(l.FreeTxt),
            Quantity    = Dec(l.Quantity),
            UomCode     = Str(l.UomCode),
            UnitMsr     = Str(l.UnitMsr),
            PriceBefDi  = Dec(l.PriceBefDi),
            DiscPrcnt   = Dec(l.DiscPrcnt),
            Price       = Dec(l.Price),
            PriceAfVat  = Dec(l.PriceAfVat),
            VatPrcnt    = 7m,
            VatGroup    = "S07",
            VatSum      = Dec(l.VatSum),
            LineTotal   = Dec(l.LineTotal),
            GTotal      = Dec(l.GTotal),
            WhsCode     = Str(l.WhsCode),
            Project     = branchCode,
            OcrCode     = branchCode,
            OcrCode2    = "CENTER"
        };
    }
}
