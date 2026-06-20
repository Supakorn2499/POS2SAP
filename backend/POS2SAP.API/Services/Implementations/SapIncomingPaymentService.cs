using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using POS2SAP.API.Common;
using POS2SAP.API.DTOs.Sap;
using POS2SAP.API.Services.Interfaces;

namespace POS2SAP.API.Services.Implementations;

public class SapIncomingPaymentService : ISapIncomingPaymentService
{
    private readonly HttpClient _httpClient;
    private readonly IInterfaceMonitorService _monitor;
    private readonly ILogger<SapIncomingPaymentService> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = null,
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
    };

    public SapIncomingPaymentService(
        HttpClient httpClient,
        IInterfaceMonitorService monitor,
        ILogger<SapIncomingPaymentService> logger)
    {
        _httpClient = httpClient;
        _monitor    = monitor;
        _logger     = logger;
    }

    public async Task<(bool Success, string? SapDocNum, string? ErrorMessage, string? RawResponse)> PostIncomingPaymentAsync(
        SapIncomingPaymentDto payment)
    {
        var config   = await _monitor.GetConfigDictAsync("IncomingPayment");
        var endpoint = GetSapUrl(config);
        var docNum   = payment.DocNum;

        SetAuthHeader(config);

        // SAP expects an array
        var json    = JsonSerializer.Serialize(new[] { payment }, JsonOpts);
        _logger.LogInformation("SAP IncomingPayment request for {DocNum}: {Payload}", docNum, json);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var maxRetry = int.TryParse(config.GetValueOrDefault(gbVar.CfgMaxRetryCount, "3"), out var mr) ? mr : 3;

        for (int attempt = 1; attempt <= maxRetry; attempt++)
        {
            try
            {
                _logger.LogInformation("SAP IP call attempt {Attempt}/{Max} DocNum={DocNum} → {Endpoint}",
                    attempt, maxRetry, docNum, endpoint);

                var response = await _httpClient.PostAsync(endpoint, content);
                var raw      = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var (bodySuccess, sapDocNum, bodyErrMsg) = ParseSapResponseBody(raw);
                    if (!bodySuccess)
                    {
                        _logger.LogWarning("SAP IP HTTP 200 but Status=Failed DocNum={DocNum}: {Msg}", docNum, bodyErrMsg);
                        if (attempt < maxRetry)
                        {
                            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
                            continue;
                        }
                        return (false, null, bodyErrMsg ?? "SAP returned Status=Failed", raw);
                    }
                    _logger.LogInformation("SAP IP success DocNum={DocNum} SapDocNum={SapDocNum}", docNum, sapDocNum);
                    return (true, sapDocNum, null, raw);
                }

                _logger.LogWarning("SAP IP error attempt {Attempt} DocNum={DocNum}: {Status} {Body}",
                    attempt, docNum, response.StatusCode, raw);

                if (attempt < maxRetry)
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));

                if (attempt == maxRetry)
                    return (false, null, $"HTTP {(int)response.StatusCode}: {raw}", raw);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SAP IP exception attempt {Attempt} DocNum={DocNum}", attempt, docNum);
                if (attempt == maxRetry)
                    return (false, null, ex.Message, null);
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
            }
        }

        return (false, null, "Max retry exceeded", null);
    }

    // ------------------------------------------------------------------ Helpers

    private static string GetSapUrl(Dictionary<string, string> config)
    {
        var env = config.GetValueOrDefault(gbVar.CfgSapEnv, "TST").ToUpper();
        return env == "PRD"
            ? config.GetValueOrDefault(gbVar.CfgSapUrlProd, string.Empty)
            : config.GetValueOrDefault(gbVar.CfgSapUrlTest, string.Empty);
    }

    private void SetAuthHeader(Dictionary<string, string> config)
    {
        var authType = config.GetValueOrDefault(gbVar.CfgSapAuthType, "None");
        _httpClient.DefaultRequestHeaders.Authorization = null;
        _httpClient.DefaultRequestHeaders.Remove("X-API-Key");

        if (authType == "ApiKey")
        {
            var apiKey = config.GetValueOrDefault(gbVar.CfgSapApiKey, string.Empty);
            if (!string.IsNullOrEmpty(apiKey))
                _httpClient.DefaultRequestHeaders.Add("X-API-Key", apiKey);
        }
        else if (authType == "Basic")
        {
            var username = config.GetValueOrDefault(gbVar.CfgSapBasicUsername, string.Empty);
            var password = config.GetValueOrDefault(gbVar.CfgSapBasicPassword, string.Empty);
            if (!string.IsNullOrEmpty(username))
            {
                var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Basic", credentials);
            }
        }
    }

    private static (bool IsSuccess, string? SapDocNum, string? ErrMsg) ParseSapResponseBody(string raw)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                root = root[0];

            if (root.ValueKind != JsonValueKind.Object)
                return (true, null, null);

            string? status = null;
            if (root.TryGetProperty("Status", out var sv)) status = sv.GetString();
            else if (root.TryGetProperty("status", out var sv2)) status = sv2.GetString();

            bool isSuccess = !string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase);

            string? sapDocNum = null;
            if (root.TryGetProperty("SAPDocNum", out var sdv))
                sapDocNum = sdv.ValueKind == JsonValueKind.Number ? sdv.GetRawText() : sdv.GetString();
            else if (root.TryGetProperty("sapDocNum", out var sdv2))
                sapDocNum = sdv2.ValueKind == JsonValueKind.Number ? sdv2.GetRawText() : sdv2.GetString();

            string? errMsg = null;
            if (!isSuccess)
            {
                if (root.TryGetProperty("errMsg", out var em)) errMsg = em.GetString();
                else if (root.TryGetProperty("ErrMsg", out var em2)) errMsg = em2.GetString();
                else if (root.TryGetProperty("message", out var em3)) errMsg = em3.GetString();
            }

            return (isSuccess, sapDocNum, errMsg);
        }
        catch
        {
            return (true, null, null);
        }
    }
}
