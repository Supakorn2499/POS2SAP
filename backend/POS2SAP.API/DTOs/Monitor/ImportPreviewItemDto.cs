namespace POS2SAP.API.DTOs.Monitor;

public class ImportPreviewItemDto
{
    public string DocNum { get; set; } = string.Empty;
    public string DocDate { get; set; } = string.Empty;
    public string BranchCode { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public decimal DocTotal { get; set; }
    /// <summary>True if this doc already exists in interface_logs for the requested interface type.</summary>
    public bool AlreadyImported { get; set; }
}
