namespace POS2SAP.API.DTOs.Sap;

/// <summary>
/// SAP B1 AR Invoice Header — maps to OINV table
/// </summary>
public class SapArInvoiceHeadDto
{
    /// <summary>POS Bill No → NumAtCard / U_OLDTAX</summary>
    public string DocNum { get; set; } = string.Empty;

    /// <summary>yyyymmdd → DocDate</summary>
    public string DocDate { get; set; } = string.Empty;

    /// <summary>Fixed: Cash → PymntGroup (SELECT GroupNum FROM OCTG WHERE PymntGroup='Cash')</summary>
    public string PymntGroup { get; set; } = "Cash";

    /// <summary>yyyymmdd → DocDueDate (same as DocDate for cash)</summary>
    public string DocDueDate { get; set; } = string.Empty;

    /// <summary>POS Terminal ID → U_POSID</summary>
    public string POSID { get; set; } = string.Empty;

    /// <summary>Branch debtor code → CardCode</summary>
    public string CardCode { get; set; } = string.Empty;

    /// <summary>Customer name → CardName / U_InvoiceName</summary>
    public string CardName { get; set; } = string.Empty;

    /// <summary>Tax ID (Full Tax only) → LicTradNum</summary>
    public string? CustTaxId { get; set; }

    /// <summary>Customer address (Full Tax only) → Address / U_InvoiceAddress</summary>
    public string? Address { get; set; }

    /// <summary>Customer VAT branch (Full Tax only) → U_CustVatBranch</summary>
    public string? CustVatBranch { get; set; }

    /// <summary>Customer phone → U_CustTel</summary>
    public string? CustTel { get; set; }

    /// <summary>Member number → U_CustMemberNo</summary>
    public string? CustMemberNo { get; set; }

    /// <summary>Fixed: THB → DocCur</summary>
    public string DocCur { get; set; } = "THB";

    /// <summary>Branch code → U_BranchCode</summary>
    public string BranchCode { get; set; } = string.Empty;

    /// <summary>Branch name → U_BranchName</summary>
    public string BranchName { get; set; } = string.Empty;

    /// <summary>VAT branch number → U_VATBRANCH</summary>
    public string VatBranch { get; set; } = string.Empty;

    /// <summary>Remarks → Comments</summary>
    public string? Comments { get; set; }

    /// <summary>Sales channel → U_Channel</summary>
    public string Channel { get; set; } = string.Empty;

    /// <summary>Points earned this bill → U_CustPoint</summary>
    public decimal? CustBillPoint { get; set; }

    /// <summary>Points redeemed this bill → U_CustRedeemPoing</summary>
    public decimal? CustRedeemPoint { get; set; }

    /// <summary>Remaining balance points → U_CustBalancePoint</summary>
    public decimal? CustBalancePoint { get; set; }

    /// <summary>Total before discount → U_TotalAmtBefDis</summary>
    public decimal TotalAmtBefDis { get; set; }

    /// <summary>Bill-level discount % → DiscPrcnt</summary>
    public decimal DiscPrcnt { get; set; }

    /// <summary>Total discount amount → DiscSum</summary>
    public decimal DiscSum { get; set; }

    /// <summary>Down payment ticket no (table booking) → ODPI lookup</summary>
    public string? DownPaymentNo { get; set; }

    /// <summary>Down payment deducted amount → DpmAmnt</summary>
    public decimal? DownPaymentAmt { get; set; }

    /// <summary>Total VAT → VatSum</summary>
    public decimal VatSum { get; set; }

    /// <summary>Grand total → DocTotal</summary>
    public decimal DocTotal { get; set; }
}
