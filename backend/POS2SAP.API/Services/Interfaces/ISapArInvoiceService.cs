using POS2SAP.API.DTOs.Sap;

namespace POS2SAP.API.Services.Interfaces;

public interface ISapArInvoiceService
{
    /// <summary>ส่ง AR Invoice ไป SAP B1 — return (success, sapDocNum, errorMessage)</summary>
    Task<(bool Success, string? SapDocNum, string? ErrorMessage, string? RawResponse)> PostArInvoiceAsync(SapArInvoiceRequestDto dto);
}
