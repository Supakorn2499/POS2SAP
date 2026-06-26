using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS2SAP.API.Common;
using POS2SAP.API.Services.Interfaces;

namespace POS2SAP.API.Controllers;

/// <summary>Development-only helpers. Returns 404 outside Development environment.</summary>
[ApiController]
[Route("api/debug")]
[Authorize]
public class DebugController : ControllerBase
{
    private readonly IInterfaceMonitorService _monitor;
    private readonly IWebHostEnvironment _env;

    public DebugController(IInterfaceMonitorService monitor, IWebHostEnvironment env)
    {
        _monitor = monitor;
        _env     = env;
    }

    [HttpPut("config/{key}")]
    public async Task<ActionResult<ApiResponse<bool>>> UpsertConfig(string key, [FromBody] object body)
    {
        if (!_env.IsDevelopment())
            return NotFound();

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
