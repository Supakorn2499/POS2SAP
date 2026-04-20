using System.Text.Json;
using Dapper;
using POS2SAP.API.Common;
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

    public async Task<(int Fetched, int Imported, string? Error)> ImportPreviewAsync(IEnumerable<string>? docNos = null)
    {
        using var scope = _scopeFactory.CreateScope();
        var monitor = scope.ServiceProvider.GetRequiredService<IInterfaceMonitorService>();
        var posData = scope.ServiceProvider.GetRequiredService<IPosDataService>();

        List<DTOs.Sap.SapArInvoiceRequestDto> bills;
        try
        {
            bills = docNos?.Any() == true
                ? await posData.GetBillsByDocNosAsync(docNos)
                : await posData.GetPendingBillsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ImportPreview: failed to fetch POS bills");
            return (0, 0, ex.Message);
        }

        // Dedup: ดึง pos_doc_no ที่เคย import แล้ว (ยกเว้น FAILED) เพื่อข้ามซ้ำ
        var existingDocNos = new HashSet<string>();
        try
        {
            var existSql = @"SELECT pos_doc_no FROM interface_logs WHERE status NOT IN ('FAILED')";
            using var dbConn = new Microsoft.Data.SqlClient.SqlConnection(Common.gbVar.MainConstr);
            var existing = await dbConn.QueryAsync<string>(existSql);
            existingDocNos = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ImportPreview: could not check existing logs, will import all");
        }

        int imported = 0;
        int skipped = 0;
        string? lastError = null;
        foreach (var bill in bills)
        {
            // ข้ามบิลที่เคย import แล้ว
            if (existingDocNos.Contains(bill.Head.DocNum))
            {
                skipped++;
                continue;
            }

            try
            {
                var posJson = JsonSerializer.Serialize(bill, JsonOpts);
                var log = new InterfaceLog
                {
                    PosDocNo   = bill.Head.DocNum,
                    PosDocDate = DateTime.TryParseExact(bill.Head.DocDate, "yyyyMMdd", null,
                                     System.Globalization.DateTimeStyles.None, out var d) ? d : null,
                    BranchCode = bill.Head.BranchCode,
                    BranchName = bill.Head.BranchName,
                    PosId      = bill.Head.POSID,
                    CardCode   = bill.Head.CardCode,
                    Channel    = bill.Head.Channel,
                    DocTotal   = bill.Head.DocTotal,
                    PosData    = posJson,
                    SapRequest = null,
                    Status     = gbVar.StatusPending
                };
                await monitor.InsertLogAsync(log);
                imported++;
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                _logger.LogWarning(ex, "ImportPreview: skip bill {DocNum}", bill.Head.DocNum);
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
        if (detail is null || string.IsNullOrEmpty(detail.SapRequest))
            return false;

        // Only allow retry on FAILED or RETRY status
        if (detail.Status != gbVar.StatusFailed && detail.Status != gbVar.StatusRetry)
            return false;

        await monitor.UpdateStatusAsync(logId, gbVar.StatusProcessing);

        try
        {
            var dto = JsonSerializer.Deserialize<DTOs.Sap.SapArInvoiceRequestDto>(detail.SapRequest, JsonOpts);
            if (dto is null) return false;

            var (success, sapDocNum, errorMsg, rawResponse) = await sap.PostArInvoiceAsync(dto);

            if (success)
                await monitor.UpdateSapResponseAsync(logId, gbVar.StatusSuccess, sapDocNum, rawResponse, null);
            else
                await monitor.UpdateSapResponseAsync(logId, gbVar.StatusFailed, null, rawResponse, errorMsg);

            return success;
        }
        catch (Exception ex)
        {
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

        List<DTOs.Sap.SapArInvoiceRequestDto> bills;
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
                // Check for existing log (could be RETRY)
                var existingQuery = new DTOs.Monitor.InterfaceLogQueryParams
                {
                    Search = bill.Head.DocNum, PageSize = 1
                };
                var existing = (await monitor.GetListAsync(existingQuery)).Items.FirstOrDefault(x => x.PosDocNo == bill.Head.DocNum);

                var requestJson = JsonSerializer.Serialize(bill, JsonOpts);

                if (existing is null)
                {
                    // New record
                    var log = new InterfaceLog
                    {
                        PosDocNo     = bill.Head.DocNum,
                        PosDocDate   = DateTime.TryParseExact(bill.Head.DocDate, "yyyyMMdd", null,
                                         System.Globalization.DateTimeStyles.None, out var d) ? d : null,
                        BranchCode   = bill.Head.BranchCode,
                        BranchName   = bill.Head.BranchName,
                        PosId        = bill.Head.POSID,
                        CardCode     = bill.Head.CardCode,
                        Channel      = bill.Head.Channel,
                        InterfaceType = "AR",
                        DocTotal     = bill.Head.DocTotal,
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
                }

                var (success, sapDocNum, errorMsg, rawResponse) = await sap.PostArInvoiceAsync(bill);

                if (success)
                {
                    await monitor.UpdateSapResponseAsync(logId, gbVar.StatusSuccess, sapDocNum, rawResponse, null);
                    sent++;
                }
                else
                {
                    // Get current retry count
                    var detail = await monitor.GetDetailAsync(logId);
                    var currentRetry = detail?.RetryCount ?? 0;
                    var newStatus = (currentRetry + 1) >= maxRetry ? gbVar.StatusFailed : gbVar.StatusRetry;

                    await monitor.UpdateSapResponseAsync(logId, newStatus, null, rawResponse, errorMsg);
                    failed++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing bill {DocNum}", bill.Head.DocNum);
                if (!string.IsNullOrEmpty(logId))
                    await monitor.UpdateSapResponseAsync(logId, gbVar.StatusFailed, null, null, ex.Message);
                failed++;
            }
        }

        _logger.LogInformation("Batch complete: sent={Sent} failed={Failed}", sent, failed);
        return (sent, failed);
    }
}
