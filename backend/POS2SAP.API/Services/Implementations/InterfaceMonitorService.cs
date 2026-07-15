using System.Data;
using System.Linq;
using Dapper;
using POS2SAP.API.Common;
using POS2SAP.API.DTOs.Config;
using POS2SAP.API.DTOs.Monitor;
using POS2SAP.API.Models;
using POS2SAP.API.Services.Interfaces;

namespace POS2SAP.API.Services.Implementations;

public class InterfaceMonitorService : IInterfaceMonitorService
{
    private readonly IDbConnection _db;

    public InterfaceMonitorService(IDbConnection db)
    {
        _db = db;
    }

    // ------------------------------------------------------------------ Logs

    public async Task<PagedResult<InterfaceLogDto>> GetListAsync(InterfaceLogQueryParams p)
    {
        var where = new List<string> { "is_deleted = 0" };
        var param = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(p.Search))
        {
            where.Add("(pos_doc_no LIKE @Search OR card_code LIKE @Search)");
            param.Add("Search", $"%{p.Search.Trim()}%");
        }
        if (!string.IsNullOrWhiteSpace(p.Status))
        {
            var statuses = p.Status.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                   .Select(s => s.ToUpper())
                                   .Where(s => !string.IsNullOrWhiteSpace(s))
                                   .ToArray();

            if (statuses.Length == 1)
            {
                where.Add("status = @Status");
                param.Add("Status", statuses[0]);
            }
            else if (statuses.Length > 1)
            {
                var statusParams = statuses.Select((_, index) => $"@Status{index}").ToArray();
                where.Add($"status IN ({string.Join(", ", statusParams)})");
                for (var i = 0; i < statuses.Length; i++)
                {
                    param.Add($"Status{i}", statuses[i]);
                }
            }
        }
        if (!string.IsNullOrWhiteSpace(p.BranchCode))
        {
            // UI dropdown uses PTTShopCode/shopcode; logs may store SLOC (SAP BranchCode)
            where.Add(@"(
                branch_code = @BranchCode
                OR EXISTS (
                    SELECT 1 FROM shop_data s
                    WHERE (s.shopcode = @BranchCode
                           OR ISNULL(s.PTTShopCode, '') = @BranchCode
                           OR ISNULL(s.SLOC, '') = @BranchCode)
                      AND (branch_code = s.SLOC
                           OR branch_code = s.PTTShopCode
                           OR branch_code = s.shopcode)
                )
            )");
            param.Add("BranchCode", p.BranchCode);
        }
        if (!string.IsNullOrWhiteSpace(p.InterfaceType))
        {
            // Accept UI-friendly names (ARInvoice, IncomingPayment, Delivery) and map to DB codes (AR, AP, DL)
            var iface = p.InterfaceType.Trim().ToUpper();
            string dbType = iface switch
            {
                "ARINVOICE" => "AR",
                "INCOMINGPAYMENT" => "AP",
                "DELIVERY" => "DL",
                _ => iface
            };

            where.Add("interface_type = @InterfaceType");
            param.Add("InterfaceType", dbType);
        }
        if (!string.IsNullOrWhiteSpace(p.DateFrom))
        {
            where.Add("CAST(ISNULL(pos_doc_date, created_at) AS DATE) >= @DateFrom");
            param.Add("DateFrom", p.DateFrom);
        }
        if (!string.IsNullOrWhiteSpace(p.DateTo))
        {
            where.Add("CAST(ISNULL(pos_doc_date, created_at) AS DATE) <= @DateTo");
            param.Add("DateTo", p.DateTo);
        }

        var whereClause = $"WHERE {string.Join(" AND ", where)}";

        var allowedSort = new HashSet<string> { "created_at", "pos_doc_date", "branch_code", "status", "doc_total", "sent_at" };
        var sortBy = allowedSort.Contains(p.SortBy.ToLower()) ? p.SortBy.ToLower() : "created_at";
        var sortDir = p.SortDirection.ToLower() == "asc" ? "ASC" : "DESC";

        var countSql = $"SELECT COUNT(*) FROM interface_logs {whereClause}";
        var offset = (p.Page - 1) * p.PageSize;

        // ponytail: IncludeJson only for export — keep normal list SELECT lean
        var jsonCols = p.IncludeJson
            ? @",
                   l.pos_data      AS PosData,
                   l.sap_request   AS SapRequest,
                   l.sap_response  AS SapResponse"
            : "";

        var dataSql = $@"
            SELECT l.id            AS Id,
                   l.pos_doc_no    AS PosDocNo,
                   l.pos_doc_date  AS PosDocDate,
                   l.branch_code   AS BranchCode,
                   COALESCE(CONVERT(NVARCHAR(100), sd.shopname), l.branch_name) AS BranchName,
                   l.pos_id        AS PosId,
                   l.card_code     AS CardCode,
                   COALESCE(sm.salemodename, l.channel) AS Channel,
                   l.interface_type AS InterfaceType,
                   l.doc_total     AS DocTotal,
                   l.sap_doc_num   AS SapDocNum,
                   l.status        AS Status,
                   l.error_message AS ErrorMessage,
                   l.retry_count   AS RetryCount,
                   l.sent_at       AS SentAt,
                   l.created_at    AS CreatedAt,
                   l.updated_at    AS UpdatedAt{jsonCols}
            FROM interface_logs l
            LEFT JOIN shop_data sd ON (sd.shopcode = l.branch_code
                                      OR sd.PTTShopCode = l.branch_code
                                      OR sd.SLOC = l.branch_code)
            LEFT JOIN salemode sm ON sm.SaleModeID = TRY_CAST(l.channel AS INT)
            {whereClause}
            ORDER BY {sortBy} {sortDir}
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

        param.Add("Offset", offset);
        param.Add("PageSize", p.PageSize);

        var total = await _db.ExecuteScalarAsync<int>(countSql, param);
        var rows = await _db.QueryAsync<InterfaceLogDto>(dataSql, param);

        return PagedResult<InterfaceLogDto>.Create(rows.ToList(), total, p.Page, p.PageSize);
    }

    public async Task<InterfaceLogDetailDto?> GetDetailAsync(string id)
    {
        var sql = @"
            SELECT l.id            AS Id,
                   l.pos_doc_no    AS PosDocNo,
                   l.pos_doc_date  AS PosDocDate,
                   l.branch_code   AS BranchCode,
                   COALESCE(CONVERT(NVARCHAR(100), sd.shopname), l.branch_name) AS BranchName,
                   l.pos_id        AS PosId,
                   l.card_code     AS CardCode,
                   COALESCE(sm.salemodename, l.channel) AS Channel,
                   l.interface_type AS InterfaceType,
                   l.doc_total     AS DocTotal,
                   l.sap_doc_num   AS SapDocNum,
                   l.status        AS Status,
                   l.error_message AS ErrorMessage,
                   l.retry_count   AS RetryCount,
                   l.sent_at       AS SentAt,
                   l.created_at    AS CreatedAt,
                   l.updated_at    AS UpdatedAt,
                   l.pos_data      AS PosData,
                   l.sap_request   AS SapRequest,
                   l.sap_response  AS SapResponse
            FROM interface_logs l
            LEFT JOIN shop_data sd ON (sd.shopcode = l.branch_code
                                      OR sd.PTTShopCode = l.branch_code
                                      OR sd.SLOC = l.branch_code)
            LEFT JOIN salemode sm ON sm.SaleModeID = TRY_CAST(l.channel AS INT)
            WHERE l.id = @Id AND l.is_deleted = 0";

        return await _db.QueryFirstOrDefaultAsync<InterfaceLogDetailDto>(sql, new { Id = id });
    }

    public async Task<string> InsertLogAsync(InterfaceLog log)
    {
        if (string.IsNullOrEmpty(log.Id))
            log.Id = Ulid.NewUlid().ToString();

        var sql = @"
            INSERT INTO interface_logs
                (id, pos_doc_no, pos_doc_date, branch_code, branch_name, pos_id, card_code,
                 channel, interface_type, doc_total, pos_data, sap_doc_num, sap_request, sap_response,
                 status, error_message, retry_count, sent_at, is_deleted,
                 created_at, created_by, updated_at, updated_by)
            VALUES
                (@Id, @PosDocNo, @PosDocDate, @BranchCode, @BranchName, @PosId, @CardCode,
                 @Channel, @InterfaceType, @DocTotal, @PosData, @SapDocNum, @SapRequest, @SapResponse,
                 @Status, @ErrorMessage, @RetryCount, @SentAt, @IsDeleted,
                 @CreatedAt, @CreatedBy, @UpdatedAt, @UpdatedBy)";

        await _db.ExecuteAsync(sql, log);
        return log.Id;
    }

    public async Task UpdateStatusAsync(string id, string status, string? errorMessage = null)
    {
        var sql = @"
            UPDATE interface_logs
            SET status = @Status,
                error_message = @ErrorMessage,
                updated_at = @UpdatedAt
            WHERE id = @Id";

        await _db.ExecuteAsync(sql, new { Id = id, Status = status, ErrorMessage = errorMessage, UpdatedAt = DateTime.UtcNow });
    }

    public async Task UpdateSapRequestAsync(string id, string? sapRequest)
    {
        var sql = @"
            UPDATE interface_logs
            SET sap_request = @SapRequest,
                updated_at = @UpdatedAt
            WHERE id = @Id";

        await _db.ExecuteAsync(sql, new { Id = id, SapRequest = sapRequest, UpdatedAt = DateTime.UtcNow });
    }

    public async Task UpdatePosDataAsync(string id, string? posData)
    {
        var sql = @"
            UPDATE interface_logs
            SET pos_data = @PosData,
                updated_at = @UpdatedAt
            WHERE id = @Id";

        await _db.ExecuteAsync(sql, new { Id = id, PosData = posData, UpdatedAt = DateTime.UtcNow });
    }

    public async Task UpdateSapResponseAsync(string id, string status, string? sapDocNum, string? sapResponse, string? errorMessage, bool incrementRetryCount = false)
    {
        var sql = @"
            UPDATE interface_logs
            SET status        = @Status,
                sap_doc_num   = @SapDocNum,
                sap_response  = @SapResponse,
                error_message = @ErrorMessage,
                retry_count   = CASE WHEN @IncrementRetryCount = 1 THEN retry_count + 1 ELSE retry_count END,
                sent_at       = @SentAt,
                updated_at    = @UpdatedAt
            WHERE id = @Id";

        await _db.ExecuteAsync(sql, new
        {
            Id = id,
            Status = status,
            SapDocNum = sapDocNum,
            SapResponse = sapResponse,
            ErrorMessage = errorMessage,
            IncrementRetryCount = incrementRetryCount ? 1 : 0,
            SentAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
    }

    public async Task<List<InterfaceLogDetailDto>> GetSendableLogsAsync(string interfaceType, IEnumerable<string>? docNos = null, int batchSize = 500)
    {
        batchSize = Math.Clamp(batchSize, 1, 1000);

        var where = new List<string>
        {
            "l.is_deleted = 0",
            "l.interface_type = @InterfaceType",
            "l.status IN ('PENDING', 'RETRY')"
        };
        var param = new DynamicParameters();
        param.Add("InterfaceType", interfaceType);

        var docNoList = docNos?.Where(d => !string.IsNullOrWhiteSpace(d)).Select(d => d.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (docNoList is { Count: > 0 })
        {
            where.Add("l.pos_doc_no IN @DocNos");
            param.Add("DocNos", docNoList);
        }

        param.Add("BatchSize", batchSize);

        var sql = $@"
            SELECT TOP (@BatchSize)
                   l.id            AS Id,
                   l.pos_doc_no    AS PosDocNo,
                   l.pos_doc_date  AS PosDocDate,
                   l.branch_code   AS BranchCode,
                   COALESCE(CONVERT(NVARCHAR(100), sd.shopname), l.branch_name) AS BranchName,
                   l.pos_id        AS PosId,
                   l.card_code     AS CardCode,
                   COALESCE(sm.salemodename, l.channel) AS Channel,
                   l.interface_type AS InterfaceType,
                   l.doc_total     AS DocTotal,
                   l.sap_doc_num   AS SapDocNum,
                   l.status        AS Status,
                   l.error_message AS ErrorMessage,
                   l.retry_count   AS RetryCount,
                   l.sent_at       AS SentAt,
                   l.created_at    AS CreatedAt,
                   l.updated_at    AS UpdatedAt,
                   l.pos_data      AS PosData,
                   l.sap_request   AS SapRequest,
                   l.sap_response  AS SapResponse
            FROM interface_logs l
            LEFT JOIN shop_data sd ON (sd.shopcode = l.branch_code
                                      OR sd.PTTShopCode = l.branch_code
                                      OR sd.SLOC = l.branch_code)
            LEFT JOIN salemode sm ON sm.SaleModeID = TRY_CAST(l.channel AS INT)
            WHERE {string.Join(" AND ", where)}
            ORDER BY l.created_at ASC";

        var rows = await _db.QueryAsync<InterfaceLogDetailDto>(sql, param);
        return rows.ToList();
    }

    public async Task<int> DeleteLogsByStatusAsync(IEnumerable<string>? docNos = null, IEnumerable<string>? statuses = null)
    {
        var deleteStatuses = (statuses?.ToList() is { Count: > 0 } s)
            ? s
            : new List<string> { gbVar.StatusPending, gbVar.StatusRetry };

        var docNoList = docNos?.ToList();

        if (docNoList is { Count: > 0 })
        {
            return await _db.ExecuteAsync(
                @"DELETE FROM interface_logs
                  WHERE status IN @Statuses
                    AND pos_doc_no IN @DocNos",
                new { Statuses = deleteStatuses, DocNos = docNoList });
        }

        return await _db.ExecuteAsync(
            "DELETE FROM interface_logs WHERE status IN @Statuses",
            new { Statuses = deleteStatuses });
    }

    public async Task<bool> SoftDeleteLogAsync(string id)
    {
        var sql = @"
            UPDATE interface_logs
            SET is_deleted = 1, updated_at = GETUTCDATE()
            WHERE id = @Id
              AND is_deleted = 0
              AND status IN ('PENDING', 'FAILED', 'RETRY')";
        var rows = await _db.ExecuteAsync(sql, new { Id = id });
        return rows > 0;
    }

    public async Task<int> ResetStuckProcessingAsync(int olderThanMinutes = 10)
    {
        var sql = @"
            UPDATE interface_logs
            SET status        = @Status,
                error_message = @ErrorMessage,
                updated_at    = @UpdatedAt
            WHERE status = 'PROCESSING'
              AND updated_at < DATEADD(MINUTE, @MinutesAgo, GETUTCDATE())";

        var count = await _db.ExecuteAsync(sql, new
        {
            Status       = gbVar.StatusFailed,
            ErrorMessage = "Reset from stuck PROCESSING state on service startup",
            UpdatedAt    = DateTime.UtcNow,
            MinutesAgo   = -olderThanMinutes
        });
        return count;
    }

    public async Task<HashSet<string>> GetImportedDocNosAsync(IEnumerable<string> docNos, string? interfaceType = null)
    {
        var docNoList = docNos.ToList();
        if (!docNoList.Any()) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        string sql;
        object param;
        if (!string.IsNullOrWhiteSpace(interfaceType))
        {
            sql   = "SELECT DISTINCT pos_doc_no FROM interface_logs WHERE pos_doc_no IN @DocNos AND interface_type = @InterfaceType AND is_deleted = 0";
            param = new { DocNos = docNoList, InterfaceType = interfaceType };
        }
        else
        {
            sql   = "SELECT DISTINCT pos_doc_no FROM interface_logs WHERE pos_doc_no IN @DocNos AND is_deleted = 0";
            param = new { DocNos = docNoList };
        }

        var result = await _db.QueryAsync<string>(sql, param);
        return new HashSet<string>(result, StringComparer.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------------ Dashboard

    public async Task<DashboardSummaryDto> GetDashboardAsync(int monthOffset = 0, string? interfaceType = null)
    {
        monthOffset = Math.Clamp(monthOffset, 0, 1);
        var now = DateTime.Today;
        var monthStart = new DateTime(now.Year, now.Month, 1).AddMonths(-monthOffset);
        var monthEnd = monthStart.AddMonths(1);

        var param = new DynamicParameters();
        param.Add("DateFrom", monthStart);
        param.Add("DateTo", monthEnd);

        var whereConditions = new List<string>
        {
            "l.is_deleted = 0",
            "l.created_at >= @DateFrom",
            "l.created_at < @DateTo"
        };

        if (!string.IsNullOrWhiteSpace(interfaceType))
        {
            var iface = interfaceType.Trim().ToUpper();
            string dbType = iface switch
            {
                "ARINVOICE" => "AR",
                "INCOMINGPAYMENT" => "AP",
                "DELIVERY" => "DL",
                _ => iface
            };
            whereConditions.Add("l.interface_type = @InterfaceType");
            param.Add("InterfaceType", dbType);
        }
        
        var whereClause = $"WHERE {string.Join(" AND ", whereConditions)}";

        var countSql = $@"
            SELECT
                SUM(CASE WHEN l.status='PENDING'    THEN 1 ELSE 0 END) AS Pending,
                SUM(CASE WHEN l.status='PROCESSING' THEN 1 ELSE 0 END) AS Processing,
                SUM(CASE WHEN l.status='SUCCESS'    THEN 1 ELSE 0 END) AS Success,
                SUM(CASE WHEN l.status='FAILED'     THEN 1 ELSE 0 END) AS Failed,
                SUM(CASE WHEN l.status='RETRY'      THEN 1 ELSE 0 END) AS Retry,
                COUNT(*) AS Total
            FROM interface_logs l
            {whereClause.Replace("l.is_deleted", "is_deleted").Replace("l.created_at", "created_at")}";

        var trendSql = $@"
            SELECT CONVERT(VARCHAR(10), l.created_at, 120) AS Date,
                   SUM(CASE WHEN l.status='SUCCESS' THEN 1 ELSE 0 END) AS Success,
                   SUM(CASE WHEN l.status='FAILED'  THEN 1 ELSE 0 END) AS Failed,
                   COUNT(*) AS Total
            FROM interface_logs l
            {whereClause.Replace("l.is_deleted", "is_deleted").Replace("l.created_at", "created_at")}
            GROUP BY CONVERT(VARCHAR(10), l.created_at, 120)
            ORDER BY Date";

        var branchSql = $@"
            SELECT TOP 10
                l.branch_code AS BranchCode,
                COALESCE(MAX(CONVERT(NVARCHAR(100), sd.shopname)), MAX(l.branch_name)) AS BranchName,
                COUNT(*) AS Total,
                SUM(CASE WHEN l.status='SUCCESS' THEN 1 ELSE 0 END) AS Success,
                SUM(CASE WHEN l.status='FAILED'  THEN 1 ELSE 0 END) AS Failed
            FROM interface_logs l
            LEFT JOIN shop_data sd ON (sd.shopcode = l.branch_code
                                      OR sd.PTTShopCode = l.branch_code
                                      OR sd.SLOC = l.branch_code)
            {whereClause} AND l.branch_code IS NOT NULL
            GROUP BY l.branch_code
            ORDER BY Total DESC";

        var failedBranchSql = $@"
            SELECT TOP 10
                l.branch_code AS BranchCode,
                COALESCE(MAX(CONVERT(NVARCHAR(100), sd.shopname)), MAX(l.branch_name)) AS BranchName,
                COUNT(*) AS Total,
                SUM(CASE WHEN l.status='SUCCESS' THEN 1 ELSE 0 END) AS Success,
                SUM(CASE WHEN l.status='FAILED'  THEN 1 ELSE 0 END) AS Failed
            FROM interface_logs l
            LEFT JOIN shop_data sd ON (sd.shopcode = l.branch_code
                                      OR sd.PTTShopCode = l.branch_code
                                      OR sd.SLOC = l.branch_code)
            {whereClause} AND l.status = 'FAILED' AND l.branch_code IS NOT NULL
            GROUP BY l.branch_code
            ORDER BY Failed DESC";

        var recentSql = $@"
            SELECT TOP 10
                l.id            AS Id,
                l.pos_doc_no    AS PosDocNo,
                l.pos_doc_date  AS PosDocDate,
                l.branch_code   AS BranchCode,
                COALESCE(CONVERT(NVARCHAR(100), sd.shopname), l.branch_name) AS BranchName,
                l.pos_id        AS PosId,
                l.card_code     AS CardCode,
                COALESCE(sm.salemodename, l.channel) AS Channel,
                l.doc_total     AS DocTotal,
                l.sap_doc_num   AS SapDocNum,
                l.status        AS Status,
                l.error_message AS ErrorMessage,
                l.retry_count   AS RetryCount,
                l.sent_at       AS SentAt,
                l.created_at    AS CreatedAt,
                l.updated_at    AS UpdatedAt
            FROM interface_logs l
            LEFT JOIN shop_data sd ON (sd.shopcode = l.branch_code
                                      OR sd.PTTShopCode = l.branch_code
                                      OR sd.SLOC = l.branch_code)
            LEFT JOIN salemode sm ON sm.SaleModeID = TRY_CAST(l.channel AS INT)
            {whereClause}
            ORDER BY l.created_at DESC";

        var counts = await _db.QueryFirstOrDefaultAsync<StatusCountDto>(countSql, param) ?? new();
        var trend = (await _db.QueryAsync<DailyTrendDto>(trendSql, param)).ToList();
        var branches = (await _db.QueryAsync<BranchStatDto>(branchSql, param)).ToList();
        var failedBranches = (await _db.QueryAsync<BranchStatDto>(failedBranchSql, param)).ToList();
        var recent = (await _db.QueryAsync<InterfaceLogDto>(recentSql, param)).ToList();

        return new DashboardSummaryDto
        {
            Counts = counts,
            DailyTrend = trend,
            TopBranches = branches,
            TopFailedBranches = failedBranches,
            RecentLogs = recent
        };
    }

    public async Task<int> SimulateLogStatusesAsync()
    {
        var statuses = new[] { "SUCCESS", "FAILED", "PENDING", "PROCESSING", "RETRY" };
        var random = new Random();

        var logIdsSql = "SELECT id FROM interface_logs WHERE is_deleted = 0";
        var logIds = (await _db.QueryAsync<string>(logIdsSql)).AsList();

        if (!logIds.Any())
            return 0;

        var updateSql = @"
            UPDATE interface_logs
            SET status = @Status,
                updated_at = @UpdatedAt
            WHERE id = @Id";

        var updatedCount = 0;
        foreach (var id in logIds)
        {
            var randomStatus = statuses[random.Next(statuses.Length)];
            var affectedRows = await _db.ExecuteAsync(updateSql, new
            {
                Id = id,
                Status = randomStatus,
                UpdatedAt = DateTime.UtcNow
            });
            updatedCount += affectedRows;
        }
        return updatedCount;
    }

    public async Task<List<BranchOptionDto>> GetBranchesAsync()
    {
        var sql = @"
            SELECT
                ISNULL(NULLIF(sd.PTTShopCode, ''), sd.shopcode) AS BranchCode,
                sd.shopname                                     AS BranchName
            FROM shop_data sd
            ORDER BY sd.shopname";

        return (await _db.QueryAsync<BranchOptionDto>(sql)).ToList();
    }

    // ------------------------------------------------------------------ Config

    public async Task<List<InterfaceConfigDto>> GetConfigsAsync()
    {
        var sql = @"SELECT 
            id           AS Id,
            config_key   AS ConfigKey,
            ISNULL(config_value, '') AS ConfigValue,
            description  AS Description,
            is_active    AS IsActive,
            updated_at   AS UpdatedAt
        FROM interface_configs ORDER BY config_key";
        var rows = await _db.QueryAsync<InterfaceConfigDto>(sql);
        return rows.ToList();
    }

    public async Task<InterfaceConfigDto?> GetConfigByKeyAsync(string key)
    {
        var sql = @"SELECT id AS Id, config_key AS ConfigKey, ISNULL(config_value,'') AS ConfigValue,
                           description AS Description, is_active AS IsActive, updated_at AS UpdatedAt
                    FROM interface_configs WHERE config_key = @Key";
        return await _db.QueryFirstOrDefaultAsync<InterfaceConfigDto>(sql, new { Key = key });
    }

    public async Task<bool> UpdateConfigAsync(string key, string value)
    {
        var sql = "UPDATE interface_configs SET config_value = @Value, updated_at = @UpdatedAt WHERE config_key = @Key";
        var rows = await _db.ExecuteAsync(sql, new { Key = key, Value = value, UpdatedAt = DateTime.UtcNow });
        return rows > 0;
    }

    public async Task<bool> UpsertConfigAsync(string key, string value, string? description = null, bool isActive = true)
    {
        var updateSql = "UPDATE interface_configs SET config_value = @Value, description = COALESCE(@Description, description), is_active = @IsActive, updated_at = @UpdatedAt WHERE config_key = @Key";
        var rows = await _db.ExecuteAsync(updateSql, new { Key = key, Value = value, Description = description ?? string.Empty, IsActive = isActive ? 1 : 0, UpdatedAt = DateTime.UtcNow });
        if (rows > 0) return true;

        var insertSql = @"INSERT INTO interface_configs (id, config_key, config_value, description, is_active, updated_at)
                          VALUES (LEFT(LOWER(REPLACE(NEWID(),'-','')), 26), @Key, @Value, @Description, @IsActive, @UpdatedAt)";
        var inserted = await _db.ExecuteAsync(insertSql, new { Key = key, Value = value, Description = description ?? string.Empty, IsActive = isActive ? 1 : 0, UpdatedAt = DateTime.UtcNow });
        return inserted > 0;
    }

    public async Task<Dictionary<string, string>> GetConfigDictAsync(string? interfaceType = null)
    {
        var sql = "SELECT config_key, config_value FROM interface_configs WHERE is_active = 1";
        var rows = await _db.QueryAsync<(string config_key, string config_value)>(sql);
        var all = rows.ToDictionary(r => r.config_key, r => r.config_value ?? string.Empty, StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(interfaceType))
        {
            return all;
        }

        // Build resolved dictionary: prefer interface-specific keys (e.g. ARInvoice.sap_url_test)
        var resolved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // First add global keys (no dot) and keys that don't follow interface.key pattern
        foreach (var kv in all)
        {
            if (!kv.Key.Contains('.'))
                resolved[kv.Key] = kv.Value;
        }

        // Now apply interface-specific overrides
        var prefix = interfaceType + ".";
        foreach (var kv in all)
        {
            if (kv.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var suffix = kv.Key.Substring(prefix.Length);
                // override global or add new
                resolved[suffix] = kv.Value;
            }
        }

        return resolved;
    }

    public async Task EnsureScheduleConfigAsync()
    {
        var defaults = new (string Key, string Value, string Description)[]
        {
            (gbVar.CfgScheduleWindowStart,       "20:00",        "Daily start time (HH:mm) — empty = always"),
            (gbVar.CfgScheduleWindowEnd,         "06:00",        "Daily end time (HH:mm) — overnight OK"),
            (gbVar.CfgScheduleTimezone,          "Asia/Bangkok", "IANA or Windows timezone id"),
            (gbVar.CfgScheduleMaxRuntimeMinutes, "240",          "Max continuous drain minutes per wake-up"),
            (gbVar.CfgInterfaceCutoverDate,    "2026-06-01",   "First POS doc date to interface"),
            (gbVar.CfgImportDateToMode,          "yesterday",    "Import up to: yesterday or today"),
            (gbVar.CfgImportBatchSize,           "500",          "Max docs per import/send batch"),
            (gbVar.CfgSapHttpTimeoutSeconds,     "90",           "SAP HTTP timeout per request (seconds)"),
            (gbVar.CfgImportChunkDays,           "7",            "Split import queries by N-day chunks when range is wider"),
        };

        foreach (var (key, value, description) in defaults)
        {
            var existing = await GetConfigByKeyAsync(key);
            if (existing is null)
                await UpsertConfigAsync(key, value, description);
        }
    }
}
