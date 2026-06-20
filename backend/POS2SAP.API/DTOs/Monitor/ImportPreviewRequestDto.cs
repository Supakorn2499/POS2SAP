namespace POS2SAP.API.DTOs.Monitor;

public class ImportPreviewRequestDto
{
    /// <summary>Start date (yyyy-MM-dd)</summary>
    public string DateFrom { get; set; } = string.Empty;

    /// <summary>End date inclusive (yyyy-MM-dd)</summary>
    public string DateTo { get; set; } = string.Empty;

    /// <summary>Optional branch code filter (BranchNo from shop_data)</summary>
    public string? BranchCode { get; set; }

    /// <summary>Document type: ARInvoice | IncomingPayment | Delivery</summary>
    public string InterfaceType { get; set; } = "ARInvoice";

    /// <summary>Max records to fetch from POS (1–1000)</summary>
    public int BatchSize { get; set; } = 500;
}
