using POS2SAP.API.Common;
using POS2SAP.API.DTOs.Monitor;
using POS2SAP.API.DTOs.Config;
using POS2SAP.API.Models;

namespace POS2SAP.API.Services.Interfaces;

public interface IInterfaceMonitorService
{
    // --- Logs CRUD ---
    Task<PagedResult<InterfaceLogDto>> GetListAsync(InterfaceLogQueryParams p);
    Task<InterfaceLogDetailDto?> GetDetailAsync(string id);
    Task<string> InsertLogAsync(InterfaceLog log);
    Task UpdateStatusAsync(string id, string status, string? errorMessage = null);
    Task UpdateSapResponseAsync(string id, string status, string? sapDocNum, string? sapResponse, string? errorMessage);
    /// <summary>Hard-delete logs whose status is in <paramref name="statuses"/> (default: PENDING, RETRY).
    /// If <paramref name="docNos"/> is provided, only matching pos_doc_no rows are deleted.</summary>
    Task<int> DeleteLogsByStatusAsync(IEnumerable<string>? docNos = null, IEnumerable<string>? statuses = null);

    // --- Dashboard ---
    Task<DashboardSummaryDto> GetDashboardAsync(int monthOffset = 0);
    Task<int> SimulateLogStatusesAsync();
    Task<List<BranchOptionDto>> GetBranchesAsync();

    // --- Config ---
    Task<List<InterfaceConfigDto>> GetConfigsAsync();
    Task<InterfaceConfigDto?> GetConfigByKeyAsync(string key);
    Task<bool> UpdateConfigAsync(string key, string value);
    Task<Dictionary<string, string>> GetConfigDictAsync();
}
