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
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<bool>>> Update(string key, [FromBody] UpdateConfigDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.ConfigValue) && !IsNullableKey(key))
            return BadRequest(ApiResponse<bool>.Fail("กรุณาระบุ config value"));

        var upserted = await _monitor.UpsertConfigAsync(key, dto.ConfigValue ?? string.Empty);
        return upserted
            ? Ok(ApiResponse<bool>.Ok(true, "บันทึกเรียบร้อย"))
            : BadRequest(ApiResponse<bool>.Fail("บันทึกค่า config ไม่สำเร็จ"));
    }

    // Allow empty value for credentials that might be intentionally cleared
    private static bool IsNullableKey(string key)
        => key is gbVar.CfgSapApiKey or gbVar.CfgSapBasicUsername or gbVar.CfgSapBasicPassword;

    /// <summary>Test connectivity to SAP for a given interface (uses resolved config)</summary>
    [HttpPost("test")]
    public async Task<ActionResult<ApiResponse<bool>>> TestConnection([FromBody] ConfigTestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.InterfaceType))
            return BadRequest(ApiResponse<bool>.Fail("Missing interfaceType"));

        var config = await _monitor.GetConfigDictAsync(dto.InterfaceType);

        var env = config.GetValueOrDefault(gbVar.CfgSapEnv, "TST").ToUpper();
        var baseUrl = env == "PRD" ? config.GetValueOrDefault(gbVar.CfgSapUrlProd, string.Empty)
                                     : config.GetValueOrDefault(gbVar.CfgSapUrlTest, string.Empty);

        if (string.IsNullOrWhiteSpace(baseUrl))
            return BadRequest(ApiResponse<bool>.Fail("SAP URL is not configured"));

        try
        {
            using var client = new HttpClient();
            var authType = config.GetValueOrDefault(gbVar.CfgSapAuthType, "None");
            if (authType == "ApiKey")
            {
                var apiKey = config.GetValueOrDefault(gbVar.CfgSapApiKey, string.Empty);
                if (!string.IsNullOrEmpty(apiKey)) client.DefaultRequestHeaders.Add("X-API-Key", apiKey);
            }
            else if (authType == "Basic")
            {
                var username = config.GetValueOrDefault(gbVar.CfgSapBasicUsername, string.Empty);
                var password = config.GetValueOrDefault(gbVar.CfgSapBasicPassword, string.Empty);
                if (!string.IsNullOrEmpty(username))
                {
                    var credentials = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{username}:{password}"));
                    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
                }
            }

            // Try GET to base URL
            var resp = await client.GetAsync(baseUrl);
            if (resp.IsSuccessStatusCode)
                return Ok(ApiResponse<bool>.Ok(true, "Connection successful"));

            var body = await resp.Content.ReadAsStringAsync();
            return Ok(ApiResponse<bool>.Fail($"HTTP {(int)resp.StatusCode}: {body}"));
        }
        catch (Exception ex)
        {
            return Ok(ApiResponse<bool>.Fail(ex.Message));
        }
    }
}

public class ConfigTestDto { public string InterfaceType { get; set; } = string.Empty; }
