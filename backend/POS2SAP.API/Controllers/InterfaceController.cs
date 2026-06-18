using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using POS2SAP.API.Common;
using POS2SAP.API.Services.Interfaces;
using System.Text.Json;
using System.Text.Json.Nodes;
using POS2SAP.API.DTOs.Sap; // DTO for SAP data structures
using POS2SAP.API.DTOs.Monitor; // เพิ่ม using statement นี้
using POS2SAP.API.Models;

namespace POS2SAP.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InterfaceController : ControllerBase
{
    private readonly IInterfaceJobService _job;
    private readonly ILogger<InterfaceController> _logger;
    private readonly IInterfaceMonitorService _monitor;

    public InterfaceController(IInterfaceJobService job, IInterfaceMonitorService monitor, ILogger<InterfaceController> logger)
    {
        _job    = job;
        _monitor = monitor;
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
        var (fetched, imported, error) = await _job.ImportPreviewAsync(request?.DocNos, request?.InterfaceType);
        var result = new ImportResultDto { Fetched = fetched, Imported = imported, Error = error };
        var msg = fetched == 0
            ? "ไม่พบข้อมูลใหม่จาก POS (อาจถูก import ไปแล้วทั้งหมด)"
            : $"พบ {fetched} รายการ — import สำเร็จ {imported} รายการ สถานะ PENDING";
        return Ok(ApiResponse<ImportResultDto>.Ok(result, msg));
    }

    /// <summary>Resend multiple records based on their SapArInvoiceHeadDto data</summary>
    [HttpPost("resend")]
    public async Task<ActionResult<ApiResponse<TriggerResultDto>>> Resend([FromBody] ResendRequestDto request)
    {
        _logger.LogInformation("Batch resend requested, items={Count}", request?.Request?.Count ?? 0);
        if (request?.Request == null || !request.Request.Any())
        {
            return BadRequest(ApiResponse<TriggerResultDto>.Fail("No items provided for resend."));
        }

        // This assumes we can use the DocNum from the request to trigger the resend job.
        // If the resend logic is more complex, the IInterfaceJobService might need adjustments.
        var docNosToResend = request.Request.Select(x => x.DocNum).ToList();
        var (sent, failed) = await _job.TriggerManualAsync(docNosToResend);
        var result = new TriggerResultDto { Sent = sent, Failed = failed, Total = sent + failed };
        return Ok(ApiResponse<TriggerResultDto>.Ok(result, $"ส่งสำเร็จ {sent} รายการ, ล้มเหลว {failed} รายการ"));
    }

    [HttpPost("upload")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> UploadRaw([FromBody] JsonElement body)
    {
        try
        {
            var json = body.GetRawText();
            var bill = DeserializeRequest(json);
            if (bill is null) return BadRequest(ApiResponse<string>.Fail("ไม่สามารถแปลง POS JSON ได้"));

            var posJson = JsonSerializer.Serialize(new[] { bill });
            var log = new InterfaceLog
            {
                PosDocNo = bill.DocNum,
                PosDocDate = DateTime.TryParse(bill.DocDate, out var d) ? d : null,
                BranchCode = bill.BranchCode,
                BranchName = bill.BranchName,
                PosId = bill.POSID,
                CardCode = bill.CardCode,
                Channel = bill.Channel,
                DocTotal = bill.DocTotal,
                PosData = posJson,
                SapRequest = null,
                Status = gbVar.StatusPending
            };

            var id = await _monitor.InsertLogAsync(log);
            var resp = new { Id = id, PosData = posJson };
            return Ok(ApiResponse<object>.Ok(resp, "Import test: created PENDING log"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UploadRaw failed");
            return BadRequest(ApiResponse<string>.Fail(ex.Message));
        }
    }
    
    private static SapArInvoiceHeadDto? DeserializeRequest(string json)
    {
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        try
        {
            var invoices = JsonSerializer.Deserialize<List<SapArInvoiceHeadDto>>(json, opts);
            if (invoices is not null && invoices.Any())
            {
                return invoices[0];
            }
        }
        catch { }

        try
        {
            var invoice = JsonSerializer.Deserialize<SapArInvoiceHeadDto>(json, opts);
            if (invoice is not null && !string.IsNullOrEmpty(invoice.DocNum))
            {
                return invoice;
            }
        }
        catch { }

        return null;
    }
}

public class TriggerRequestDto
{
    public List<string>? DocNos { get; set; }
    public string? InterfaceType { get; set; }
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
