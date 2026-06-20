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

        // Reset any records stuck in PROCESSING from a previous crash
        try
        {
            using var startupScope = _scopeFactory.CreateScope();
            var startupMonitor = startupScope.ServiceProvider.GetRequiredService<IInterfaceMonitorService>();
            var resetCount = await startupMonitor.ResetStuckProcessingAsync(olderThanMinutes: 10);
            if (resetCount > 0)
                _logger.LogWarning("Reset {Count} stuck PROCESSING record(s) to FAILED on startup", resetCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset stuck PROCESSING records on startup");
        }

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

    public async Task<(int Fetched, int Imported, string? Error)> ImportPreviewAsync(IEnumerable<string>? docNos = null, string? interfaceType = null, string? branchCode = null)
    {
        using var scope = _scopeFactory.CreateScope();
        var monitor = scope.ServiceProvider.GetRequiredService<IInterfaceMonitorService>();
        var posData = scope.ServiceProvider.GetRequiredService<IPosDataService>();

        var docNoList = docNos?.ToList();
        var config    = await monitor.GetConfigDictAsync();
        var batchSize = int.TryParse(config.GetValueOrDefault(gbVar.CfgImportBatchSize, "500"), out var bs) && bs > 0 ? bs : 500;

        // Map interface type UI names to internal DB codes
        var mappedInterfaceType = interfaceType?.Trim().ToUpper() switch
        {
            "ARINVOICE"        => "AR",
            "INCOMINGPAYMENT"  => "AP",
            "DELIVERY"         => "DL",
            _ => interfaceType ?? "AR"
        };

        // ── Incoming Payment import path ─────────────────────────────────────────
        if (mappedInterfaceType == "AP")
        {
            List<SapIncomingPaymentDto> payments;
            try
            {
                payments = docNoList?.Count > 0
                    ? await posData.GetPaymentsByDocNosAsync(docNoList)
                    : await posData.GetPaymentsByFilterAsync(
                        DateTime.Today.AddDays(-30), DateTime.Today, branchCode, batchSize);

                if (!string.IsNullOrWhiteSpace(branchCode))
                    payments = payments
                        .Where(p => string.Equals(p.BranchCode, branchCode, StringComparison.OrdinalIgnoreCase))
                        .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ImportPreview AP: failed to fetch POS payments");
                return (0, 0, ex.Message);
            }

            var alreadyApSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                const string apExistSql = @"
                    SELECT DISTINCT pos_doc_no FROM interface_logs
                    WHERE interface_type = 'AP'
                      AND status IN ('PENDING','PROCESSING','SUCCESS')
                      AND is_deleted = 0";
                using var dbConn = new Microsoft.Data.SqlClient.SqlConnection(gbVar.MainConstr);
                await dbConn.OpenAsync();
                alreadyApSet = new HashSet<string>(
                    await dbConn.QueryAsync<string>(apExistSql),
                    StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ImportPreview AP: could not check existing AP logs");
            }

            int apImported = 0, apSkipped = 0;
            string? apLastError = null;
            foreach (var pay in payments)
            {
                if (alreadyApSet.Contains(pay.DocNum)) { apSkipped++; continue; }
                try
                {
                    var posJson = JsonSerializer.Serialize(pay, JsonOpts);
                    var log = new InterfaceLog
                    {
                        PosDocNo      = pay.DocNum,
                        PosDocDate    = DateTime.TryParse(pay.DocDate, out var dd) ? dd : null,
                        BranchCode    = pay.BranchCode,
                        BranchName    = pay.BranchName,
                        PosId         = pay.POSID,
                        CardCode      = pay.CardCode,
                        Channel       = pay.Channel,
                        InterfaceType = "AP",
                        DocTotal      = pay.CashSum + pay.TrsfrSum + pay.paymentCreditCards.Sum(c => c.CreditSum),
                        PosData       = posJson,
                        SapRequest    = null,
                        Status        = gbVar.StatusPending,
                    };
                    await monitor.InsertLogAsync(log);
                    apImported++;
                    alreadyApSet.Add(pay.DocNum);
                }
                catch (Exception ex)
                {
                    apLastError = ex.Message;
                    _logger.LogWarning(ex, "ImportPreview AP: skip {DocNum}", pay.DocNum);
                }
            }
            _logger.LogInformation("ImportPreview AP: fetched={F} skipped={S} imported={I}",
                payments.Count, apSkipped, apImported);
            return (payments.Count, apImported, apLastError);
        }

        // ── AR Invoice import path (default) ─────────────────────────────────────
        List<SapArInvoiceHeadDto> bills;
        try
        {
            bills = docNoList?.Count > 0
                ? await posData.GetBillsByDocNosAsync(docNoList)
                : await posData.GetPendingBillsAsync(batchSize);

            if (!string.IsNullOrWhiteSpace(branchCode))
                bills = bills
                    .Where(b => string.Equals(b.BranchCode, branchCode, StringComparison.OrdinalIgnoreCase))
                    .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ImportPreview AR: failed to fetch POS bills");
            return (0, 0, ex.Message);
        }

        var existingDocs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            const string existSql = @"SELECT DISTINCT pos_doc_no + '|' + branch_code FROM interface_logs";
            using var dbConn = new Microsoft.Data.SqlClient.SqlConnection(gbVar.MainConstr);
            await dbConn.OpenAsync();
            existingDocs = new HashSet<string>(
                await dbConn.QueryAsync<string>(existSql),
                StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ImportPreview AR: could not check existing logs");
        }

        int imported = 0, skipped = 0;
        string? lastError = null;
        foreach (var bill in bills)
        {
            var billKey = $"{bill.DocNum}|{bill.BranchCode}";
            if (existingDocs.Contains(billKey)) { skipped++; continue; }

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
                    InterfaceType = mappedInterfaceType
                };
                await monitor.InsertLogAsync(log);
                imported++;
                existingDocs.Add(billKey);
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                _logger.LogWarning(ex, "ImportPreview AR: skip bill {DocNum}", bill.DocNum);
            }
        }

        _logger.LogInformation("ImportPreview AR: fetched={Fetched} skipped={Skipped} imported={Imported}",
            bills.Count, skipped, imported);
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

        if (detail.Status != gbVar.StatusFailed && detail.Status != gbVar.StatusRetry && detail.Status != gbVar.StatusProcessing)
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
        // Step 1: AR Invoices
        var (arSent, arFailed) = await RunArInvoiceBatchAsync(scope, docNos);

        // Step 2: Incoming Payments (only receipts whose AR Invoice already succeeded)
        var (apSent, apFailed) = await RunIncomingPaymentBatchAsync(scope, docNos);

        _logger.LogInformation("Batch complete: AR sent={ArSent} failed={ArFailed} | AP sent={ApSent} failed={ApFailed}",
            arSent, arFailed, apSent, apFailed);
        return (arSent + apSent, arFailed + apFailed);
    }

    private async Task<(int Sent, int Failed)> RunArInvoiceBatchAsync(IServiceScope scope, List<string>? docNos = null)
    {
        var monitor = scope.ServiceProvider.GetRequiredService<IInterfaceMonitorService>();
        var posData = scope.ServiceProvider.GetRequiredService<IPosDataService>();
        var sap     = scope.ServiceProvider.GetRequiredService<ISapArInvoiceService>();

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

        _logger.LogInformation("AR batch complete: sent={Sent} failed={Failed}", sent, failed);
        return (sent, failed);
    }

    private async Task<(int Sent, int Failed)> RunIncomingPaymentBatchAsync(IServiceScope scope, List<string>? docNos = null)
    {
        var monitor = scope.ServiceProvider.GetRequiredService<IInterfaceMonitorService>();
        var posData = scope.ServiceProvider.GetRequiredService<IPosDataService>();
        var sapIp   = scope.ServiceProvider.GetRequiredService<ISapIncomingPaymentService>();

        var config   = await monitor.GetConfigDictAsync("IncomingPayment");
        var maxRetry = int.TryParse(config.GetValueOrDefault(gbVar.CfgMaxRetryCount, "3"), out var mr) ? mr : 3;

        List<SapIncomingPaymentDto> payments;
        try
        {
            payments = docNos is { Count: > 0 }
                ? await posData.GetPaymentsByDocNosAsync(docNos)
                : await posData.GetPendingPaymentsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AP batch: failed to fetch POS payments");
            return (0, 0);
        }

        if (!payments.Any())
        {
            _logger.LogInformation("AP batch: no pending payments found");
            return (0, 0);
        }

        int sent = 0, failed = 0;

        foreach (var payment in payments)
        {
            var logId = string.Empty;
            try
            {
                var existingQuery = new DTOs.Monitor.InterfaceLogQueryParams
                {
                    Search = payment.DocNum, PageSize = 5
                };
                var existing = (await monitor.GetListAsync(existingQuery))
                    .Items.FirstOrDefault(x =>
                        x.PosDocNo == payment.DocNum &&
                        string.Equals(x.InterfaceType, "AP", StringComparison.OrdinalIgnoreCase));

                // Skip records that are already successfully sent or currently in flight
                if (existing?.Status == gbVar.StatusSuccess || existing?.Status == gbVar.StatusProcessing)
                {
                    _logger.LogDebug("AP batch skip {DocNum} — status={Status}", payment.DocNum, existing.Status);
                    continue;
                }

                var requestJson = JsonSerializer.Serialize(payment, JsonOpts);

                if (existing is null)
                {
                    var log = new InterfaceLog
                    {
                        PosDocNo      = payment.DocNum,
                        PosDocDate    = DateTime.TryParse(payment.DocDate, out var d) ? d : null,
                        BranchCode    = payment.BranchCode,
                        BranchName    = payment.BranchName,
                        PosId         = payment.POSID,
                        CardCode      = payment.CardCode,
                        Channel       = payment.Channel,
                        InterfaceType = "AP",
                        DocTotal      = payment.CashSum + payment.TrsfrSum +
                                        payment.paymentCreditCards.Sum(c => c.CreditSum),
                        PosData       = requestJson,
                        SapRequest    = requestJson,
                        Status        = gbVar.StatusProcessing
                    };
                    logId = await monitor.InsertLogAsync(log);
                }
                else
                {
                    logId = existing.Id;
                    await monitor.UpdateStatusAsync(logId, gbVar.StatusProcessing);
                    await monitor.UpdateSapRequestAsync(logId, requestJson);
                }

                var (success, sapDocNum, errorMsg, rawResponse) = await sapIp.PostIncomingPaymentAsync(payment);

                if (success)
                {
                    await monitor.UpdateSapResponseAsync(logId, gbVar.StatusSuccess, sapDocNum, rawResponse, null);
                    sent++;
                }
                else
                {
                    var detail       = await monitor.GetDetailAsync(logId);
                    var currentRetry = detail?.RetryCount ?? 0;
                    var newStatus    = (currentRetry + 1) >= maxRetry ? gbVar.StatusFailed : gbVar.StatusRetry;
                    await monitor.UpdateSapResponseAsync(logId, newStatus, null, rawResponse, errorMsg);
                    failed++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AP batch error processing payment {DocNum}", payment.DocNum);
                if (!string.IsNullOrEmpty(logId))
                    await monitor.UpdateSapResponseAsync(logId, gbVar.StatusFailed, null, null, ex.Message);
                failed++;
            }
        }

        _logger.LogInformation("AP batch complete: sent={Sent} failed={Failed}", sent, failed);
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
