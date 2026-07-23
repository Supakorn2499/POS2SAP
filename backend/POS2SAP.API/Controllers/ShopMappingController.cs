using Microsoft.AspNetCore.Mvc;
using POS2SAP.API.Common;
using POS2SAP.API.DTOs.Config;
using POS2SAP.API.Services.Interfaces;
using Authorize = POS2SAP.API.Attributes.AuthorizeAttribute;

namespace POS2SAP.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ShopMappingController : ControllerBase
{
    private readonly IShopMappingService _svc;

    public ShopMappingController(IShopMappingService svc)
    {
        _svc = svc;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<ShopSapMappingDto>>>> GetAll()
    {
        var rows = await _svc.GetAllMappingsAsync();
        return Ok(ApiResponse<List<ShopSapMappingDto>>.Ok(rows));
    }

    [HttpGet("unmapped")]
    public async Task<ActionResult<ApiResponse<List<UnmappedShopDto>>>> GetUnmapped()
    {
        var rows = await _svc.GetUnmappedShopsAsync();
        return Ok(ApiResponse<List<UnmappedShopDto>>.Ok(rows));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<bool>>> Upsert([FromBody] UpsertShopMappingDto dto)
    {
        if (dto.ShopID <= 0)
            return BadRequest(ApiResponse<bool>.Fail("ShopID is required"));

        if (dto.IsActive)
        {
            if (string.IsNullOrWhiteSpace(dto.SapCardCode))
                return BadRequest(ApiResponse<bool>.Fail("SapCardCode is required (SAP CardCode / SLOC)"));
            if (string.IsNullOrWhiteSpace(dto.SapBranchCode))
                return BadRequest(ApiResponse<bool>.Fail("SapBranchCode is required (SAP BranchCode)"));
            if (string.IsNullOrWhiteSpace(dto.SapVatBranch))
                return BadRequest(ApiResponse<bool>.Fail("SapVatBranch is required (SAP VatBranch)"));
        }

        var ok = await _svc.UpsertMappingAsync(dto);
        return Ok(ApiResponse<bool>.Ok(ok, "บันทึก Shop Mapping สำเร็จ"));
    }

    [HttpDelete("{shopId:int}")]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(int shopId)
    {
        var ok = await _svc.DeleteMappingAsync(shopId);
        return Ok(ApiResponse<bool>.Ok(ok));
    }
}
