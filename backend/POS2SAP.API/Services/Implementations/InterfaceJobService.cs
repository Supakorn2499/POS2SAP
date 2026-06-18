using System.Text.Json;
using Dapper;
using POS2SAP.API.Common;
using POS2SAP.API.DTOs.Sap;
using POS2SAP.API.Models;
using POS2SAP.API.Services.Interfaces;

namespace POS2SAP.API.Services.Implementations;

public class InterfaceJobService : BackgroundService, IInterfaceJobService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InterfaceJobService> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    public InterfaceJobService(IServiceScopeFactory scopeFactory, ILogger<InterfaceJobService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    // ------------------------------------------------------------------ BackgroundService

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("InterfaceJobService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var monitor = scope.ServiceProvider.GetRequiredService<IInterfaceMonitorService>();

                var config = await monitor.GetConfigDictAsync();
                var enabled = config.GetValueOrDefault(gbVar.CfgScheduleEnabled, "true").ToLower() == "true";
                var intervalMin = int.TryParse(config.GetValueOrDefault(gbVar.CfgScheduleIntervalMinutes, "5"), out var m) ? m : 5;

                if (enabled)
                {
                    _logger.LogInformation("Scheduled batch started");
                    await RunBatchAsync(scope);
                }

                await Task.Delay(TimeSpan.FromMinutes(intervalMin), stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduled batch error");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        _logger.LogInformation("InterfaceJobService stopped");
    }

    // ------------------------------------------------------------------ IInterfaceJobService

    public async Task<(int Sent, int Failed)> TriggerManualAsync(IEnumerable<string>? docNos = null)
    {
        using var scope = _scopeFactory.CreateScope();
        return await RunBatchAsync(scope, docNos?.ToList());
    }

    public async Task<(int Fetched, int Imported, string? Error)> ImportPreviewAsync(IEnumerable<string>? docNos = null, string? interfaceType = null)
    {
        using var scope = _scopeFactory.CreateScope();
        var monitor = scope.ServiceProvider.GetRequiredService<IInterfaceMonitorService>();
        var posData = scope.ServiceProvider.GetRequiredService<IPosDataService>();

        var docNoList = docNos?.ToList();
        var config = await monitor.GetConfigDictAsync();
        var batchSize = int.TryParse(config.GetValueOrDefault(gbVar.CfgImportBatchSize, "500"), out var bs) && bs > 0 ? bs : 500;

        List<SapArInvoiceHeadDto> bills;
        try
        {
            bills = docNoList?.Count > 0
                ? await posData.GetBillsByDocNosAsync(docNoList)
                : await posData.GetPendingBillsAsync(batchSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ImportPreview: failed to fetch POS bills");
            return (0, 0, ex.Message);
        }

        var existingDocs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var existSql = @"SELECT DISTINCT pos_doc_no + '|' + branch_code FROM interface_logs";
            using var dbConn = new Microsoft.Data.SqlClient.SqlConnection(gbVar.MainConstr);
            var existingKeys = await dbConn.QueryAsync<string>(existSql);
            existingDocs = new HashSet<string>(existingKeys, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ImportPreview: could not check existing logs, will import all and may create duplicates");
        }

        int imported = 0, skipped = 0;
        string? lastError = null;
        foreach (var bill in bills)
        {
            var billKey = $"{bill.DocNum}|{bill.BranchCode}";
            if (existingDocs.Contains(billKey))
            {
                skipped++;
                continue;
            }

            try
            {
                var posJson = JsonSerializer.Serialize(new[] { bill }, JsonOpts);
                var log = new InterfaceLog
                {
                    PosDocNo      = bill.DocNum,
                    PosDocDate    = DateTime.TryParse(bill.DocDate, out var d) ? d : null,
                    BranchCode    = bill.BranchCode,
                    BranchName    = bill.BranchName,
                    PosId         = bill.POSID,
                    CardCode      = bill.CardCode,
                    Channel       = bill.Channel,
                    DocTotal      = bill.DocTotal,
                    PosData       = posJson,
                    SapRequest    = null,
                    Status        = gbVar.StatusPending,
                    InterfaceType = "AR"
                };
                await monitor.InsertLogAsync(log);
                imported++;
                existingDocs.Add(billKey);
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                _logger.LogWarning(ex, "ImportPreview: skip bill {DocNum}", bill.DocNum);
            }
        }

        _logger.LogInformation("ImportPreview: fetched={Fetched} skipped={Skipped} imported={Imported}", bills.Count, skipped, imported);
        return (bills.Count, imported, lastError);
    }

    public async Task<bool> RetryAsync(string logId)
    {
        using var scope = _scopeFactory.CreateScope();
        var monitor = scope.ServiceProvider.GetRequiredService<IInterfaceMonitorService>();
        var sap     = scope.ServiceProvider.GetRequiredService<ISapArInvoiceService>();

        var detail = await monitor.GetDetailAsync(logId);
        if (detail is null)
        {
            _logger.LogWarning("RetryAsync failed: Log ID {LogId} not found.", logId);
            return false;
        }

        if (detail.Status != gbVar.StatusFailed && detail.Status != gbVar.StatusRetry)
        {
            _logger.LogWarning("RetryAsync failed for Log ID {LogId}: Status is '{Status}', not FAILED or RETRY.", logId, detail.Status);
            return false;
        }

        var requestJson = !string.IsNullOrEmpty(detail.SapRequest) 
            ? detail.SapRequest 
            : detail.PosData;

        if (string.IsNullOrEmpty(requestJson))
        {
            _logger.LogWarning("RetryAsync failed for Log ID {LogId}: Both SapRequest and PosData payloads are empty.", logId);
            return false;
        }
        
        await monitor.UpdateStatusAsync(logId, gbVar.StatusProcessing);

        try
        {
            var invoices = DeserializeRequest(requestJson, _logger);
            if (invoices is null || !invoices.Any())
            {
                _logger.LogError("RetryAsync failed for Log ID {LogId}: Failed to deserialize request JSON.", logId);
                await monitor.UpdateSapResponseAsync(logId, gbVar.StatusFailed, null, null, "Failed to deserialize request JSON for retry.");
                return false;
            }
            
            // The JSON in the DB should be the same as what we send to SAP.
            await monitor.UpdateSapRequestAsync(logId, requestJson);

            var (success, sapDocNum, errorMsg, rawResponse) = await sap.PostArInvoiceAsync(invoices);

            if (success)
                await monitor.UpdateSapResponseAsync(logId, gbVar.StatusSuccess, sapDocNum, rawResponse, null);
            else
                await monitor.UpdateSapResponseAsync(logId, gbVar.StatusFailed, null, rawResponse, errorMsg);

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RetryAsync exception for Log ID {LogId}.", logId);
            await monitor.UpdateSapResponseAsync(logId, gbVar.StatusFailed, null, null, ex.Message);
            return false;
        }
    }

    // ------------------------------------------------------------------ Core Batch

    private async Task<(int Sent, int Failed)> RunBatchAsync(IServiceScope scope, List<string>? docNos = null)
    {
        var monitor    = scope.ServiceProvider.GetRequiredService<IInterfaceMonitorService>();
        var posData    = scope.ServiceProvider.GetRequiredService<IPosDataService>();
        var sap        = scope.ServiceProvider.GetRequiredService<ISapArInvoiceService>();

        var config = await monitor.GetConfigDictAsync();
        var maxRetry = int.TryParse(config.GetValueOrDefault(gbVar.CfgMaxRetryCount, "3"), out var mr) ? mr : 3;

        List<SapArInvoiceHeadDto> bills;
        try
        {
            bills = docNos is { Count: > 0 }
                ? await posData.GetBillsByDocNosAsync(docNos)
                : await posData.GetPendingBillsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch POS bills");
            return (0, 0);
        }

        int sent = 0, failed = 0;

        foreach (var bill in bills)
        {
            var logId = string.Empty;
            try
            {
                var existingQuery = new DTOs.Monitor.InterfaceLogQueryParams { Search = bill.DocNum, PageSize = 1 };
                var existing = (await monitor.GetListAsync(existingQuery)).Items.FirstOrDefault(x => x.PosDocNo == bill.DocNum);

                var requestJson = JsonSerializer.Serialize(new[] { bill }, JsonOpts);

                if (existing is null)
                {
                    var log = new InterfaceLog
                    {
                        PosDocNo     = bill.DocNum,
                        PosDocDate   = DateTime.TryParse(bill.DocDate, out var d) ? d : null,
                        BranchCode   = bill.BranchCode,
                        BranchName   = bill.BranchName,
                        PosId        = bill.POSID,
                        CardCode     = bill.CardCode,
                        Channel      = bill.Channel,
                        InterfaceType = "AR",
                        DocTotal     = bill.DocTotal,
                        PosData      = requestJson,
                        SapRequest   = requestJson,
                        Status       = gbVar.StatusProcessing
                    };
                    logId = await monitor.InsertLogAsync(log);
                }
                else
                {
                    logId = existing.Id;
                    await monitor.UpdateStatusAsync(logId, gbVar.StatusProcessing);
                    await monitor.UpdateSapRequestAsync(logId, requestJson);
                }

                var (success, sapDocNum, errorMsg, rawResponse) = await sap.PostArInvoiceAsync(new List<SapArInvoiceHeadDto> { bill });

                if (success)
                {
                    await monitor.UpdateSapResponseAsync(logId, gbVar.StatusSuccess, sapDocNum, rawResponse, null);
                    sent++;
                }
                else
                {
                    var detail = await monitor.GetDetailAsync(logId);
                    var currentRetry = detail?.RetryCount ?? 0;
                    var newStatus = (currentRetry + 1) >= maxRetry ? gbVar.StatusFailed : gbVar.StatusRetry;

                    await monitor.UpdateSapResponseAsync(logId, newStatus, null, rawResponse, errorMsg);
                    failed++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing bill {DocNum}", bill.DocNum);
                if (!string.IsNullOrEmpty(logId))
                    await monitor.UpdateSapResponseAsync(logId, gbVar.StatusFailed, null, null, ex.Message);
                failed++;
            }
        }

        _logger.LogInformation("Batch complete: sent={Sent} failed={Failed}", sent, failed);
        return (sent, failed);
    }

    private static List<SapArInvoiceHeadDto>? DeserializeRequest(string json, ILogger logger)
    {
        try
        {
            // The standard format is now an array of heads, so try this first.
            var invoices = JsonSerializer.Deserialize<List<SapArInvoiceHeadDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (invoices is not null && invoices.Any())
            {
                return invoices;
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not deserialize as List<SapArInvoiceHeadDto>. Will try parsing as single object or old format.");
        }

        try
        {
            // Fallback for a single object {"DocNum":...} instead of [{"DocNum":...}]
            var invoice = JsonSerializer.Deserialize<SapArInvoiceHeadDto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (invoice is not null && !string.IsNullOrEmpty(invoice.DocNum))
            {
                return new List<SapArInvoiceHeadDto> { invoice };
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not deserialize request from JSON.");
            return null;
        }

        return null; // Return null if all attempts fail
    }
}
