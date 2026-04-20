namespace POS2SAP.API.DTOs.Monitor;

public class InterfaceLogQueryParams
{
    public string? Search { get; set; }           // DocNo or BranchCode
    public string? Status { get; set; }           // PENDING/PROCESSING/SUCCESS/FAILED/RETRY
    public string? InterfaceType { get; set; }     // AR/AP
    public string? BranchCode { get; set; }
    public string? DateFrom { get; set; }         // yyyy-MM-dd
    public string? DateTo { get; set; }           // yyyy-MM-dd
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string SortBy { get; set; } = "created_at";
    public string SortDirection { get; set; } = "desc";
}
