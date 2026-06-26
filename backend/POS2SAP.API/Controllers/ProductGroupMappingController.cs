using Microsoft.AspNetCore.Mvc;
using POS2SAP.API.Common;
using POS2SAP.API.DTOs.Config;
using POS2SAP.API.Services.Interfaces;
using Authorize = POS2SAP.API.Attributes.AuthorizeAttribute;

namespace POS2SAP.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProductGroupMappingController : ControllerBase
{
    private readonly IProductGroupMappingService _svc;

    public ProductGroupMappingController(IProductGroupMappingService svc)
    {
        _svc = svc;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<ProductGroupSapMappingDto>>>> GetAll()
    {
        var rows = await _svc.GetAllMappingsAsync();
        return Ok(ApiResponse<List<ProductGroupSapMappingDto>>.Ok(rows));
    }

    [HttpGet("unmapped")]
    public async Task<ActionResult<ApiResponse<List<UnmappedProductGroupDto>>>> GetUnmapped()
    {
        var rows = await _svc.GetUnmappedProductGroupsAsync();
        return Ok(ApiResponse<List<UnmappedProductGroupDto>>.Ok(rows));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<bool>>> Upsert([FromBody] UpsertProductGroupMappingDto dto)
    {
        if (dto.ProductGroupID <= 0)
            return BadRequest(ApiResponse<bool>.Fail("ProductGroupID is required"));

        if (dto.IsActive)
        {
            var sapCode = dto.SapItemGroupCode?.Trim();
            if (string.IsNullOrEmpty(sapCode) || sapCode.Equals(gbVar.SapItemGroupPending, StringComparison.OrdinalIgnoreCase))
                return BadRequest(ApiResponse<bool>.Fail($"SapItemGroupCode is required and cannot be {gbVar.SapItemGroupPending}"));
        }

        var ok = await _svc.UpsertMappingAsync(dto);
        return Ok(ApiResponse<bool>.Ok(ok, "บันทึก Product Group Mapping สำเร็จ"));
    }

    [HttpDelete("{productGroupId:int}")]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(int productGroupId)
    {
        var ok = await _svc.DeleteMappingAsync(productGroupId);
        return Ok(ApiResponse<bool>.Ok(ok));
    }
}
