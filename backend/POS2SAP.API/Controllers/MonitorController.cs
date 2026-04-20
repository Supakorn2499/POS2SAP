using Microsoft.AspNetCore.Mvc;
using POS2SAP.API.Common;
using POS2SAP.API.DTOs.Monitor;
using POS2SAP.API.Services.Interfaces;

namespace POS2SAP.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MonitorController : ControllerBase
{
    private readonly IInterfaceMonitorService _monitor;

    public MonitorController(IInterfaceMonitorService monitor)
    {
        _monitor = monitor;
    }

    /// <summary>Get paginated log list with filters</summary>
    [HttpGet("logs")]
    public async Task<ActionResult<ApiResponse<PagedResult<InterfaceLogDto>>>> GetLogs(
        [FromQuery] InterfaceLogQueryParams p)
    {
        if (p.PageSize < 1) p.PageSize = 20;
        if (p.PageSize > 100) p.PageSize = 100;
        if (p.Page < 1) p.Page = 1;

        var result = await _monitor.GetListAsync(p);
        return Ok(ApiResponse<PagedResult<InterfaceLogDto>>.Ok(result));
    }

    /// <summary>Get full detail of a single log including JSON payloads</summary>
    [HttpGet("logs/{id}")]
    public async Task<ActionResult<ApiResponse<InterfaceLogDetailDto>>> GetLogDetail(string id)
    {
        var detail = await _monitor.GetDetailAsync(id);
        if (detail is null)
            return NotFound(ApiResponse<InterfaceLogDetailDto>.NotFound($"ไม่พบ log id={id}"));
        return Ok(ApiResponse<InterfaceLogDetailDto>.Ok(detail));
    }

    /// <summary>Get branch list from shop_data for filter dropdown</summary>
    [HttpGet("branches")]
    public async Task<ActionResult<ApiResponse<List<BranchOptionDto>>>> GetBranches()
    {
        var branches = await _monitor.GetBranchesAsync();
        return Ok(ApiResponse<List<BranchOptionDto>>.Ok(branches));
    }

    /// <summary>Get dashboard summary — status counts, daily trend, top branches</summary>
    [HttpGet("dashboard")]
    public async Task<ActionResult<ApiResponse<DashboardSummaryDto>>> GetDashboard([FromQuery] int monthOffset = 0)
    {
        var summary = await _monitor.GetDashboardAsync(monthOffset);
        return Ok(ApiResponse<DashboardSummaryDto>.Ok(summary));
    }

    /// <summary>DEV ONLY: Simulate random statuses for all logs</summary>
    [HttpPost("simulate-statuses")]
    public async Task<ActionResult<ApiResponse<string>>> SimulateStatuses()
    {
        var count = await _monitor.SimulateLogStatusesAsync();
        var message = $"Simulated {count} logs with random statuses.";
        return Ok(ApiResponse<string>.Ok(message));
    }
}
