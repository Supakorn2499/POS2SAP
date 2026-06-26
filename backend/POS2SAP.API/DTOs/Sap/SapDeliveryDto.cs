namespace POS2SAP.API.DTOs.Sap;

/// <summary>SAP B1 Delivery (ODLN head + DLN1 lines) — JSON shape matches SRC-Spec Delivery example.</summary>
public class SapDeliveryDto
{
    public string DocNum { get; set; } = string.Empty;
    public string DocDate { get; set; } = string.Empty;
    public string POSID { get; set; } = string.Empty;
    public string CardCode { get; set; } = string.Empty;
    public string CardName { get; set; } = string.Empty;
    public string BranchCode { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public string VatBranch { get; set; } = string.Empty;
    public string DeliveryReason { get; set; } = string.Empty;
    public string DeliveryReasonOther { get; set; } = string.Empty;
    public string Comments { get; set; } = string.Empty;

    public List<SapDeliveryLineDto> DocumentLines { get; set; } = new();
}

public class SapDeliveryLineDto
{
    public string DocNum { get; set; } = string.Empty;
    public int LineNum { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string Dscription { get; set; } = string.Empty;
    public string FreeTxt { get; set; } = string.Empty;
    /// <summary>SAP spec example uses string quantities ("2", "1").</summary>
    public string Quantity { get; set; } = "0";
    public string UomCode { get; set; } = string.Empty;
    public string unitMsr { get; set; } = string.Empty;
    public string WhsCode { get; set; } = string.Empty;
}
