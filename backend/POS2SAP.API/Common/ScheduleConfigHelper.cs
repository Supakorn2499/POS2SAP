namespace POS2SAP.API.Common;

/// <summary>Reads schedule / cutover settings from interface_configs.</summary>
public static class ScheduleConfigHelper
{
    public static bool IsWithinWindow(IReadOnlyDictionary<string, string> config, DateTime utcNow)
    {
        var startStr = config.GetValueOrDefault(gbVar.CfgScheduleWindowStart, "").Trim();
        if (string.IsNullOrEmpty(startStr))
            return true;

        var tz = ResolveTimeZone(config.GetValueOrDefault(gbVar.CfgScheduleTimezone, "Asia/Bangkok"));
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, tz);

        if (!TimeOnly.TryParse(startStr, out var start))
            return true;

        var localTime = TimeOnly.FromDateTime(localNow);
        var endStr = config.GetValueOrDefault(gbVar.CfgScheduleWindowEnd, "").Trim();

        if (string.IsNullOrEmpty(endStr))
            return localTime >= start;

        if (!TimeOnly.TryParse(endStr, out var end))
            return localTime >= start;

        // Same-day window e.g. 09:00–17:00
        if (start <= end)
            return localTime >= start && localTime < end;

        // Overnight window e.g. 20:00–06:00
        return localTime >= start || localTime < end;
    }

    public static DateTime GetCutoverDate(IReadOnlyDictionary<string, string> config)
    {
        var raw = config.GetValueOrDefault(gbVar.CfgInterfaceCutoverDate, "").Trim();
        if (DateTime.TryParse(raw, out var d))
            return d.Date;

        return new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    }

    public static DateTime GetImportDateTo(IReadOnlyDictionary<string, string> config)
    {
        var mode = config.GetValueOrDefault(gbVar.CfgImportDateToMode, "yesterday")
            .Trim().ToLowerInvariant();
        return mode == "today" ? DateTime.Today : DateTime.Today.AddDays(-1);
    }

    public static (DateTime From, DateTime To) ResolveImportRange(IReadOnlyDictionary<string, string> config)
    {
        var from = GetCutoverDate(config);
        var to = GetImportDateTo(config);
        if (to < from)
            to = from;
        return (from, to);
    }

    /// <summary>Clamp user-selected range so import never goes before cutover.</summary>
    public static (DateTime From, DateTime To) ClampImportRange(
        IReadOnlyDictionary<string, string> config,
        DateTime? dateFrom,
        DateTime? dateTo)
    {
        var cutover = GetCutoverDate(config);
        var from = dateFrom?.Date ?? cutover;
        var to = dateTo?.Date ?? GetImportDateTo(config);
        if (from < cutover) from = cutover;
        if (to < from) to = from;
        return (from, to);
    }

    public static int GetMaxRuntimeMinutes(IReadOnlyDictionary<string, string> config)
    {
        return int.TryParse(config.GetValueOrDefault(gbVar.CfgScheduleMaxRuntimeMinutes, "240"), out var m) && m > 0
            ? m
            : 240;
    }

    public static int GetBatchSize(IReadOnlyDictionary<string, string> config)
    {
        return int.TryParse(config.GetValueOrDefault(gbVar.CfgImportBatchSize, "500"), out var bs) && bs > 0
            ? Math.Clamp(bs, 1, 1000)
            : 500;
    }

    public static int GetImportChunkDays(IReadOnlyDictionary<string, string> config)
    {
        return int.TryParse(config.GetValueOrDefault(gbVar.CfgImportChunkDays, "7"), out var d) && d > 0
            ? Math.Clamp(d, 1, 31)
            : 7;
    }

    /// <summary>Split a date range into chunks of at most <paramref name="chunkDays"/> calendar days.</summary>
    public static IEnumerable<(DateTime From, DateTime To)> EnumerateDayChunks(
        DateTime from, DateTime to, int chunkDays)
    {
        from = from.Date;
        to   = to.Date;
        if (to < from)
            yield break;

        var spanDays = (to - from).Days + 1;
        if (spanDays <= chunkDays)
        {
            yield return (from, to);
            yield break;
        }

        var cursor = from;
        while (cursor <= to)
        {
            var chunkEnd = cursor.AddDays(chunkDays - 1);
            if (chunkEnd > to) chunkEnd = to;
            yield return (cursor, chunkEnd);
            cursor = chunkEnd.AddDays(1);
        }
    }

    private static TimeZoneInfo ResolveTimeZone(string tzId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(tzId);
        }
        catch
        {
            // Windows id for Bangkok; Linux uses Asia/Bangkok
            try { return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); }
            catch { return TimeZoneInfo.Utc; }
        }
    }
}
