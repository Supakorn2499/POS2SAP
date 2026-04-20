namespace POS2SAP.API.DTOs.Monitor;

public class DashboardSummaryDto
{
    public StatusCountDto Counts { get; set; } = new();
    public List<DailyTrendDto> DailyTrend { get; set; } = new();  // last 7 days
    public List<BranchStatDto> TopBranches { get; set; } = new(); // top 10
    public List<BranchStatDto> TopFailedBranches { get; set; } = new(); // top 10 by failure count
    public List<InterfaceLogDto> RecentLogs { get; set; } = new(); // last 10
}

public class StatusCountDto
{
    public int Pending { get; set; }
    public int Processing { get; set; }
    public int Success { get; set; }
    public int Failed { get; set; }
    public int Retry { get; set; }
    public int Total { get; set; }
}

public class DailyTrendDto
{
    public string Date { get; set; } = string.Empty;   // yyyy-MM-dd
    public int Success { get; set; }
    public int Failed { get; set; }
    public int Total { get; set; }
}

public class BranchStatDto
{
    public string BranchCode { get; set; } = string.Empty;
    public string? BranchName { get; set; }
    public int Total { get; set; }
    public int Success { get; set; }
    public int Failed { get; set; }
}
