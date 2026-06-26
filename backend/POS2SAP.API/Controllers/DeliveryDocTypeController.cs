using Microsoft.AspNetCore.Mvc;
using POS2SAP.API.Common;
using POS2SAP.API.DTOs.Config;
using POS2SAP.API.Services.Interfaces;
using Authorize = POS2SAP.API.Attributes.AuthorizeAttribute;

namespace POS2SAP.API.Controllers;

[ApiController]
[Route("api/delivery-doctype")]
[Authorize]
public class DeliveryDocTypeController : ControllerBase
{
    private readonly IDeliveryDocTypeService _svc;

    public DeliveryDocTypeController(IDeliveryDocTypeService svc)
    {
        _svc = svc;
    }

    /// <summary>List POS stock-out document types with Delivery interface enabled flag.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<DeliveryDocTypeDto>>>> GetAll()
    {
        var rows = await _svc.GetDocumentTypesAsync();
        return Ok(ApiResponse<List<DeliveryDocTypeDto>>.Ok(rows));
    }

    /// <summary>Replace enabled Delivery document types (whitelist for DL import/send).</summary>
    [HttpPut]
    public async Task<ActionResult<ApiResponse<bool>>> Save([FromBody] SaveDeliveryDocTypeDto dto)
    {
        var ok = await _svc.SaveEnabledDocumentTypesAsync(dto.EnabledDocumentTypeIds);
        return Ok(ApiResponse<bool>.Ok(ok, "บันทึกประเภทเอกสาร Delivery เรียบร้อย"));
    }
}
