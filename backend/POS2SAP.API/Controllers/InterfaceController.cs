using Microsoft.AspNetCore.Mvc;
using POS2SAP.API.Common;
using POS2SAP.API.Services.Interfaces;

namespace POS2SAP.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InterfaceController : ControllerBase
{
    private readonly IInterfaceJobService _job;
    private readonly ILogger<InterfaceController> _logger;

    public InterfaceController(IInterfaceJobService job, ILogger<InterfaceController> logger)
    {
        _job    = job;
        _logger = logger;
    }

    /// <summary>Manual trigger — send all PENDING/RETRY or specific docNos</summary>
    [HttpPost("trigger")]
    public async Task<ActionResult<ApiResponse<TriggerResultDto>>> Trigger([FromBody] TriggerRequestDto? request)
    {
        _logger.LogInformation("Manual trigger requested, docNos={Count}", request?.DocNos?.Count ?? 0);
        var (sent, failed) = await _job.TriggerManualAsync(request?.DocNos);
        var result = new TriggerResultDto { Sent = sent, Failed = failed, Total = sent + failed };
        return Ok(ApiResponse<TriggerResultDto>.Ok(result, $"ส่งสำเร็จ {sent} รายการ, ล้มเหลว {failed} รายการ"));
    }

    /// <summary>Retry a single FAILED record by log ID</summary>
    [HttpPost("retry/{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> Retry(string id)
    {
        _logger.LogInformation("Retry requested for logId={Id}", id);
        var success = await _job.RetryAsync(id);
        return success
            ? Ok(ApiResponse<bool>.Ok(true, "Retry สำเร็จ"))
            : BadRequest(ApiResponse<bool>.Fail("Retry ล้มเหลว หรือ record ไม่พร้อม retry"));
    }

    /// <summary>Import preview — ดึงข้อมูลจาก POS แล้ว insert เป็น PENDING ยังไม่ส่ง SAP</summary>
    [HttpPost("import")]
    public async Task<ActionResult<ApiResponse<ImportResultDto>>> Import([FromBody] TriggerRequestDto? request)
    {
        _logger.LogInformation("Import preview requested, docNos={Count}", request?.DocNos?.Count ?? 0);
        var (fetched, imported, error) = await _job.ImportPreviewAsync(request?.DocNos);
        var result = new ImportResultDto { Fetched = fetched, Imported = imported, Error = error };
        var msg = fetched == 0
            ? "ไม่พบข้อมูลใหม่จาก POS (อาจถูก import ไปแล้วทั้งหมด)"
            : $"พบ {fetched} รายการ — import สำเร็จ {imported} รายการ สถานะ PENDING";
        return Ok(ApiResponse<ImportResultDto>.Ok(result, msg));
    }
}

public class TriggerRequestDto
{
    public List<string>? DocNos { get; set; }
}

public class TriggerResultDto
{
    public int Sent { get; set; }
    public int Failed { get; set; }
    public int Total { get; set; }
}

public class ImportResultDto
{
    public int Fetched { get; set; }
    public int Imported { get; set; }
    public string? Error { get; set; }
}
