using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace POS2SAP.API.DTOs.Sap;

/// <summary>Normalize Delivery payload and serialize as single JSON object for SAP (spec example fields only).</summary>
public static class SapDeliveryJsonHelper
{
    public static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = null,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public static SapDeliveryDto Normalize(SapDeliveryDto src) =>
        new()
        {
            DocNum              = src.DocNum ?? string.Empty,
            DocDate             = src.DocDate ?? string.Empty,
            POSID               = src.POSID ?? string.Empty,
            CardCode            = src.CardCode ?? string.Empty,
            CardName            = src.CardName ?? string.Empty,
            BranchCode          = src.BranchCode ?? string.Empty,
            BranchName          = src.BranchName ?? string.Empty,
            VatBranch           = src.VatBranch ?? string.Empty,
            DeliveryReason      = src.DeliveryReason ?? string.Empty,
            DeliveryReasonOther = src.DeliveryReasonOther ?? string.Empty,
            Comments            = src.Comments ?? string.Empty,
            DocumentLines       = (src.DocumentLines ?? new List<SapDeliveryLineDto>())
                .Select((line, i) => NormalizeLine(line, src.DocNum ?? string.Empty, src.BranchCode ?? string.Empty, i))
                .ToList()
        };

    private static SapDeliveryLineDto NormalizeLine(SapDeliveryLineDto line, string headDocNum, string branchCode, int index)
    {
        var docNum   = !string.IsNullOrWhiteSpace(line.DocNum) ? line.DocNum : headDocNum;
        var qty      = FormatQuantity(line.Quantity);
        var unitName = !string.IsNullOrWhiteSpace(line.UomCode)
            ? line.UomCode
            : (line.unitMsr ?? string.Empty);
        var whsCode  = !string.IsNullOrWhiteSpace(branchCode) ? branchCode : (line.WhsCode ?? string.Empty);

        return new SapDeliveryLineDto
        {
            DocNum     = docNum,
            LineNum    = line.LineNum >= 0 ? line.LineNum : index,
            ItemCode   = line.ItemCode ?? string.Empty,
            Dscription = line.Dscription ?? string.Empty,
            FreeTxt    = line.FreeTxt ?? string.Empty,
            Quantity   = qty,
            UomCode    = unitName,
            unitMsr    = unitName,
            WhsCode    = whsCode
        };
    }

    public static string FormatQuantity(object? value)
    {
        if (value is null) return "0";
        if (value is string s && !string.IsNullOrWhiteSpace(s)) return s.Trim();
        if (decimal.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
        {
            if (d == Math.Truncate(d))
                return ((long)d).ToString(CultureInfo.InvariantCulture);
            return d.ToString(CultureInfo.InvariantCulture);
        }
        return "0";
    }

    /// <summary>SAP Delivery endpoint expects a single JSON object (not array).</summary>
    public static string ToJson(SapDeliveryDto delivery) =>
        JsonSerializer.Serialize(Normalize(delivery), JsonOpts);

    public static SapDeliveryDto? TryDeserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            var dto = JsonSerializer.Deserialize<SapDeliveryDto>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            return dto is null ? null : Normalize(dto);
        }
        catch
        {
            return null;
        }
    }
}
