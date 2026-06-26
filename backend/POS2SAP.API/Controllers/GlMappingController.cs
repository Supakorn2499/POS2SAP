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

        var category = (dto.SapPayCategory ?? "").Trim().ToUpperInvariant();
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "CASH", "TRANSFER", "CREDIT_CARD", "SKIP" };
        if (!allowed.Contains(category))
            return BadRequest(ApiResponse<bool>.Fail("SapPayCategory must be CASH, TRANSFER, CREDIT_CARD, or SKIP"));

        dto.SapPayCategory = category;

        if (category != "SKIP")
        {
            var gl = dto.SapGlAccount?.Trim();
            if (string.IsNullOrEmpty(gl) || gl.Equals("[GL-PENDING]", StringComparison.OrdinalIgnoreCase))
                return BadRequest(ApiResponse<bool>.Fail("SapGlAccount is required and cannot be [GL-PENDING]"));

            if (category == "CREDIT_CARD" && string.IsNullOrWhiteSpace(dto.SapPayTypeName))
                return BadRequest(ApiResponse<bool>.Fail("SapPayTypeName is required for CREDIT_CARD category"));
        }

        var ok = await _svc.UpsertMappingAsync(dto);
        return Ok(ApiResponse<bool>.Ok(ok, "บันทึก GL Mapping สำเร็จ"));
    }

    /// <summary>Delete a GL mapping row by PayTypeID</summary>
    [HttpDelete("{payTypeId:int}")]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(int payTypeId)
    {
        var ok = await _svc.DeleteMappingAsync(payTypeId);
        return Ok(ApiResponse<bool>.Ok(ok));
    }
}
