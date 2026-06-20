using Microsoft.AspNetCore.Mvc;
using POS2SAP.API.Common;
using POS2SAP.API.DTOs.Config;
using POS2SAP.API.Services.Interfaces;
using Authorize = POS2SAP.API.Attributes.AuthorizeAttribute;

namespace POS2SAP.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GlMappingController : ControllerBase
{
    private readonly IGlMappingService _svc;

    public GlMappingController(IGlMappingService svc)
    {
        _svc = svc;
    }

    /// <summary>Get all GL mappings (including live PayTypeName from POS)</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<PaytypeGlMappingDto>>>> GetAll()
    {
        var rows = await _svc.GetAllMappingsAsync();
        return Ok(ApiResponse<List<PaytypeGlMappingDto>>.Ok(rows));
    }

    /// <summary>Get POS payment types not yet in GL mapping</summary>
    [HttpGet("unmapped")]
    public async Task<ActionResult<ApiResponse<List<UnmappedPaytypeDto>>>> GetUnmapped()
    {
        var rows = await _svc.GetUnmappedPaytypesAsync();
        return Ok(ApiResponse<List<UnmappedPaytypeDto>>.Ok(rows));
    }

    /// <summary>Insert or update a GL mapping row</summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<bool>>> Upsert([FromBody] UpsertGlMappingDto dto)
    {
        if (dto.PayTypeID <= 0)
            return BadRequest(ApiResponse<bool>.Fail("PayTypeID is required"));

        var ok = await _svc.UpsertMappingAsync(dto);
        return Ok(ApiResponse<bool>.Ok(ok));
    }

    /// <summary>Delete a GL mapping row by PayTypeID</summary>
    [HttpDelete("{payTypeId:int}")]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(int payTypeId)
    {
        var ok = await _svc.DeleteMappingAsync(payTypeId);
        return Ok(ApiResponse<bool>.Ok(ok));
    }
}
