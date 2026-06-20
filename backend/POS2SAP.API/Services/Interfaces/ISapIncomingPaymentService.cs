using POS2SAP.API.DTOs.Sap;

namespace POS2SAP.API.Services.Interfaces;

public interface ISapIncomingPaymentService
{
    /// <summary>ส่ง Incoming Payment ไป SAP B1 — return (success, sapDocNum, errorMessage, rawResponse)</summary>
    Task<(bool Success, string? SapDocNum, string? ErrorMessage, string? RawResponse)> PostIncomingPaymentAsync(
        SapIncomingPaymentDto payment);
}
