using Microsoft.AspNetCore.Mvc;
using POS2SAP.API.Common;
using POS2SAP.API.Services.Interfaces;

namespace POS2SAP.API.Controllers;

[ApiController]
[Route("api/debug")]
public class DebugController : ControllerBase
{
    private readonly IInterfaceMonitorService _monitor;

    public DebugController(IInterfaceMonitorService monitor)
    {
        _monitor = monitor;
    }

    // Temporary public endpoint for testing config upsert
    [HttpPut("config/{key}")]
    public async Task<ActionResult<ApiResponse<bool>>> UpsertConfig(string key, [FromBody] object body)
    {
        try
        {
            var dict = body as System.Text.Json.JsonElement?;
            string value = string.Empty;
            if (dict.HasValue)
            {
                if (dict.Value.TryGetProperty("configValue", out var v)) value = v.GetString() ?? string.Empty;
                else if (dict.Value.TryGetProperty("ConfigValue", out var v2)) value = v2.GetString() ?? string.Empty;
            }

            var ok = await _monitor.UpsertConfigAsync(key, value);
            return ok ? Ok(ApiResponse<bool>.Ok(true, "Upserted")) : BadRequest(ApiResponse<bool>.Fail("Failed"));
        }
        catch (Exception ex)
        {
            return ApiResponse<bool>.ServerError(ex.Message);
        }
    }
}
