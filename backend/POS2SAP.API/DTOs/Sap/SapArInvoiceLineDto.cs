namespace POS2SAP.API.DTOs.Sap;

public class SapArInvoiceLineDto
{
    public string DocNum { get; set; } = string.Empty;
    public int LineNum { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string ItemCategory { get; set; } = string.Empty;
    public string Dscription { get; set; } = string.Empty;
    public string FreeTxt { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string UomCode { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal PriceAfVat { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscPrcnt { get; set; }
    public decimal LineTotal { get; set; }
    public string VatGroup { get; set; } = string.Empty;
    public decimal VatPrcnt { get; set; }
    public decimal VatSum { get; set; }
    public string WhsCode { get; set; } = string.Empty;
    public decimal GTotal { get; set; }
    public string CouponNo { get; set; } = string.Empty;
}