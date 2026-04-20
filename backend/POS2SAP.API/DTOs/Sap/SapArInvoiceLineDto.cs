namespace POS2SAP.API.DTOs.Sap;

/// <summary>
/// SAP B1 AR Invoice Line — maps to INV1 table
/// </summary>
public class SapArInvoiceLineDto
{
    /// <summary>POS Bill No (matches header) → DocNum in SAP</summary>
    public string DocNum { get; set; } = string.Empty;

    /// <summary>0-based line number → LineNum</summary>
    public int LineNum { get; set; }

    /// <summary>Item code → ItemCode</summary>
    public string ItemCode { get; set; } = string.Empty;

    /// <summary>Item description → Dscription</summary>
    public string Dscription { get; set; } = string.Empty;

    /// <summary>Additional description → FreeTxt</summary>
    public string? FreeTxt { get; set; }

    /// <summary>Quantity (negative for discount items) → Quantity</summary>
    public decimal Quantity { get; set; }

    /// <summary>Unit of measure code → UomCode</summary>
    public string UomCode { get; set; } = string.Empty;

    /// <summary>Unit of measure name → unitMsr</summary>
    public string UnitMsr { get; set; } = string.Empty;

    /// <summary>Price before discount before VAT → PriceBefDi</summary>
    public decimal PriceBefDi { get; set; }

    /// <summary>Discount % → DiscPrcnt</summary>
    public decimal DiscPrcnt { get; set; }

    /// <summary>Price after discount before VAT → Price</summary>
    public decimal Price { get; set; }

    /// <summary>Price after discount including VAT → PriceAfVat</summary>
    public decimal PriceAfVat { get; set; }

    /// <summary>Fixed: 7 → VatPrcnt</summary>
    public decimal VatPrcnt { get; set; } = 7m;

    /// <summary>Fixed: S07 → VatGroup</summary>
    public string VatGroup { get; set; } = "S07";

    /// <summary>VAT amount for this line → VatSum</summary>
    public decimal VatSum { get; set; }

    /// <summary>Line total before VAT → LineTotal</summary>
    public decimal LineTotal { get; set; }

    /// <summary>Grand total after discount including VAT → GTotal</summary>
    public decimal GTotal { get; set; }

    /// <summary>Warehouse code (from OCRD.U_WhsCode) → WhsCode</summary>
    public string WhsCode { get; set; } = string.Empty;

    /// <summary>Project code = BranchCode → Project</summary>
    public string Project { get; set; } = string.Empty;

    /// <summary>Cost center 1 = BranchCode → OcrCode</summary>
    public string OcrCode { get; set; } = string.Empty;

    /// <summary>Fixed: CENTER → OcrCode2</summary>
    public string OcrCode2 { get; set; } = "CENTER";
}
