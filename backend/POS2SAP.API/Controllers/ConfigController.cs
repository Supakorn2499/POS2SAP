using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using POS2SAP.API.Common;
using POS2SAP.API.DTOs.Config;
using POS2SAP.API.Services.Interfaces;

namespace POS2SAP.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ConfigController : ControllerBase
{
    private readonly IInterfaceMonitorService _monitor;

    public ConfigController(IInterfaceMonitorService monitor)
    {
        _monitor = monitor;
    }

    /// <summary>Get all config entries</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<InterfaceConfigDto>>>> GetAll()
    {
        var configs = await _monitor.GetConfigsAsync();
        return Ok(ApiResponse<List<InterfaceConfigDto>>.Ok(configs));
    }

    /// <summary>Get single config by key</summary>
    [HttpGet("{key}")]
    public async Task<ActionResult<ApiResponse<InterfaceConfigDto>>> GetByKey(string key)
    {
        var config = await _monitor.GetConfigByKeyAsync(key);
        if (config is null)
            return NotFound(ApiResponse<InterfaceConfigDto>.NotFound($"ไม่พบ config key={key}"));
        return Ok(ApiResponse<InterfaceConfigDto>.Ok(config));
    }

    /// <summary>Update a config value by key</summary>
    [HttpPut("{key}")]
    public async Task<ActionResult<ApiResponse<bool>>> Update(string key, [FromBody] UpdateConfigDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.ConfigValue) && !IsNullableKey(key))
            return BadRequest(ApiResponse<bool>.Fail("กรุณาระบุ config value"));

        var updated = await _monitor.UpdateConfigAsync(key, dto.ConfigValue ?? string.Empty);
        return updated
            ? Ok(ApiResponse<bool>.Ok(true, "บันทึกเรียบร้อย"))
            : NotFound(ApiResponse<bool>.NotFound($"ไม่พบ config key={key}"));
    }

    // Allow empty value for credentials that might be intentionally cleared
    private static bool IsNullableKey(string key)
        => key is gbVar.CfgSapApiKey or gbVar.CfgSapBasicUsername or gbVar.CfgSapBasicPassword;
}
