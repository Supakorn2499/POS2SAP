namespace POS2SAP.API.DTOs.Sap;

/// <summary>
/// SAP B1 Incoming Payment head — maps to one ordertransaction receipt.
/// JSON spec: paymentCreditCards (lowercase p) matches SAP endpoint contract exactly.
/// </summary>
public class SapIncomingPaymentDto
{
    public string DocNum { get; set; } = string.Empty;   // ReceiptNumber
    public string DocDate { get; set; } = string.Empty;  // SaleDate (yyyy-MM-dd)
    public string DocType { get; set; } = "C";           // fixed "C" (Customer)
    public string CardCode { get; set; } = string.Empty; // shop_data.SLOC
    public string CardName { get; set; } = string.Empty; // MemberName
    public string POSID { get; set; } = string.Empty;    // ComputerID

    // Bank transfer / PromptPay (TRANSFER category in paytype_gl_mapping)
    public string TrsfrAcct { get; set; } = string.Empty;
    public decimal TrsfrSum { get; set; }
    public string TrsfrDate { get; set; } = string.Empty;
    public string TrsfrRef { get; set; } = string.Empty; // CCApproveCode or PayRemark

    // Cash (CASH category in paytype_gl_mapping)
    public string CashAcct { get; set; } = string.Empty;
    public decimal CashSum { get; set; }

    public string PayNoDoc { get; set; } = "N";  // fixed
    public decimal NoDocSum { get; set; }         // fixed 0

    public string DocCur { get; set; } = "THB";  // gbVar.SapDocCur
    public string BranchCode { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string Comments { get; set; } = string.Empty;

    /// <summary>Links this payment to its SAP AR Invoice (InvoiceNum = interface_logs.sap_doc_num).</summary>
    public List<SapPaymentInvoiceLineDto> PaymentInvoices { get; set; } = new();

    /// <summary>One entry per credit card / delivery platform / voucher payment row.
    /// Field name is lowercase 'p' to match SAP endpoint contract exactly.</summary>
    public List<SapPaymentCreditCardDto> paymentCreditCards { get; set; } = new();
}

/// <summary>Links Incoming Payment to its SAP AR Invoice document.</summary>
public class SapPaymentInvoiceLineDto
{
    public string DocNum { get; set; } = string.Empty;    // same as head DocNum
    public int LineNum { get; set; }                      // 0-based index
    public int InvType { get; set; } = 13;                // 13 = AR Invoice (SAP constant)
    public string InvoiceNum { get; set; } = string.Empty; // interface_logs.sap_doc_num (AR)
    public int Dcount { get; set; }                        // discount count, usually 0
    public decimal SumApplied { get; set; }                // amount applied = DocTotal of receipt
}

/// <summary>
/// One row per credit card / delivery platform / voucher payment.
/// CREDIT_CARD category in paytype_gl_mapping.
/// </summary>
public class SapPaymentCreditCardDto
{
    public string DocNum { get; set; } = string.Empty;    // same as head DocNum
    public int LineNum { get; set; }                      // 0-based index within transaction
    public string CreditCard { get; set; } = string.Empty; // paytype_gl_mapping.SapPayTypeName
    public string CreditAcct { get; set; } = string.Empty; // paytype_gl_mapping.SapGlAccount
    public string CrCardNum { get; set; } = string.Empty;  // orderpaydetail.CreditCardNo (last 4)
    public string CardValid { get; set; } = string.Empty;  // "YYYY-MM-DD" from ExpireYear/ExpireMonth
    public string CreditCardBank { get; set; } = string.Empty;
    public decimal CreditSum { get; set; }                 // orderpaydetail.PayAmount
    public string VoucherNum { get; set; } = string.Empty; // CCApproveCode ?? VoucherNo
}
