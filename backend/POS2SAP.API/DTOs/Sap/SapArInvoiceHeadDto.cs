namespace POS2SAP.API.DTOs.Sap;

public class SapArInvoiceHeadDto
{
    public string DocNum { get; set; } = string.Empty;
    public string DocDate { get; set; } = string.Empty;
    public string PymntGroup { get; set; } = string.Empty;
    public string DocDueDate { get; set; } = string.Empty;
    public string POSID { get; set; } = string.Empty;
    public string CardCode { get; set; } = string.Empty;
    public string CardName { get; set; } = string.Empty;
    public string CustTaxId { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string CustVatBranch { get; set; } = string.Empty;
    public string CustTel { get; set; } = string.Empty;
    public string CustMemberNo { get; set; } = string.Empty;
    public string DocCur { get; set; } = string.Empty;
    public string BranchCode { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public string VatBranch { get; set; } = string.Empty;
    public string Comments { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public int CustBillPoint { get; set; }
    public int CustRedeemPoing { get; set; } // Intentional typo — matches SAP spec
    public int CustBalancePoint { get; set; }
    public decimal TotalAmtBefDis { get; set; }
    public decimal DiscPrcnt { get; set; }
    public string DownPaymentNo { get; set; } = string.Empty;
    public decimal DownPaymentAmt { get; set; }
    public decimal DocTotal { get; set; }
    public List<SapArInvoiceLineDto> DocumentLines { get; set; } = new();
}