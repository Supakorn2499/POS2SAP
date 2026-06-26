using POS2SAP.API.DTOs.Sap;

namespace POS2SAP.API.Services.Interfaces;

public interface ISapDeliveryService
{
    Task<(bool Success, string? SapDocNum, string? ErrorMessage, string? RawResponse)> PostDeliveryAsync(
        SapDeliveryDto delivery);
}
