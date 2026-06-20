using POS2SAP.API.DTOs.Config;

namespace POS2SAP.API.Services.Interfaces;

public interface IGlMappingService
{
    Task<List<PaytypeGlMappingDto>> GetAllMappingsAsync();
    Task<List<UnmappedPaytypeDto>>  GetUnmappedPaytypesAsync();
    Task<bool> UpsertMappingAsync(UpsertGlMappingDto dto);
    Task<bool> DeleteMappingAsync(int payTypeId);
}
