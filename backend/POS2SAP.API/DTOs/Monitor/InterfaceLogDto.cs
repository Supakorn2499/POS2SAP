namespace POS2SAP.API.DTOs.Monitor;

/// <summary>List view — no JSON payload for performance</summary>
public class InterfaceLogDto
{
    public string Id { get; set; } = string.Empty;
    public string PosDocNo { get; set; } = string.Empty;
    public DateTime? PosDocDate { get; set; }
    public string? BranchCode { get; set; }
    public string? BranchName { get; set; }
    public string? PosId { get; set; }
    public string? CardCode { get; set; }
    public string? Channel { get; set; }
    public string InterfaceType { get; set; } = string.Empty;
    public decimal? DocTotal { get; set; }
    public string? SapDocNum { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
