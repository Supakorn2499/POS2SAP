using System.Text.Json;
using System.Text.Json.Serialization;

namespace POS2SAP.API.DTOs.Sap;

/// <summary>Normalize Incoming Payment payload and always serialize as JSON array for SAP.</summary>
public static class SapIncomingPaymentJsonHelper
{
    public static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = null,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    /// <summary>Apply SAP spec defaults: empty string for unused dates/refs, 0 for unused amounts.</summary>
    public static SapIncomingPaymentDto Normalize(SapIncomingPaymentDto src)
    {
        var cashSum = src.CashSum;
        var trsfrSum = src.TrsfrSum;
        var hasCash = cashSum > 0;
        var hasTrsfr = trsfrSum > 0;

        return new SapIncomingPaymentDto
        {
            DocNum     = src.DocNum ?? string.Empty,
            DocDate    = src.DocDate ?? string.Empty,
            SettlementDate = src.SettlementDate ?? string.Empty,
            SettlementTime = src.SettlementTime ?? string.Empty,
            DocType    = string.IsNullOrWhiteSpace(src.DocType) ? "C" : src.DocType,
            CardCode   = src.CardCode ?? string.Empty,
            CardName   = src.CardName ?? string.Empty,
            POSID      = src.POSID ?? string.Empty,

            CashAcct   = hasCash ? (src.CashAcct ?? string.Empty) : string.Empty,
            CashSum    = hasCash ? cashSum : 0m,

            TrsfrAcct  = hasTrsfr ? (src.TrsfrAcct ?? string.Empty) : string.Empty,
            TrsfrSum   = hasTrsfr ? trsfrSum : 0m,
            TrsfrDate  = hasTrsfr ? (src.TrsfrDate ?? string.Empty) : string.Empty,
            TrsfrRef   = hasTrsfr ? (src.TrsfrRef ?? string.Empty) : string.Empty,

            PayNoDoc   = string.IsNullOrWhiteSpace(src.PayNoDoc) ? "N" : src.PayNoDoc,
            NoDocSum   = src.NoDocSum,
            DocCur     = string.IsNullOrWhiteSpace(src.DocCur) ? "THB" : src.DocCur,
            BranchCode = src.BranchCode ?? string.Empty,
            BranchName = src.BranchName ?? string.Empty,
            VatBranch  = src.VatBranch ?? string.Empty,
            Channel    = src.Channel ?? string.Empty,
            Comments   = src.Comments ?? string.Empty,

            PaymentInvoices = (src.PaymentInvoices ?? new List<SapPaymentInvoiceLineDto>())
                .Select((line, i) => new SapPaymentInvoiceLineDto
                {
                    DocNum     = line.DocNum ?? src.DocNum ?? string.Empty,
                    LineNum    = line.LineNum >= 0 ? line.LineNum : i,
                    InvType    = line.InvType > 0 ? line.InvType : 13,
                    InvoiceNum = line.InvoiceNum ?? string.Empty,
                    Dcount     = line.Dcount,
                    SumApplied = line.SumApplied
                }).ToList(),

            paymentCreditCards = (src.paymentCreditCards ?? new List<SapPaymentCreditCardDto>())
                .Where(cc => cc.CreditSum > 0)
                .Select((cc, i) => new SapPaymentCreditCardDto
                {
                    DocNum         = cc.DocNum ?? src.DocNum ?? string.Empty,
                    LineNum        = cc.LineNum >= 0 ? cc.LineNum : i,
                    CreditCard     = cc.CreditCard ?? string.Empty,
                    CreditAcct     = string.Empty, // SAP: never send GL on credit-card lines
                    CrCardNum      = cc.CrCardNum ?? string.Empty,
                    CardValid      = cc.CardValid ?? string.Empty,
                    CreditCardBank = cc.CreditCardBank ?? string.Empty,
                    CreditSum      = cc.CreditSum,
                    VoucherNum     = cc.VoucherNum ?? string.Empty
                }).ToList()
        };
    }

    /// <summary>SAP endpoint expects root JSON array: [{ ... }]</summary>
    public static string ToJsonArray(SapIncomingPaymentDto payment) =>
        JsonSerializer.Serialize(new[] { Normalize(payment) }, JsonOpts);

    /// <summary>Extract AR Invoice DocNum from stored AR sap_request JSON (array or single object).</summary>
    public static string? TryExtractArInvoiceDocNum(string? sapRequestJson)
    {
        if (string.IsNullOrWhiteSpace(sapRequestJson)) return null;

        try
        {
            using var doc = JsonDocument.Parse(sapRequestJson);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Array)
            {
                if (root.GetArrayLength() == 0) return null;
                root = root[0];
            }

            if (root.ValueKind != JsonValueKind.Object) return null;

            if (root.TryGetProperty("DocNum", out var dn))
                return dn.GetString();
            if (root.TryGetProperty("docNum", out var dn2))
                return dn2.GetString();
        }
        catch
        {
            // ignore malformed JSON
        }

        return null;
    }
}
