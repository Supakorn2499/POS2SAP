using POS2SAP.API.DTOs.Config;

namespace POS2SAP.API.Services.Interfaces;

public interface IDeliveryDocTypeService
{
    Task EnsureSchemaAsync();
    Task<List<DeliveryDocTypeDto>> GetDocumentTypesAsync();
    Task<List<string>> GetEnabledTypeHeadersAsync();
    Task<bool> SaveEnabledDocumentTypesAsync(IEnumerable<int> documentTypeIds);
}
