namespace POS2SAP.API.Common;

/// <summary>
/// POS receipt numbers restart per shop — DocNum must include BranchCode for uniqueness.
/// Format: {BranchCode}|{ReceiptNumber}  e.g. BFM-006|RC01072026/00001
/// BranchCode = shop_data.PTTShopCode (fallback shopcode)
/// </summary>
public static class PosDocNumHelper
{
    public const char Separator = '|';

    public static string Build(string? branchCode, string? receiptNumber)
    {
        var branch = string.IsNullOrWhiteSpace(branchCode) ? "_" : branchCode.Trim();
        var receipt = (receiptNumber ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(receipt)) return branch;
        // Already composite?
        if (receipt.Contains(Separator) && TryParse(receipt, out _, out _))
            return receipt;
        return $"{branch}{Separator}{receipt}";
    }

    public static bool TryParse(string? docNum, out string branchCode, out string receiptNumber)
    {
        branchCode = string.Empty;
        receiptNumber = string.Empty;
        if (string.IsNullOrWhiteSpace(docNum)) return false;

        var s = docNum.Trim();
        var idx = s.IndexOf(Separator);
        if (idx <= 0 || idx >= s.Length - 1)
        {
            // Legacy bare ReceiptNumber
            receiptNumber = s;
            return false;
        }

        branchCode = s[..idx].Trim();
        receiptNumber = s[(idx + 1)..].Trim();
        return !string.IsNullOrEmpty(branchCode) && !string.IsNullOrEmpty(receiptNumber);
    }

    /// <summary>True if this head matches a requested DocNum (composite or legacy receipt-only).</summary>
    public static bool Matches(string? requestedDocNum, string? branchCode, string? receiptNumber)
    {
        if (string.IsNullOrWhiteSpace(requestedDocNum)) return false;
        var receipt = (receiptNumber ?? string.Empty).Trim();
        var branch = (branchCode ?? string.Empty).Trim();

        if (TryParse(requestedDocNum, out var reqBranch, out var reqReceipt))
        {
            return string.Equals(reqReceipt, receipt, StringComparison.OrdinalIgnoreCase)
                && string.Equals(reqBranch, branch, StringComparison.OrdinalIgnoreCase);
        }

        // Legacy: DocNum is bare ReceiptNumber
        return string.Equals(requestedDocNum.Trim(), receipt, StringComparison.OrdinalIgnoreCase);
    }

    public static List<string> ExtractReceiptNumbers(IEnumerable<string> docNos)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in docNos)
        {
            if (string.IsNullOrWhiteSpace(d)) continue;
            if (TryParse(d, out _, out var receipt))
                set.Add(receipt);
            else
                set.Add(d.Trim());
        }
        return set.ToList();
    }
}
