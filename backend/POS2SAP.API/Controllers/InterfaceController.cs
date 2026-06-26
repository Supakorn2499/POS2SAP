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
    private readonly IPosDataService _posData;
    private readonly IWebHostEnvironment _env;

    public InterfaceController(
        IInterfaceJobService job,
        IInterfaceMonitorService monitor,
        IPosDataService posData,
        ILogger<InterfaceController> logger,
        IWebHostEnvironment env)
    {
        _job     = job;
        _monitor = monitor;
        _posData = posData;
        _logger  = logger;
        _env     = env;
    }

    /// <summary>Manual trigger — send all PENDING/RETRY or specific docNos</summary>
    [HttpPost("trigger")]
    public async Task<ActionResult<ApiResponse<TriggerResultDto>>> Trigger([FromBody] TriggerRequestDto? request)
    {
        _logger.LogInformation("Manual trigger requested, docNos={Count}", request?.DocNos?.Count ?? 0);
        var (sent, failed) = await _job.TriggerManualAsync(request?.DocNos, request?.InterfaceType);
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
        _logger.LogInformation("Import preview requested, docNos={Count} branch={Branch}", request?.DocNos?.Count ?? 0, request?.BranchCode);
        var (fetched, imported, error) = await _job.ImportPreviewAsync(request?.DocNos, request?.InterfaceType, request?.BranchCode);
        var result = new ImportResultDto { Fetched = fetched, Imported = imported, Error = error };
        var msg = fetched == 0
            ? "ไม่พบข้อมูลใหม่จาก POS (อาจถูก import ไปแล้วทั้งหมด)"
            : $"พบ {fetched} รายการ — import สำเร็จ {imported} รายการ สถานะ PENDING";
        return Ok(ApiResponse<ImportResultDto>.Ok(result, msg));
    }

    /// <summary>Preview POS bills by date range / branch before importing</summary>
    [HttpPost("preview")]
    public async Task<ActionResult<ApiResponse<List<ImportPreviewItemDto>>>> PreviewImport([FromBody] ImportPreviewRequestDto request)
    {
        if (!DateTime.TryParse(request.DateFrom, out var dateFrom))
            return BadRequest(ApiResponse<List<ImportPreviewItemDto>>.Fail("DateFrom ไม่ถูกต้อง (ใช้รูปแบบ yyyy-MM-dd)"));
        if (!DateTime.TryParse(request.DateTo, out var dateTo))
            return BadRequest(ApiResponse<List<ImportPreviewItemDto>>.Fail("DateTo ไม่ถูกต้อง (ใช้รูปแบบ yyyy-MM-dd)"));
        if (dateFrom > dateTo)
            return BadRequest(ApiResponse<List<ImportPreviewItemDto>>.Fail("DateFrom ต้องไม่มากกว่า DateTo"));

        var configDict = await _monitor.GetConfigDictAsync();
        (dateFrom, dateTo) = ScheduleConfigHelper.ClampImportRange(configDict, dateFrom, dateTo);

        var batchSize = Math.Clamp(request.BatchSize <= 0 ? 500 : request.BatchSize, 1, 1000);
        var dbInterfaceType = request.InterfaceType.Trim().ToUpper() switch
        {
            "ARINVOICE"        => "AR",
            "INCOMINGPAYMENT"  => "AP",
            "DELIVERY"         => "DL",
            _                  => "AR"
        };

        _logger.LogInformation("PreviewImport: dateFrom={From} dateTo={To} branch={Branch} type={Type}",
            dateFrom, dateTo, request.BranchCode, dbInterfaceType);

        // ── Incoming Payment: query payment data (requires paytype_gl_mapping table) ──
        if (dbInterfaceType == "AP")
        {
            List<SapIncomingPaymentDto> payments;
            try
            {
                payments = await _posData.GetPaymentsByFilterAsync(dateFrom, dateTo, request.BranchCode, batchSize);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PreviewImport AP: failed to fetch from POS");
                return StatusCode(500, ApiResponse<List<ImportPreviewItemDto>>.Fail("ดึงข้อมูล Incoming Payment จาก POS ไม่สำเร็จ: " + ex.Message));
            }

            if (!payments.Any())
                return Ok(ApiResponse<List<ImportPreviewItemDto>>.Ok(new List<ImportPreviewItemDto>(), "ไม่พบข้อมูลใน POS ตามเงื่อนไขที่กำหนด"));

            var payDocNos  = payments.Select(p => p.DocNum).ToList();
            var alreadyAp  = await _monitor.GetImportedDocNosAsync(payDocNos, "AP");

            var payResult = payments.Select(p => new ImportPreviewItemDto
            {
                DocNum          = p.DocNum,
                DocDate         = p.DocDate,
                BranchCode      = p.BranchCode,
                BranchName      = p.BranchName,
                Channel         = p.Channel,
                DocTotal        = p.CashSum + p.TrsfrSum + p.paymentCreditCards.Sum(c => c.CreditSum),
                AlreadyImported = alreadyAp.Contains(p.DocNum)
            }).ToList();

            var newPayCount = payResult.Count(r => !r.AlreadyImported);
            return Ok(ApiResponse<List<ImportPreviewItemDto>>.Ok(payResult,
                $"พบ {payResult.Count} รายการ ({newPayCount} รายการใหม่, {payResult.Count - newPayCount} นำเข้าแล้ว)"));
        }

        // ── Delivery ──
        if (dbInterfaceType == "DL")
        {
            List<SapDeliveryDto> deliveries;
            try
            {
                deliveries = await _posData.GetDeliveriesByFilterAsync(dateFrom, dateTo, request.BranchCode, batchSize);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PreviewImport DL: failed to fetch from POS");
                return StatusCode(500, ApiResponse<List<ImportPreviewItemDto>>.Fail("ดึงข้อมูล Delivery จาก POS ไม่สำเร็จ: " + ex.Message));
            }

            if (!deliveries.Any())
                return Ok(ApiResponse<List<ImportPreviewItemDto>>.Ok(new List<ImportPreviewItemDto>(), "ไม่พบข้อมูลใน POS ตามเงื่อนไขที่กำหนด"));

            var dlDocNos  = deliveries.Select(d => d.DocNum).ToList();
            var alreadyDl = await _monitor.GetImportedDocNosAsync(dlDocNos, "DL");

            var dlResult = deliveries.Select(d => new ImportPreviewItemDto
            {
                DocNum          = d.DocNum,
                DocDate         = d.DocDate,
                BranchCode      = d.BranchCode,
                BranchName      = d.BranchName,
                DocTotal        = d.DocumentLines.Sum(l => decimal.TryParse(l.Quantity, out var q) ? q : 0m),
                AlreadyImported = alreadyDl.Contains(d.DocNum)
            }).ToList();

            var newDlCount = dlResult.Count(r => !r.AlreadyImported);
            return Ok(ApiResponse<List<ImportPreviewItemDto>>.Ok(dlResult,
                $"พบ {dlResult.Count} รายการ ({newDlCount} รายการใหม่, {dlResult.Count - newDlCount} นำเข้าแล้ว)"));
        }

        // ── AR Invoice ──
        List<SapArInvoiceHeadDto> bills;
        try
        {
            bills = await _posData.GetBillsByFilterAsync(dateFrom, dateTo, request.BranchCode, batchSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PreviewImport: failed to fetch from POS");
            return StatusCode(500, ApiResponse<List<ImportPreviewItemDto>>.Fail("ดึงข้อมูลจาก POS ไม่สำเร็จ: " + ex.Message));
        }

        if (!bills.Any())
            return Ok(ApiResponse<List<ImportPreviewItemDto>>.Ok(new List<ImportPreviewItemDto>(), "ไม่พบข้อมูลใน POS ตามเงื่อนไขที่กำหนด"));

        var docNos = bills.Select(b => b.DocNum).ToList();
        var alreadyImported = await _monitor.GetImportedDocNosAsync(docNos, dbInterfaceType);

        var result = bills.Select(b => new ImportPreviewItemDto
        {
            DocNum          = b.DocNum,
            DocDate         = b.DocDate,
            BranchCode      = b.BranchCode,
            BranchName      = b.BranchName,
            Channel         = b.Channel,
            DocTotal        = b.DocTotal,
            AlreadyImported = alreadyImported.Contains(b.DocNum)
        }).ToList();

        var newCount = result.Count(r => !r.AlreadyImported);
        return Ok(ApiResponse<List<ImportPreviewItemDto>>.Ok(result,
            $"พบ {result.Count} รายการ ({newCount} รายการใหม่, {result.Count - newCount} นำเข้าแล้ว)"));
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

    /// <summary>DEV ONLY: accept raw AR JSON and create a PENDING log (Development environment only).</summary>
    [HttpPost("upload")]
    public async Task<ActionResult<ApiResponse<object>>> UploadRaw([FromBody] JsonElement body)
    {
        if (!_env.IsDevelopment())
            return NotFound();
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
    public string? BranchCode { get; set; }
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
