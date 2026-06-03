using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using POS2SAP.API.Common;
using POS2SAP.API.Services.Interfaces;
using System.Text.Json;
using System.Text.Json.Nodes;
using POS2SAP.API.DTOs.Sap;
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
        var (fetched, imported, error) = await _job.ImportPreviewAsync(request?.DocNos);
        var result = new ImportResultDto { Fetched = fetched, Imported = imported, Error = error };
        var msg = fetched == 0
            ? "ไม่พบข้อมูลใหม่จาก POS (อาจถูก import ไปแล้วทั้งหมด)"
            : $"พบ {fetched} รายการ — import สำเร็จ {imported} รายการ สถานะ PENDING";
        return Ok(ApiResponse<ImportResultDto>.Ok(result, msg));
    }

    /// <summary>Test upload: accept raw POS JSON (old or new format) and insert as PENDING log</summary>
    [HttpPost("upload")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> UploadRaw([FromBody] JsonElement body)
    {
        try
        {
            var json = body.GetRawText();
            var dto = DeserializeSapRequest(json);
            if (dto is null) return BadRequest(ApiResponse<string>.Fail("ไม่สามารถแปลง POS JSON ได้"));

            // Build canonical JSON from original request to preserve only provided fields
            using var parsed = JsonDocument.Parse(json);
            var root = parsed.RootElement;
            var arr = new JsonArray();
            var obj = new JsonObject();

            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("Head", out var headEl))
            {
                foreach (var p in headEl.EnumerateObject())
                {
                    obj[p.Name] = JsonNode.Parse(p.Value.GetRawText());
                }

                if (root.TryGetProperty("DocumentLines", out var dlines))
                {
                    obj["DocumentLines"] = JsonNode.Parse(dlines.GetRawText());
                }
                else if (root.TryGetProperty("Lines", out var lines))
                {
                    obj["DocumentLines"] = JsonNode.Parse(lines.GetRawText());
                }
            }
            else if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
            {
                var el = root[0];
                foreach (var p in el.EnumerateObject())
                {
                    if (p.NameEquals("Lines") )
                    {
                        obj["DocumentLines"] = JsonNode.Parse(p.Value.GetRawText());
                    }
                    else
                    {
                        obj[p.Name] = JsonNode.Parse(p.Value.GetRawText());
                    }
                }

                if (!obj.ContainsKey("DocumentLines") && el.TryGetProperty("DocumentLines", out var dl))
                {
                    obj["DocumentLines"] = JsonNode.Parse(dl.GetRawText());
                }
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                // Fallback: copy all top-level properties
                foreach (var p in root.EnumerateObject())
                {
                    if (p.NameEquals("Lines")) obj["DocumentLines"] = JsonNode.Parse(p.Value.GetRawText());
                    else obj[p.Name] = JsonNode.Parse(p.Value.GetRawText());
                }
            }

            arr.Add(obj);
            var posJson = arr.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
            var log = new InterfaceLog
            {
                PosDocNo = dto.Head.DocNum,
                PosDocDate = DateTime.TryParseExact(dto.Head.DocDate, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var d) ? d : null,
                BranchCode = dto.Head.BranchCode,
                BranchName = dto.Head.BranchName,
                PosId = dto.Head.POSID,
                CardCode = dto.Head.CardCode,
                Channel = dto.Head.Channel,
                DocTotal = dto.Head.DocTotal,
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

    // Duplicate of deserialize helper to support both old and new POS JSON shapes for test endpoint
    private static SapArInvoiceRequestDto? DeserializeSapRequest(string json)
    {
        try
        {
            var old = JsonSerializer.Deserialize<SapArInvoiceRequestDto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (old is not null && old.Head is not null)
                return old;
        }
        catch { }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            JsonElement el;
            if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                el = root[0];
            else if (root.ValueKind == JsonValueKind.Object)
                el = root;
            else
                return null;

            var dto = new SapArInvoiceRequestDto();
            var head = new SapArInvoiceHeadDto();

            string GetString(string name)
            {
                if (el.TryGetProperty(name, out var p) && p.ValueKind != JsonValueKind.Null)
                {
                    return p.ValueKind == JsonValueKind.String ? p.GetString() ?? string.Empty : p.GetRawText().Trim('"');
                }
                return string.Empty;
            }

            decimal? GetDecimal(string name)
            {
                if (!el.TryGetProperty(name, out var p) || p.ValueKind == JsonValueKind.Null)
                    return null;
                try
                {
                    if (p.ValueKind == JsonValueKind.Number) return p.GetDecimal();
                    var s = p.GetRawText().Trim('"');
                    if (decimal.TryParse(s, out var v)) return v;
                }
                catch { }
                return null;
            }

            head.DocNum = GetString("DocNum");
            head.DocDate = GetString("DocDate");
            head.PymntGroup = GetString("PymntGroup");
            head.DocDueDate = GetString("DocDueDate");
            head.POSID = GetString("POSID");
            head.CardCode = GetString("CardCode");
            head.CardName = GetString("CardName");
            head.CustTaxId = GetString("CustTaxId");
            head.Address = GetString("Address");
            head.CustVatBranch = GetString("CustVatBranch");
            head.CustTel = GetString("CustTel");
            head.CustMemberNo = GetString("CustMemberNo");
            head.DocCur = GetString("DocCur");
            head.BranchCode = GetString("BranchCode");
            head.BranchName = GetString("BranchName");
            head.VatBranch = GetString("VatBranch");
            head.Comments = GetString("Comments");
            head.Channel = GetString("Channel");
            head.CustBillPoint = GetDecimal("CustBillPoint");
            head.CustRedeemPoint = GetDecimal("CustRedeemPoint");
            head.CustBalancePoint = GetDecimal("CustBalancePoint");
            head.TotalAmtBefDis = GetDecimal("TotalAmtBefDis") ?? 0m;
            head.DiscPrcnt = GetDecimal("DiscPrcnt") ?? 0m;
            head.DiscSum = GetDecimal("DiscSum") ?? 0m;
            head.DownPaymentNo = GetString("DownPaymentNo");
            head.DownPaymentAmt = GetDecimal("DownPaymentAmt");
            head.VatSum = GetDecimal("VatSum") ?? 0m;
            head.DocTotal = GetDecimal("DocTotal") ?? 0m;

            dto.Head = head;

            if (el.TryGetProperty("DocumentLines", out var linesEl) || el.TryGetProperty("Lines", out linesEl))
            {
                try
                {
                    dto.Lines = JsonSerializer.Deserialize<List<SapArInvoiceLineDto>>(linesEl.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<SapArInvoiceLineDto>();
                }
                catch
                {
                    dto.Lines = new List<SapArInvoiceLineDto>();
                }
            }

            return dto;
        }
        catch
        {
            return null;
        }
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
