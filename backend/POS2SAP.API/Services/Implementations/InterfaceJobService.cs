using System.Text.Json;
using Dapper;
using POS2SAP.API.Common;
using POS2SAP.API.DTOs.Monitor;
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
                    if (ScheduleConfigHelper.IsWithinWindow(config, DateTime.UtcNow))
                    {
                        _logger.LogInformation("Scheduled cycle starting (within window)");
                        await RunScheduledCycleAsync(scope, config, stoppingToken);
                    }
                    else
                    {
                        _logger.LogDebug("Schedule enabled but outside time window — skipped");
                    }
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

    public async Task<(int Sent, int Failed)> TriggerManualAsync(IEnumerable<string>? docNos = null, string? interfaceType = null)
    {
        using var scope = _scopeFactory.CreateScope();
        return await RunBatchAsync(scope, docNos?.ToList(), interfaceType);
    }

    public async Task<(int Fetched, int Imported, string? Error)> ImportPreviewAsync(
        IEnumerable<string>? docNos = null,
        string? interfaceType = null,
        string? branchCode = null,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        bool schedulerFetch = false)
    {
        using var scope = _scopeFactory.CreateScope();
        var monitor = scope.ServiceProvider.GetRequiredService<IInterfaceMonitorService>();
        var posData = scope.ServiceProvider.GetRequiredService<IPosDataService>();

        var docNoList = docNos?.ToList();
        var config    = await monitor.GetConfigDictAsync();
        var batchSize = ScheduleConfigHelper.GetBatchSize(config);
        var chunkDays = ScheduleConfigHelper.GetImportChunkDays(config);
        var (importFrom, importTo) = dateFrom.HasValue || dateTo.HasValue
            ? ScheduleConfigHelper.ClampImportRange(config, dateFrom, dateTo)
            : ScheduleConfigHelper.ResolveImportRange(config);

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
                    : await FetchByDateChunksAsync(
                        importFrom, importTo, batchSize, chunkDays,
                        async (cf, ct, lim) => schedulerFetch
                            ? await posData.GetPendingPaymentsAsync(cf, ct, lim)
                            : await posData.GetPaymentsByFilterAsync(cf, ct, branchCode, lim));
                // Branch already filtered in SQL when applicable (see Delivery note).
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ImportPreview AP: failed to fetch POS payments");
                return (0, 0, ex.Message);
            }

            var existingAp = new Dictionary<string, (string Id, string Status)>(StringComparer.OrdinalIgnoreCase);
            try
            {
                const string apExistSql = @"
                    SELECT id, pos_doc_no, status FROM interface_logs
                    WHERE interface_type = 'AP'
                      AND status IN ('PENDING','PROCESSING','SUCCESS','RETRY')
                      AND is_deleted = 0";
                using var dbConn = new Microsoft.Data.SqlClient.SqlConnection(gbVar.MainConstr);
                await dbConn.OpenAsync();
                foreach (var row in await dbConn.QueryAsync<(string Id, string PosDocNo, string Status)>(apExistSql))
                    existingAp.TryAdd(row.PosDocNo, (row.Id, row.Status));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ImportPreview AP: could not check existing AP logs");
            }

            int apImported = 0, apSkipped = 0, apRefreshed = 0;
            string? apLastError = null;
            foreach (var pay in payments)
            {
                try
                {
                    var posJson = SapIncomingPaymentJsonHelper.ToJsonArray(pay);

                    if (existingAp.TryGetValue(pay.DocNum, out var existing))
                    {
                        if (existing.Status is gbVar.StatusSuccess or gbVar.StatusProcessing)
                        {
                            apSkipped++;
                            continue;
                        }

                        // Refresh POS JSON so new fields (e.g. SettlementDate/Time) apply on re-import.
                        await monitor.UpdatePosDataAsync(existing.Id, posJson);
                        await monitor.UpdateSapRequestAsync(existing.Id, null);
                        apRefreshed++;
                        continue;
                    }

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
                    existingAp[pay.DocNum] = (log.Id, gbVar.StatusPending);
                }
                catch (Exception ex)
                {
                    apLastError = ex.Message;
                    _logger.LogWarning(ex, "ImportPreview AP: skip {DocNum}", pay.DocNum);
                }
            }
            _logger.LogInformation("ImportPreview AP: fetched={F} skipped={S} imported={I} refreshed={R}",
                payments.Count, apSkipped, apImported, apRefreshed);
            return (payments.Count, apImported, apLastError);
        }

        // ── Delivery import path ─────────────────────────────────────────────────
        if (mappedInterfaceType == "DL")
        {
            List<SapDeliveryDto> deliveries;
            try
            {
                deliveries = docNoList?.Count > 0
                    ? await posData.GetDeliveriesByDocNosAsync(docNoList)
                    : await FetchByDateChunksAsync(
                        importFrom, importTo, batchSize, chunkDays,
                        async (cf, ct, lim) => schedulerFetch
                            ? await posData.GetPendingDeliveriesAsync(cf, ct, lim)
                            : await posData.GetDeliveriesByFilterAsync(cf, ct, branchCode, lim));
                // Branch filter already applied in SQL (shopcode / PTTShopCode / SLOC).
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ImportPreview DL: failed to fetch POS deliveries");
                return (0, 0, ex.Message);
            }

            var existingDl = new Dictionary<string, (string Id, string Status)>(StringComparer.OrdinalIgnoreCase);
            try
            {
                const string dlExistSql = @"
                    SELECT id, pos_doc_no, status FROM interface_logs
                    WHERE interface_type = 'DL'
                      AND status IN ('PENDING','PROCESSING','SUCCESS','RETRY')
                      AND is_deleted = 0";
                using var dbConn = new Microsoft.Data.SqlClient.SqlConnection(gbVar.MainConstr);
                await dbConn.OpenAsync();
                foreach (var row in await dbConn.QueryAsync<(string Id, string PosDocNo, string Status)>(dlExistSql))
                    existingDl.TryAdd(row.PosDocNo, (row.Id, row.Status));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ImportPreview DL: could not check existing DL logs");
            }

            int dlImported = 0, dlSkipped = 0, dlRefreshed = 0;
            string? dlLastError = null;
            foreach (var delivery in deliveries)
            {
                try
                {
                    var posJson = SapDeliveryJsonHelper.ToJson(delivery);

                    if (existingDl.TryGetValue(delivery.DocNum, out var existing))
                    {
                        if (existing.Status is gbVar.StatusSuccess or gbVar.StatusProcessing)
                        {
                            dlSkipped++;
                            continue;
                        }

                        await monitor.UpdatePosDataAsync(existing.Id, posJson);
                        await monitor.UpdateSapRequestAsync(existing.Id, null);
                        dlRefreshed++;
                        continue;
                    }

                    var log = new InterfaceLog
                    {
                        PosDocNo      = delivery.DocNum,
                        PosDocDate    = DateTime.TryParse(delivery.DocDate, out var dd) ? dd : null,
                        BranchCode    = delivery.BranchCode,
                        BranchName    = delivery.BranchName,
                        PosId         = delivery.POSID,
                        CardCode      = delivery.CardCode,
                        Channel       = delivery.Channel,
                        InterfaceType = "DL",
                        DocTotal      = delivery.DocumentLines.Sum(l => decimal.TryParse(l.Quantity, out var q) ? q : 0m),
                        PosData       = posJson,
                        SapRequest    = null,
                        Status        = gbVar.StatusPending,
                    };
                    await monitor.InsertLogAsync(log);
                    dlImported++;
                    existingDl[delivery.DocNum] = (log.Id, gbVar.StatusPending);
                }
                catch (Exception ex)
                {
                    dlLastError = ex.Message;
                    _logger.LogWarning(ex, "ImportPreview DL: skip {DocNum}", delivery.DocNum);
                }
            }
            _logger.LogInformation("ImportPreview DL: fetched={F} skipped={S} imported={I} refreshed={R}",
                deliveries.Count, dlSkipped, dlImported, dlRefreshed);
            return (deliveries.Count, dlImported, dlLastError);
        }

        if (mappedInterfaceType != "AR")
        {
            _logger.LogWarning("ImportPreview: unsupported interface type {Type}", mappedInterfaceType);
            return (0, 0, $"Unsupported interface type: {mappedInterfaceType}");
        }

        // ── AR Invoice import path ───────────────────────────────────────────────
        List<SapArInvoiceHeadDto> bills;
        try
        {
            bills = docNoList?.Count > 0
                ? await posData.GetBillsByDocNosAsync(docNoList)
                : await FetchByDateChunksAsync(
                    importFrom, importTo, batchSize, chunkDays,
                    async (cf, ct, lim) => schedulerFetch
                        ? await posData.GetPendingBillsAsync(cf, ct, lim)
                        : await posData.GetBillsByFilterAsync(cf, ct, branchCode, lim));
            // Branch already filtered in SQL when applicable (DTO.BranchCode is PTTShopCode; UI sends same).
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ImportPreview AR: failed to fetch POS bills");
            return (0, 0, ex.Message);
        }

        var existingDocs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            const string existSql = @"
                SELECT pos_doc_no, ISNULL(branch_code, '') AS branch_code
                FROM interface_logs
                WHERE interface_type = @InterfaceType
                  AND status IN ('PENDING','PROCESSING','SUCCESS')
                  AND is_deleted = 0";
            using var dbConn = new Microsoft.Data.SqlClient.SqlConnection(gbVar.MainConstr);
            await dbConn.OpenAsync();
            foreach (var row in await dbConn.QueryAsync<(string PosDocNo, string BranchCode)>(existSql, new { InterfaceType = mappedInterfaceType }))
            {
                existingDocs.Add(row.PosDocNo);
                // Legacy logs: bare ReceiptNumber — also index with branch
                if (!row.PosDocNo.Contains(PosDocNumHelper.Separator) && !string.IsNullOrEmpty(row.BranchCode))
                    existingDocs.Add(PosDocNumHelper.Build(row.BranchCode, row.PosDocNo));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ImportPreview AR: could not check existing logs");
        }

        int imported = 0, skipped = 0;
        string? lastError = null;
        foreach (var bill in bills)
        {
            if (existingDocs.Contains(bill.DocNum)) { skipped++; continue; }

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
                existingDocs.Add(bill.DocNum);
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

        if (string.Equals(detail.InterfaceType, "AR", StringComparison.OrdinalIgnoreCase))
            return await RetryArAsync(scope, detail);

        if (string.Equals(detail.InterfaceType, "AP", StringComparison.OrdinalIgnoreCase))
            return await RetryApAsync(scope, detail);

        if (string.Equals(detail.InterfaceType, "DL", StringComparison.OrdinalIgnoreCase))
            return await RetryDlAsync(scope, detail);

        _logger.LogWarning("RetryAsync failed for Log ID {LogId}: unsupported interface_type={Type}.", logId, detail.InterfaceType);
        return false;
    }

    private async Task<bool> RetryArAsync(IServiceScope scope, InterfaceLogDetailDto detail)
    {
        var monitor = scope.ServiceProvider.GetRequiredService<IInterfaceMonitorService>();
        var sap     = scope.ServiceProvider.GetRequiredService<ISapArInvoiceService>();

        var config   = await monitor.GetConfigDictAsync();
        var maxRetry = int.TryParse(config.GetValueOrDefault(gbVar.CfgMaxRetryCount, "3"), out var mr) ? mr : 3;

        var requestJson = !string.IsNullOrEmpty(detail.SapRequest)
            ? detail.SapRequest
            : detail.PosData;

        if (string.IsNullOrEmpty(requestJson))
        {
            _logger.LogWarning("RetryAsync AR failed for Log ID {LogId}: Both SapRequest and PosData payloads are empty.", detail.Id);
            return false;
        }

        await monitor.UpdateStatusAsync(detail.Id, gbVar.StatusProcessing);

        try
        {
            var invoices = DeserializeArRequest(requestJson, _logger);
            if (invoices is null || !invoices.Any())
            {
                _logger.LogError("RetryAsync AR failed for Log ID {LogId}: Failed to deserialize request JSON.", detail.Id);
                await monitor.UpdateSapResponseAsync(detail.Id, gbVar.StatusFailed, null, null, "Failed to deserialize request JSON for retry.", incrementRetryCount: true);
                return false;
            }

            await monitor.UpdateSapRequestAsync(detail.Id, JsonSerializer.Serialize(invoices, JsonOpts));

            var (success, sapDocNum, errorMsg, rawResponse) = await sap.PostArInvoiceAsync(invoices);

            if (success)
            {
                await monitor.UpdateSapResponseAsync(detail.Id, gbVar.StatusSuccess, sapDocNum, rawResponse, null);
                return true;
            }

            var nextRetry = detail.RetryCount + 1;
            var newStatus = nextRetry >= maxRetry ? gbVar.StatusFailed : gbVar.StatusRetry;
            await monitor.UpdateSapResponseAsync(detail.Id, newStatus, null, rawResponse, errorMsg, incrementRetryCount: true);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RetryAsync AR exception for Log ID {LogId}.", detail.Id);
            var nextRetry = detail.RetryCount + 1;
            var newStatus = nextRetry >= maxRetry ? gbVar.StatusFailed : gbVar.StatusRetry;
            await monitor.UpdateSapResponseAsync(detail.Id, newStatus, null, null, ex.Message, incrementRetryCount: true);
            return false;
        }
    }

    private async Task<bool> RetryApAsync(IServiceScope scope, InterfaceLogDetailDto detail)
    {
        var monitor = scope.ServiceProvider.GetRequiredService<IInterfaceMonitorService>();
        var posData = scope.ServiceProvider.GetRequiredService<IPosDataService>();
        var sapIp   = scope.ServiceProvider.GetRequiredService<ISapIncomingPaymentService>();

        var config   = await monitor.GetConfigDictAsync("IncomingPayment");
        var maxRetry = int.TryParse(config.GetValueOrDefault(gbVar.CfgMaxRetryCount, "3"), out var mr) ? mr : 3;

        List<SapIncomingPaymentDto> payments;
        try
        {
            payments = await posData.GetPaymentsByDocNosAsync(new[] { detail.PosDocNo });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RetryAsync AP failed to fetch POS payment for {DocNo}", detail.PosDocNo);
            return false;
        }

        var payment = payments.FirstOrDefault();
        if (payment is null)
        {
            _logger.LogWarning("RetryAsync AP failed: no POS payment data for {DocNo}", detail.PosDocNo);
            await monitor.UpdateSapResponseAsync(detail.Id, gbVar.StatusFailed, null, null,
                "No POS payment data found for retry.", incrementRetryCount: true);
            return false;
        }

        var (sent, _) = await SendIncomingPaymentLogAsync(monitor, sapIp, detail, payment, maxRetry);
        return sent > 0;
    }

    private async Task<bool> RetryDlAsync(IServiceScope scope, InterfaceLogDetailDto detail)
    {
        var monitor = scope.ServiceProvider.GetRequiredService<IInterfaceMonitorService>();
        var posData = scope.ServiceProvider.GetRequiredService<IPosDataService>();
        var sapDl   = scope.ServiceProvider.GetRequiredService<ISapDeliveryService>();

        var config   = await monitor.GetConfigDictAsync("Delivery");
        var maxRetry = int.TryParse(config.GetValueOrDefault(gbVar.CfgMaxRetryCount, "3"), out var mr) ? mr : 3;

        SapDeliveryDto? delivery = null;
        try
        {
            var fresh = await posData.GetDeliveriesByDocNosAsync(new[] { detail.PosDocNo });
            delivery = fresh.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RetryAsync DL failed to fetch POS delivery for {DocNo}", detail.PosDocNo);
        }

        if (delivery is null)
            delivery = ResolveDeliveryForLog(detail);

        if (delivery is null)
        {
            _logger.LogWarning("RetryAsync DL failed: no delivery data for {DocNo}", detail.PosDocNo);
            await monitor.UpdateSapResponseAsync(detail.Id, gbVar.StatusFailed, null, null,
                "No POS delivery data found for retry.", incrementRetryCount: true);
            return false;
        }

        var (sent, _) = await SendDeliveryLogAsync(monitor, sapDl, detail, delivery, maxRetry);
        return sent > 0;
    }

    // ------------------------------------------------------------------ Scheduled Pipeline (import → send AR → AP → DL)

    private async Task RunScheduledCycleAsync(
        IServiceScope scope,
        IReadOnlyDictionary<string, string> config,
        CancellationToken stoppingToken)
    {
        var (dateFrom, dateTo) = ScheduleConfigHelper.ResolveImportRange(config);
        var batchSize = ScheduleConfigHelper.GetBatchSize(config);
        var deadline = DateTime.UtcNow.AddMinutes(ScheduleConfigHelper.GetMaxRuntimeMinutes(config));

        _logger.LogInformation(
            "Scheduled drain started: cutover={From:yyyy-MM-dd} importTo={To:yyyy-MM-dd} batchSize={Batch}",
            dateFrom, dateTo, batchSize);

        var cycleNum = 0;
        while (DateTime.UtcNow < deadline && !stoppingToken.IsCancellationRequested)
        {
            if (!ScheduleConfigHelper.IsWithinWindow(config, DateTime.UtcNow))
            {
                _logger.LogInformation("Schedule window ended — stopping drain loop");
                break;
            }

            cycleNum++;

            var (_, arImported, _) = await ImportPreviewAsync(null, "AR", null, dateFrom, dateTo, schedulerFetch: true);
            var (arSent, arFailed) = await RunArInvoiceBatchAsync(scope, null);

            var (_, apImported, _) = await ImportPreviewAsync(null, "AP", null, dateFrom, dateTo, schedulerFetch: true);
            var (apSent, apFailed) = await RunIncomingPaymentBatchAsync(scope, null);

            var (_, dlImported, _) = await ImportPreviewAsync(null, "DL", null, dateFrom, dateTo, schedulerFetch: true);
            var (dlSent, dlFailed) = await RunDeliveryBatchAsync(scope, null);

            var workDone = arImported + apImported + dlImported + arSent + apSent + dlSent;
            _logger.LogInformation(
                "Scheduled cycle #{Cycle}: AR import={Ai} send={As}/{Af} | AP import={Pi} send={Ps}/{Pf} | DL import={Di} send={Ds}/{Df}",
                cycleNum, arImported, arSent, arFailed, apImported, apSent, apFailed, dlImported, dlSent, dlFailed);

            if (workDone == 0)
                break;
        }

        _logger.LogInformation("Scheduled drain finished after {Cycles} cycle(s)", cycleNum);
    }

    // ------------------------------------------------------------------ Core Batch

    private async Task<(int Sent, int Failed)> RunBatchAsync(IServiceScope scope, List<string>? docNos = null, string? interfaceType = null)
    {
        var mapped = MapInterfaceType(interfaceType);
        int arSent = 0, arFailed = 0, apSent = 0, apFailed = 0, dlSent = 0, dlFailed = 0;

        if (mapped is null or "AR")
            (arSent, arFailed) = await RunArInvoiceBatchAsync(scope, docNos);

        if (mapped is null or "AP")
            (apSent, apFailed) = await RunIncomingPaymentBatchAsync(scope, docNos);

        if (mapped is null or "DL")
            (dlSent, dlFailed) = await RunDeliveryBatchAsync(scope, docNos);

        _logger.LogInformation("Batch complete: AR sent={ArSent} failed={ArFailed} | AP sent={ApSent} failed={ApFailed} | DL sent={DlSent} failed={DlFailed}",
            arSent, arFailed, apSent, apFailed, dlSent, dlFailed);
        return (arSent + apSent + dlSent, arFailed + apFailed + dlFailed);
    }

    private static string? MapInterfaceType(string? interfaceType)
    {
        if (string.IsNullOrWhiteSpace(interfaceType)) return null;
        return interfaceType.Trim().ToUpper() switch
        {
            "ARINVOICE"       => "AR",
            "INCOMINGPAYMENT" => "AP",
            "DELIVERY"        => "DL",
            "AR" or "AP" or "DL" => interfaceType.Trim().ToUpper(),
            _ => null
        };
    }

    private async Task<(int Sent, int Failed)> RunArInvoiceBatchAsync(IServiceScope scope, List<string>? docNos = null)
    {
        var monitor = scope.ServiceProvider.GetRequiredService<IInterfaceMonitorService>();
        var posData = scope.ServiceProvider.GetRequiredService<IPosDataService>();
        var sap     = scope.ServiceProvider.GetRequiredService<ISapArInvoiceService>();

        var config    = await monitor.GetConfigDictAsync();
        var maxRetry    = int.TryParse(config.GetValueOrDefault(gbVar.CfgMaxRetryCount, "3"), out var mr) ? mr : 3;
        var batchSize   = int.TryParse(config.GetValueOrDefault(gbVar.CfgImportBatchSize, "500"), out var bs) && bs > 0 ? bs : 500;

        int sent = 0, failed = 0;
        var processedDocNos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // ── Step 1: Process queued logs (PENDING / RETRY) ─────────────────────
        List<InterfaceLogDetailDto> queuedLogs;
        try
        {
            queuedLogs = await monitor.GetSendableLogsAsync("AR", docNos, batchSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AR batch: failed to fetch sendable logs");
            return (0, 0);
        }

        foreach (var log in queuedLogs)
        {
            processedDocNos.Add(log.PosDocNo);
            var (s, f) = await SendArInvoiceLogAsync(monitor, sap, log, maxRetry);
            sent += s;
            failed += f;
        }

        // ── Step 2: Direct POS send for docNos not found in queue ───────────
        if (docNos is { Count: > 0 })
        {
            var remaining = docNos
                .Where(d => !string.IsNullOrWhiteSpace(d) && !processedDocNos.Contains(d.Trim()))
                .Select(d => d.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (remaining.Count > 0)
            {
                List<SapArInvoiceHeadDto> bills;
                try
                {
                    bills = await posData.GetBillsByDocNosAsync(remaining);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "AR batch: failed to fetch POS bills for direct send");
                    return (sent, failed);
                }

                foreach (var bill in bills)
                {
                    var (s, f) = await SendArInvoiceFromPosAsync(monitor, sap, bill, maxRetry);
                    sent += s;
                    failed += f;
                }
            }
        }

        _logger.LogInformation("AR batch complete: sent={Sent} failed={Failed}", sent, failed);
        return (sent, failed);
    }

    private async Task<(int Sent, int Failed)> SendArInvoiceLogAsync(
        IInterfaceMonitorService monitor,
        ISapArInvoiceService sap,
        InterfaceLogDetailDto log,
        int maxRetry)
    {
        var logId = log.Id;
        try
        {
            await monitor.UpdateStatusAsync(logId, gbVar.StatusProcessing);

            var requestJson = !string.IsNullOrEmpty(log.SapRequest) ? log.SapRequest : log.PosData;
            if (string.IsNullOrEmpty(requestJson))
            {
                await monitor.UpdateSapResponseAsync(logId, gbVar.StatusFailed, null, null,
                    "Empty pos_data/sap_request", incrementRetryCount: true);
                return (0, 1);
            }

            var invoices = DeserializeRequest(requestJson, _logger);
            if (invoices is null || !invoices.Any())
            {
                await monitor.UpdateSapResponseAsync(logId, gbVar.StatusFailed, null, null,
                    "Failed to deserialize request JSON", incrementRetryCount: true);
                return (0, 1);
            }

            var serialized = JsonSerializer.Serialize(invoices, JsonOpts);
            await monitor.UpdateSapRequestAsync(logId, serialized);

            var (success, sapDocNum, errorMsg, rawResponse) = await sap.PostArInvoiceAsync(invoices);
            if (success)
            {
                await monitor.UpdateSapResponseAsync(logId, gbVar.StatusSuccess, sapDocNum, rawResponse, null);
                return (1, 0);
            }

            var nextRetry = log.RetryCount + 1;
            var newStatus = nextRetry >= maxRetry ? gbVar.StatusFailed : gbVar.StatusRetry;
            await monitor.UpdateSapResponseAsync(logId, newStatus, null, rawResponse, errorMsg, incrementRetryCount: true);
            return (0, 1);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AR batch error processing queued log {LogId} doc={DocNo}", logId, log.PosDocNo);
            var nextRetry = log.RetryCount + 1;
            var newStatus = nextRetry >= maxRetry ? gbVar.StatusFailed : gbVar.StatusRetry;
            await monitor.UpdateSapResponseAsync(logId, newStatus, null, null, ex.Message, incrementRetryCount: true);
            return (0, 1);
        }
    }

    private async Task<(int Sent, int Failed)> SendArInvoiceFromPosAsync(
        IInterfaceMonitorService monitor,
        ISapArInvoiceService sap,
        SapArInvoiceHeadDto bill,
        int maxRetry)
    {
        var logId = string.Empty;
        try
        {
            var existingQuery = new DTOs.Monitor.InterfaceLogQueryParams
            {
                Search = bill.DocNum,
                InterfaceType = "AR",
                PageSize = 5
            };
            var existing = (await monitor.GetListAsync(existingQuery))
                .Items.FirstOrDefault(x =>
                    x.PosDocNo == bill.DocNum &&
                    string.Equals(x.InterfaceType, "AR", StringComparison.OrdinalIgnoreCase));

            if (existing?.Status == gbVar.StatusSuccess || existing?.Status == gbVar.StatusProcessing)
            {
                _logger.LogDebug("AR direct send skip {DocNum} — status={Status}", bill.DocNum, existing.Status);
                return (0, 0);
            }

            var requestJson = JsonSerializer.Serialize(new[] { bill }, JsonOpts);

            if (existing is null)
            {
                var newLog = new InterfaceLog
                {
                    PosDocNo      = bill.DocNum,
                    PosDocDate    = DateTime.TryParse(bill.DocDate, out var d) ? d : null,
                    BranchCode    = bill.BranchCode,
                    BranchName    = bill.BranchName,
                    PosId         = bill.POSID,
                    CardCode      = bill.CardCode,
                    Channel       = bill.Channel,
                    InterfaceType = "AR",
                    DocTotal      = bill.DocTotal,
                    PosData       = requestJson,
                    SapRequest    = requestJson,
                    Status        = gbVar.StatusProcessing
                };
                logId = await monitor.InsertLogAsync(newLog);
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
                return (1, 0);
            }

            var detail       = await monitor.GetDetailAsync(logId);
            var currentRetry = detail?.RetryCount ?? 0;
            var nextRetry    = currentRetry + 1;
            var newStatus    = nextRetry >= maxRetry ? gbVar.StatusFailed : gbVar.StatusRetry;
            await monitor.UpdateSapResponseAsync(logId, newStatus, null, rawResponse, errorMsg, incrementRetryCount: true);
            return (0, 1);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AR direct send error for bill {DocNum}", bill.DocNum);
            if (!string.IsNullOrEmpty(logId))
                await monitor.UpdateSapResponseAsync(logId, gbVar.StatusFailed, null, null, ex.Message, incrementRetryCount: true);
            return (0, 1);
        }
    }

    private async Task<(int Sent, int Failed)> RunIncomingPaymentBatchAsync(IServiceScope scope, List<string>? docNos = null)
    {
        var monitor = scope.ServiceProvider.GetRequiredService<IInterfaceMonitorService>();
        var posData = scope.ServiceProvider.GetRequiredService<IPosDataService>();
        var sapIp   = scope.ServiceProvider.GetRequiredService<ISapIncomingPaymentService>();

        var config    = await monitor.GetConfigDictAsync("IncomingPayment");
        var maxRetry  = int.TryParse(config.GetValueOrDefault(gbVar.CfgMaxRetryCount, "3"), out var mr) ? mr : 3;
        var batchSize = int.TryParse(config.GetValueOrDefault(gbVar.CfgImportBatchSize, "500"), out var bs) && bs > 0 ? bs : 500;

        int sent = 0, failed = 0;
        var processedDocNos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // ── Step 1: Process queued logs (PENDING / RETRY) ─────────────────────
        List<InterfaceLogDetailDto> queuedLogs;
        try
        {
            queuedLogs = await monitor.GetSendableLogsAsync("AP", docNos, batchSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AP batch: failed to fetch sendable logs");
            return (0, 0);
        }

        foreach (var log in queuedLogs)
        {
            processedDocNos.Add(log.PosDocNo);
            var payment = await ResolvePaymentForLogAsync(posData, monitor, log);
            if (payment is null)
            {
                failed++;
                continue;
            }

            var (s, f) = await SendIncomingPaymentLogAsync(monitor, sapIp, log, payment, maxRetry);
            sent += s;
            failed += f;
        }

        // ── Step 2: Direct POS send for docNos not found in queue ───────────
        if (docNos is { Count: > 0 })
        {
            var remaining = docNos
                .Where(d => !string.IsNullOrWhiteSpace(d) && !processedDocNos.Contains(d.Trim()))
                .Select(d => d.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (remaining.Count > 0)
            {
                List<SapIncomingPaymentDto> payments;
                try
                {
                    payments = await posData.GetPaymentsByDocNosAsync(remaining);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "AP batch: failed to fetch POS payments for direct send");
                    failed += remaining.Count;
                    payments = new List<SapIncomingPaymentDto>();
                }

                foreach (var payment in payments)
                {
                    if (processedDocNos.Contains(payment.DocNum))
                        continue;

                    processedDocNos.Add(payment.DocNum);
                    var (s, f) = await SendIncomingPaymentFromPosAsync(monitor, sapIp, payment, maxRetry);
                    sent += s;
                    failed += f;
                }
            }
        }

        _logger.LogInformation("AP batch complete: sent={Sent} failed={Failed}", sent, failed);
        return (sent, failed);
    }

    private async Task<SapIncomingPaymentDto?> ResolvePaymentForLogAsync(
        IPosDataService posData,
        IInterfaceMonitorService monitor,
        InterfaceLogDetailDto log)
    {
        SapIncomingPaymentDto? payment = null;

        if (!string.IsNullOrEmpty(log.SapRequest))
            payment = DeserializeIncomingPayment(log.SapRequest, _logger);

        if (payment is null && !string.IsNullOrEmpty(log.PosData))
            payment = DeserializeIncomingPayment(log.PosData, _logger);

        var invoiceNum = payment?.PaymentInvoices.FirstOrDefault()?.InvoiceNum;
        if (payment is null || string.IsNullOrWhiteSpace(invoiceNum))
        {
            try
            {
                var fresh = await posData.GetPaymentsByDocNosAsync(new[] { log.PosDocNo });
                payment = fresh.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to re-fetch POS payment for log {LogId}", log.Id);
                return null;
            }
        }

        if (payment is null)
            return null;

        payment = SapIncomingPaymentJsonHelper.Normalize(payment);

        if (!await EnsureArInvoiceReadyAsync(monitor, payment))
            return null;

        return payment;
    }

    private async Task<bool> EnsureArInvoiceReadyAsync(IInterfaceMonitorService monitor, SapIncomingPaymentDto payment)
    {
        var arLog = await GetArSuccessLogAsync(monitor, payment.DocNum);
        if (arLog is null)
        {
            _logger.LogWarning("AP skipped for {DocNo}: AR invoice not yet SUCCESS", payment.DocNum);
            return false;
        }

        var line = payment.PaymentInvoices.FirstOrDefault();
        if (line is null || string.IsNullOrWhiteSpace(line.InvoiceNum))
        {
            var arDetail = await monitor.GetDetailAsync(arLog.Id);
            var invoiceNum = SapIncomingPaymentJsonHelper.TryExtractArInvoiceDocNum(arDetail?.SapRequest);
            if (string.IsNullOrWhiteSpace(invoiceNum))
            {
                _logger.LogWarning("AP skipped for {DocNo}: cannot resolve AR InvoiceNum", payment.DocNum);
                return false;
            }

            if (line is null)
            {
                payment.PaymentInvoices = new List<SapPaymentInvoiceLineDto>
                {
                    new()
                    {
                        DocNum     = payment.DocNum,
                        LineNum    = 0,
                        InvType    = 13,
                        InvoiceNum = invoiceNum,
                        SumApplied = payment.CashSum + payment.TrsfrSum + payment.paymentCreditCards.Sum(c => c.CreditSum)
                    }
                };
            }
            else
            {
                line.InvoiceNum = invoiceNum;
            }
        }

        return true;
    }

    private static async Task<InterfaceLogDto?> GetArSuccessLogAsync(IInterfaceMonitorService monitor, string docNum)
    {
        var items = (await monitor.GetListAsync(new DTOs.Monitor.InterfaceLogQueryParams
        {
            Search = docNum,
            PageSize = 10
        })).Items;

        return items.FirstOrDefault(x =>
            x.PosDocNo == docNum &&
            string.Equals(x.InterfaceType, "AR", StringComparison.OrdinalIgnoreCase) &&
            x.Status == gbVar.StatusSuccess);
    }

    private async Task<(int Sent, int Failed)> SendIncomingPaymentLogAsync(
        IInterfaceMonitorService monitor,
        ISapIncomingPaymentService sapIp,
        InterfaceLogDetailDto log,
        SapIncomingPaymentDto payment,
        int maxRetry)
    {
        payment = SapIncomingPaymentJsonHelper.Normalize(payment);

        if (!await EnsureArInvoiceReadyAsync(monitor, payment))
            return (0, 1);

        var requestJson = SapIncomingPaymentJsonHelper.ToJsonArray(payment);

        await monitor.UpdateStatusAsync(log.Id, gbVar.StatusProcessing);
        await monitor.UpdateSapRequestAsync(log.Id, requestJson);

        try
        {
            var (success, sapDocNum, errorMsg, rawResponse) = await sapIp.PostIncomingPaymentAsync(payment);

            if (success)
            {
                await monitor.UpdateSapResponseAsync(log.Id, gbVar.StatusSuccess, sapDocNum, rawResponse, null);
                return (1, 0);
            }

            var nextRetry = log.RetryCount + 1;
            var newStatus = nextRetry >= maxRetry ? gbVar.StatusFailed : gbVar.StatusRetry;
            await monitor.UpdateSapResponseAsync(log.Id, newStatus, null, rawResponse, errorMsg, incrementRetryCount: true);
            return (0, 1);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AP send exception for log {LogId} doc {DocNo}", log.Id, payment.DocNum);
            var nextRetry = log.RetryCount + 1;
            var newStatus = nextRetry >= maxRetry ? gbVar.StatusFailed : gbVar.StatusRetry;
            await monitor.UpdateSapResponseAsync(log.Id, newStatus, null, null, ex.Message, incrementRetryCount: true);
            return (0, 1);
        }
    }

    private async Task<(int Sent, int Failed)> SendIncomingPaymentFromPosAsync(
        IInterfaceMonitorService monitor,
        ISapIncomingPaymentService sapIp,
        SapIncomingPaymentDto payment,
        int maxRetry)
    {
        payment = SapIncomingPaymentJsonHelper.Normalize(payment);

        if (!await EnsureArInvoiceReadyAsync(monitor, payment))
            return (0, 1);

        var existing = (await monitor.GetListAsync(new DTOs.Monitor.InterfaceLogQueryParams
        {
            Search = payment.DocNum,
            PageSize = 5
        })).Items.FirstOrDefault(x =>
            x.PosDocNo == payment.DocNum &&
            string.Equals(x.InterfaceType, "AP", StringComparison.OrdinalIgnoreCase));

        if (existing?.Status == gbVar.StatusSuccess || existing?.Status == gbVar.StatusProcessing)
            return (0, 0);

        var requestJson = SapIncomingPaymentJsonHelper.ToJsonArray(payment);
        string logId;

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
                DocTotal      = payment.CashSum + payment.TrsfrSum + payment.paymentCreditCards.Sum(c => c.CreditSum),
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

        try
        {
            var (success, sapDocNum, errorMsg, rawResponse) = await sapIp.PostIncomingPaymentAsync(payment);

            if (success)
            {
                await monitor.UpdateSapResponseAsync(logId, gbVar.StatusSuccess, sapDocNum, rawResponse, null);
                return (1, 0);
            }

            var detail       = await monitor.GetDetailAsync(logId);
            var currentRetry = detail?.RetryCount ?? 0;
            var nextRetry    = currentRetry + 1;
            var newStatus    = nextRetry >= maxRetry ? gbVar.StatusFailed : gbVar.StatusRetry;
            await monitor.UpdateSapResponseAsync(logId, newStatus, null, rawResponse, errorMsg, incrementRetryCount: true);
            return (0, 1);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AP send exception for doc {DocNo}", payment.DocNum);
            var detail       = await monitor.GetDetailAsync(logId);
            var currentRetry = detail?.RetryCount ?? 0;
            var nextRetry    = currentRetry + 1;
            var newStatus    = nextRetry >= maxRetry ? gbVar.StatusFailed : gbVar.StatusRetry;
            await monitor.UpdateSapResponseAsync(logId, newStatus, null, null, ex.Message, incrementRetryCount: true);
            return (0, 1);
        }
    }

    private async Task<(int Sent, int Failed)> RunDeliveryBatchAsync(IServiceScope scope, List<string>? docNos = null)
    {
        var monitor = scope.ServiceProvider.GetRequiredService<IInterfaceMonitorService>();
        var posData = scope.ServiceProvider.GetRequiredService<IPosDataService>();
        var sapDl   = scope.ServiceProvider.GetRequiredService<ISapDeliveryService>();

        var config    = await monitor.GetConfigDictAsync("Delivery");
        var maxRetry  = int.TryParse(config.GetValueOrDefault(gbVar.CfgMaxRetryCount, "3"), out var mr) ? mr : 3;
        var batchSize = int.TryParse(config.GetValueOrDefault(gbVar.CfgImportBatchSize, "500"), out var bs) && bs > 0 ? bs : 500;

        int sent = 0, failed = 0;
        var processedDocNos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        List<InterfaceLogDetailDto> queuedLogs;
        try
        {
            queuedLogs = await monitor.GetSendableLogsAsync("DL", docNos, batchSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DL batch: failed to fetch sendable logs");
            return (0, 0);
        }

        foreach (var log in queuedLogs)
        {
            processedDocNos.Add(log.PosDocNo);
            var delivery = await ResolveDeliveryForLogAsync(posData, log);
            if (delivery is null)
            {
                failed++;
                continue;
            }

            var (s, f) = await SendDeliveryLogAsync(monitor, sapDl, log, delivery, maxRetry);
            sent += s;
            failed += f;
        }

        if (docNos is { Count: > 0 })
        {
            var remaining = docNos
                .Where(d => !string.IsNullOrWhiteSpace(d) && !processedDocNos.Contains(d.Trim()))
                .Select(d => d.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (remaining.Count > 0)
            {
                List<SapDeliveryDto> deliveries;
                try
                {
                    deliveries = await posData.GetDeliveriesByDocNosAsync(remaining);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "DL batch: failed to fetch POS deliveries for direct send");
                    failed += remaining.Count;
                    deliveries = new List<SapDeliveryDto>();
                }

                foreach (var delivery in deliveries)
                {
                    if (processedDocNos.Contains(delivery.DocNum))
                        continue;

                    processedDocNos.Add(delivery.DocNum);
                    var (s, f) = await SendDeliveryFromPosAsync(monitor, sapDl, delivery, maxRetry);
                    sent += s;
                    failed += f;
                }
            }
        }

        _logger.LogInformation("DL batch complete: sent={Sent} failed={Failed}", sent, failed);
        return (sent, failed);
    }

    private async Task<SapDeliveryDto?> ResolveDeliveryForLogAsync(IPosDataService posData, InterfaceLogDetailDto log)
    {
        var stored = ResolveDeliveryForLog(log);
        if (stored is not null)
            return SapDeliveryJsonHelper.Normalize(stored);

        try
        {
            var fresh = await posData.GetDeliveriesByDocNosAsync(new[] { log.PosDocNo });
            var delivery = fresh.FirstOrDefault();
            if (delivery is not null)
                return SapDeliveryJsonHelper.Normalize(delivery);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to re-fetch POS delivery for log {LogId}", log.Id);
        }

        return null;
    }

    private static SapDeliveryDto? ResolveDeliveryForLog(InterfaceLogDetailDto log)
    {
        if (!string.IsNullOrEmpty(log.SapRequest))
        {
            var d = SapDeliveryJsonHelper.TryDeserialize(log.SapRequest);
            if (d is not null) return d;
        }

        if (!string.IsNullOrEmpty(log.PosData))
            return SapDeliveryJsonHelper.TryDeserialize(log.PosData);

        return null;
    }

    private async Task<(int Sent, int Failed)> SendDeliveryLogAsync(
        IInterfaceMonitorService monitor,
        ISapDeliveryService sapDl,
        InterfaceLogDetailDto log,
        SapDeliveryDto delivery,
        int maxRetry)
    {
        delivery = SapDeliveryJsonHelper.Normalize(delivery);
        var requestJson = SapDeliveryJsonHelper.ToJson(delivery);

        await monitor.UpdateStatusAsync(log.Id, gbVar.StatusProcessing);
        await monitor.UpdateSapRequestAsync(log.Id, requestJson);

        try
        {
            var (success, sapDocNum, errorMsg, rawResponse) = await sapDl.PostDeliveryAsync(delivery);

            if (success)
            {
                await monitor.UpdateSapResponseAsync(log.Id, gbVar.StatusSuccess, sapDocNum, rawResponse, null);
                return (1, 0);
            }

            var nextRetry = log.RetryCount + 1;
            var newStatus = nextRetry >= maxRetry ? gbVar.StatusFailed : gbVar.StatusRetry;
            await monitor.UpdateSapResponseAsync(log.Id, newStatus, sapDocNum, rawResponse, errorMsg, incrementRetryCount: true);
            return (0, 1);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DL send exception for log {LogId} doc {DocNo}", log.Id, delivery.DocNum);
            var nextRetry = log.RetryCount + 1;
            var newStatus = nextRetry >= maxRetry ? gbVar.StatusFailed : gbVar.StatusRetry;
            await monitor.UpdateSapResponseAsync(log.Id, newStatus, null, null, ex.Message, incrementRetryCount: true);
            return (0, 1);
        }
    }

    private async Task<(int Sent, int Failed)> SendDeliveryFromPosAsync(
        IInterfaceMonitorService monitor,
        ISapDeliveryService sapDl,
        SapDeliveryDto delivery,
        int maxRetry)
    {
        delivery = SapDeliveryJsonHelper.Normalize(delivery);

        var existing = (await monitor.GetListAsync(new DTOs.Monitor.InterfaceLogQueryParams
        {
            Search = delivery.DocNum,
            PageSize = 5
        })).Items.FirstOrDefault(x =>
            x.PosDocNo == delivery.DocNum &&
            string.Equals(x.InterfaceType, "DL", StringComparison.OrdinalIgnoreCase));

        if (existing?.Status == gbVar.StatusSuccess || existing?.Status == gbVar.StatusProcessing)
            return (0, 0);

        var requestJson = SapDeliveryJsonHelper.ToJson(delivery);
        string logId;

        if (existing is null)
        {
            var log = new InterfaceLog
            {
                PosDocNo      = delivery.DocNum,
                PosDocDate    = DateTime.TryParse(delivery.DocDate, out var d) ? d : null,
                BranchCode    = delivery.BranchCode,
                BranchName    = delivery.BranchName,
                PosId         = delivery.POSID,
                CardCode      = delivery.CardCode,
                Channel       = delivery.Channel,
                InterfaceType = "DL",
                DocTotal      = delivery.DocumentLines.Sum(l => decimal.TryParse(l.Quantity, out var q) ? q : 0m),
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

        try
        {
            var (success, sapDocNum, errorMsg, rawResponse) = await sapDl.PostDeliveryAsync(delivery);

            if (success)
            {
                await monitor.UpdateSapResponseAsync(logId, gbVar.StatusSuccess, sapDocNum, rawResponse, null);
                return (1, 0);
            }

            var detail       = await monitor.GetDetailAsync(logId);
            var currentRetry = detail?.RetryCount ?? 0;
            var nextRetry    = currentRetry + 1;
            var newStatus    = nextRetry >= maxRetry ? gbVar.StatusFailed : gbVar.StatusRetry;
            await monitor.UpdateSapResponseAsync(logId, newStatus, sapDocNum, rawResponse, errorMsg, incrementRetryCount: true);
            return (0, 1);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DL send exception for doc {DocNo}", delivery.DocNum);
            var detail       = await monitor.GetDetailAsync(logId);
            var currentRetry = detail?.RetryCount ?? 0;
            var nextRetry    = currentRetry + 1;
            var newStatus    = nextRetry >= maxRetry ? gbVar.StatusFailed : gbVar.StatusRetry;
            await monitor.UpdateSapResponseAsync(logId, newStatus, null, null, ex.Message, incrementRetryCount: true);
            return (0, 1);
        }
    }

    private static SapIncomingPaymentDto? DeserializeIncomingPayment(string json, ILogger logger)
    {
        try
        {
            var list = JsonSerializer.Deserialize<List<SapIncomingPaymentDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (list is { Count: > 0 })
                return SapIncomingPaymentJsonHelper.Normalize(list[0]);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "DeserializeIncomingPayment: array parse failed, trying single object");
        }

        try
        {
            var single = JsonSerializer.Deserialize<SapIncomingPaymentDto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (single is not null)
                return SapIncomingPaymentJsonHelper.Normalize(single);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "DeserializeIncomingPayment: single object parse failed");
        }

        return null;
    }

    private static List<SapArInvoiceHeadDto>? DeserializeArRequest(string json, ILogger logger) =>
        DeserializeRequest(json, logger);

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

    /// <summary>Fetch POS rows in day-sized chunks to avoid heavy single queries on wide date ranges.</summary>
    private static async Task<List<T>> FetchByDateChunksAsync<T>(
        DateTime from,
        DateTime to,
        int batchSize,
        int chunkDays,
        Func<DateTime, DateTime, int, Task<List<T>>> fetchChunk)
    {
        var spanDays = (to.Date - from.Date).Days + 1;
        if (spanDays <= chunkDays)
            return await fetchChunk(from, to, batchSize);

        var results = new List<T>();
        foreach (var (chunkFrom, chunkTo) in ScheduleConfigHelper.EnumerateDayChunks(from, to, chunkDays))
        {
            if (results.Count >= batchSize)
                break;

            var remaining = batchSize - results.Count;
            var chunk = await fetchChunk(chunkFrom, chunkTo, remaining);
            if (chunk.Count > 0)
                results.AddRange(chunk);
        }

        return results;
    }
}
