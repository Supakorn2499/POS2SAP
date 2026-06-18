using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using POS2SAP.API.Common;
using POS2SAP.API.DTOs.Sap;
using POS2SAP.API.Services.Interfaces;

namespace POS2SAP.API.Services.Implementations;

public class SapArInvoiceService : ISapArInvoiceService
{
    private readonly HttpClient _httpClient;
    private readonly IInterfaceMonitorService _monitor;
    private readonly ILogger<SapArInvoiceService> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = null,
        WriteIndented = false
    };

    public SapArInvoiceService(
        HttpClient httpClient,
        IInterfaceMonitorService monitor,
        ILogger<SapArInvoiceService> logger)
    {
        _httpClient = httpClient;
        _monitor    = monitor;
        _logger     = logger;
    }

    public async Task<(bool Success, string? SapDocNum, string? ErrorMessage, string? RawResponse)> PostArInvoiceAsync(
        List<SapArInvoiceHeadDto> invoices)
    {
        var config = await _monitor.GetConfigDictAsync("ARInvoice");
        var endpoint = GetSapArInvoiceUrl(config);
        var docNumForLogging = invoices.FirstOrDefault()?.DocNum ?? "N/A";

        SetAuthHeader(config);

        var json = JsonSerializer.Serialize(invoices, JsonOpts);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        int maxRetry = int.TryParse(config.GetValueOrDefault(gbVar.CfgMaxRetryCount, "3"), out var mr) ? mr : 3;

        for (int attempt = 1; attempt <= maxRetry; attempt++)
        {
            try
            {
                _logger.LogInformation("SAP call attempt {Attempt}/{Max} for DocNum={DocNum} to {Endpoint}", attempt, maxRetry, docNumForLogging, endpoint);
                _logger.LogDebug("SAP Payload for {DocNum}: {Payload}", docNumForLogging, json);

                var response = await _httpClient.PostAsync(endpoint, content);
                var raw = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    string? sapDocNum = ExtractSapDocNum(raw);
                    _logger.LogInformation("SAP success DocNum={DocNum} SapDocNum={SapDocNum}", docNumForLogging, sapDocNum);
                    return (true, sapDocNum, null, raw);
                }

                _logger.LogWarning("SAP error attempt {Attempt} for DocNum={DocNum}: {Status} {Body}", attempt, docNumForLogging, response.StatusCode, raw);

                if (attempt < maxRetry)
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));

                if (attempt == maxRetry)
                    return (false, null, $"HTTP {(int)response.StatusCode}: {raw}", raw);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SAP call exception attempt {Attempt} DocNum={DocNum}", attempt, docNumForLogging);
                if (attempt == maxRetry)
                    return (false, null, ex.Message, null);
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
            }
        }

        return (false, null, "Max retry exceeded", null);
    }

    // ------------------------------------------------------------------ Helpers

    private static string GetSapArInvoiceUrl(Dictionary<string, string> config)
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
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            }
        }
    }

    private static string? ExtractSapDocNum(string raw)
    {
        try
        {
            // The response might be an array, a single object, or a nested object.
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            // Case 1: Response is a JSON array `[{"DocNum": "123"}]`
            if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
            {
                root = root[0];
            }

            // Case 2: Response is an object `{"DocNum": "123"}` (or the first element from array)
            if (root.TryGetProperty("DocNum", out var v)) return v.GetString();
            if (root.TryGetProperty("docNum", out var v2)) return v2.GetString();
            
            // Case 3: Response is nested `{"data": {"DocNum": "123"}}`
            if (root.TryGetProperty("data", out var data))
            {
                if (data.TryGetProperty("DocNum", out var dv)) return dv.GetString();
            }
        }
        catch(Exception e) 
        { 
            /* raw is not JSON or unexpected shape */
            Console.WriteLine($"Error parsing SAP response: {e.Message}");
        }
        return null;
    }
}
