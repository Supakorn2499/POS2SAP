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
    Task UpdateSapRequestAsync(string id, string? sapRequest);
    Task UpdatePosDataAsync(string id, string? posData);
    Task UpdateSapResponseAsync(string id, string status, string? sapDocNum, string? sapResponse, string? errorMessage, bool incrementRetryCount = false);

    /// <summary>Returns PENDING/RETRY logs ready to send for the given interface type (e.g. "AR").</summary>
    Task<List<InterfaceLogDetailDto>> GetSendableLogsAsync(string interfaceType, IEnumerable<string>? docNos = null, int batchSize = 500);
    /// <summary>Hard-delete logs whose status is in <paramref name="statuses"/> (default: PENDING, RETRY).
    /// If <paramref name="docNos"/> is provided, only matching pos_doc_no rows are deleted.</summary>
    Task<int> DeleteLogsByStatusAsync(IEnumerable<string>? docNos = null, IEnumerable<string>? statuses = null);

    /// <summary>Soft-delete a single log by ID. Allowed only for PENDING, FAILED, RETRY status.</summary>
    Task<bool> SoftDeleteLogAsync(string id);

    /// <summary>Reset any PROCESSING records older than <paramref name="olderThanMinutes"/> minutes to FAILED.
    /// Called on service startup to clear records stuck from a previous crash.</summary>
    Task<int> ResetStuckProcessingAsync(int olderThanMinutes = 10);

    /// <summary>Returns the set of pos_doc_no values that already exist in interface_logs
    /// for the given interface type (e.g. "AR"). Pass null to match any type.</summary>
    Task<HashSet<string>> GetImportedDocNosAsync(IEnumerable<string> docNos, string? interfaceType = null);

    // --- Dashboard ---
    Task<DashboardSummaryDto> GetDashboardAsync(int monthOffset = 0, string? interfaceType = null);
    Task<int> SimulateLogStatusesAsync();
    Task<List<BranchOptionDto>> GetBranchesAsync();

    // --- Config ---
    Task<List<InterfaceConfigDto>> GetConfigsAsync();
    Task<InterfaceConfigDto?> GetConfigByKeyAsync(string key);
    Task<bool> UpdateConfigAsync(string key, string value);
    Task<bool> UpsertConfigAsync(string key, string value, string? description = null, bool isActive = true);
    Task<Dictionary<string, string>> GetConfigDictAsync(string? interfaceType = null);

    /// <summary>Insert default schedule/cutover config rows if missing (idempotent).</summary>
    Task EnsureScheduleConfigAsync();
}
