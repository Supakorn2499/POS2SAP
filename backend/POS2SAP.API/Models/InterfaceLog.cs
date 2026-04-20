namespace POS2SAP.API.Models;

public class InterfaceLog
{
    public string Id { get; set; } = string.Empty;          // ULID
    public string PosDocNo { get; set; } = string.Empty;
    public DateTime? PosDocDate { get; set; }
    public string? BranchCode { get; set; }
    public string? BranchName { get; set; }
    public string? PosId { get; set; }
    public string? CardCode { get; set; }
    public string? Channel { get; set; }
    public string InterfaceType { get; set; } = "AR";
    public decimal? DocTotal { get; set; }
    public string? PosData { get; set; }         // JSON snapshot POS source
    public string? SapDocNum { get; set; }
    public string? SapRequest { get; set; }      // JSON sent to SAP
    public string? SapResponse { get; set; }     // JSON from SAP
    public string Status { get; set; } = "PENDING";
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; } = 0;
    public DateTime? SentAt { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? UpdatedBy { get; set; }
}
