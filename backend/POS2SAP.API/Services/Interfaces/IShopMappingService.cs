using POS2SAP.API.DTOs.Config;

namespace POS2SAP.API.Services.Interfaces;

public interface IShopMappingService
{
    Task EnsureSchemaAsync();
    Task<List<ShopSapMappingDto>> GetAllMappingsAsync();
    Task<List<UnmappedShopDto>> GetUnmappedShopsAsync();
    Task<bool> UpsertMappingAsync(UpsertShopMappingDto dto);
    Task<bool> DeleteMappingAsync(int shopId);
}
