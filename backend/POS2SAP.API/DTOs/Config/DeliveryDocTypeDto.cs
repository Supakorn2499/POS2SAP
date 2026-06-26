namespace POS2SAP.API.DTOs.Config;

/// <summary>POS documenttype row with Delivery (DL) interface selection.</summary>
public class DeliveryDocTypeDto
{
    public int DocumentTypeId { get; set; }
    /// <summary>POS documenttype.DocumentTypeHeader (e.g. STOCK-001).</summary>
    public string DocumentTypeCode { get; set; } = string.Empty;
    public string DocumentTypeName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
}

public class SaveDeliveryDocTypeDto
{
    public List<int> EnabledDocumentTypeIds { get; set; } = new();
}
