using POS2SAP.API.DTOs.Config;

namespace POS2SAP.API.Services.Interfaces;

public interface IProductGroupMappingService
{
    /// <summary>Creates productgroup_sap_mapping if missing and seeds from POS productgroup.</summary>
    Task EnsureSchemaAsync();

    Task<List<ProductGroupSapMappingDto>> GetAllMappingsAsync();
    Task<List<UnmappedProductGroupDto>> GetUnmappedProductGroupsAsync();
    Task<bool> UpsertMappingAsync(UpsertProductGroupMappingDto dto);
    Task<bool> DeleteMappingAsync(int productGroupId);
}
