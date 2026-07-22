using POS2SAP.API.Common;

namespace POS2SAP.Tests.Common;

public class ScheduleConfigHelperTests
{
    private static Dictionary<string, string> Config(params (string k, string v)[] pairs)
        => pairs.ToDictionary(p => p.k, p => p.v);

    [Fact]
    public void IsWithinWindow_EmptyStart_AlwaysTrue()
    {
        var cfg = Config((gbVar.CfgScheduleWindowStart, ""));
        Assert.True(ScheduleConfigHelper.IsWithinWindow(cfg, DateTime.UtcNow));
    }

    [Fact]
    public void IsWithinWindow_Overnight_IncludesLateNightAndEarlyMorning()
    {
        // 20:00–06:00 Bangkok (UTC+7) → 13:00–23:00 UTC
        var cfg = Config(
            (gbVar.CfgScheduleWindowStart, "20:00"),
            (gbVar.CfgScheduleWindowEnd, "06:00"),
            (gbVar.CfgScheduleTimezone, "Asia/Bangkok"));

        // 21:00 Bangkok = 14:00 UTC
        Assert.True(ScheduleConfigHelper.IsWithinWindow(cfg, new DateTime(2026, 7, 1, 14, 0, 0, DateTimeKind.Utc)));
        // 03:00 Bangkok = 20:00 UTC previous day
        Assert.True(ScheduleConfigHelper.IsWithinWindow(cfg, new DateTime(2026, 7, 1, 20, 0, 0, DateTimeKind.Utc)));
        // 12:00 Bangkok = 05:00 UTC — outside
        Assert.False(ScheduleConfigHelper.IsWithinWindow(cfg, new DateTime(2026, 7, 1, 5, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void IsWithinWindow_SameDay_InclusiveStartExclusiveEnd()
    {
        var cfg = Config(
            (gbVar.CfgScheduleWindowStart, "09:00"),
            (gbVar.CfgScheduleWindowEnd, "17:00"),
            (gbVar.CfgScheduleTimezone, "Asia/Bangkok"));

        // 10:00 Bangkok = 03:00 UTC
        Assert.True(ScheduleConfigHelper.IsWithinWindow(cfg, new DateTime(2026, 7, 1, 3, 0, 0, DateTimeKind.Utc)));
        // 17:00 Bangkok = 10:00 UTC — exclusive end
        Assert.False(ScheduleConfigHelper.IsWithinWindow(cfg, new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc)));
        // 08:00 Bangkok = 01:00 UTC
        Assert.False(ScheduleConfigHelper.IsWithinWindow(cfg, new DateTime(2026, 7, 1, 1, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void ClampImportRange_NeverGoesBeforeCutover()
    {
        var cfg = Config((gbVar.CfgInterfaceCutoverDate, "2026-07-01"));
        var (from, to) = ScheduleConfigHelper.ClampImportRange(
            cfg, new DateTime(2026, 6, 1), new DateTime(2026, 7, 10));

        Assert.Equal(new DateTime(2026, 7, 1), from);
        Assert.Equal(new DateTime(2026, 7, 10), to);
    }

    [Fact]
    public void ClampImportRange_ToBeforeFrom_ClampsToFrom()
    {
        var cfg = Config((gbVar.CfgInterfaceCutoverDate, "2026-07-01"));
        var (from, to) = ScheduleConfigHelper.ClampImportRange(
            cfg, new DateTime(2026, 7, 15), new DateTime(2026, 7, 10));

        Assert.Equal(new DateTime(2026, 7, 15), from);
        Assert.Equal(new DateTime(2026, 7, 15), to);
    }

    [Fact]
    public void EnumerateDayChunks_SingleChunk_WhenSpanFits()
    {
        var chunks = ScheduleConfigHelper.EnumerateDayChunks(
            new DateTime(2026, 7, 1), new DateTime(2026, 7, 3), 7).ToList();
        Assert.Single(chunks);
        Assert.Equal(new DateTime(2026, 7, 1), chunks[0].From);
        Assert.Equal(new DateTime(2026, 7, 3), chunks[0].To);
    }

    [Fact]
    public void EnumerateDayChunks_SplitsEvenly()
    {
        var chunks = ScheduleConfigHelper.EnumerateDayChunks(
            new DateTime(2026, 7, 1), new DateTime(2026, 7, 10), 3).ToList();

        Assert.Equal(4, chunks.Count);
        Assert.Equal((new DateTime(2026, 7, 1), new DateTime(2026, 7, 3)), chunks[0]);
        Assert.Equal((new DateTime(2026, 7, 4), new DateTime(2026, 7, 6)), chunks[1]);
        Assert.Equal((new DateTime(2026, 7, 7), new DateTime(2026, 7, 9)), chunks[2]);
        Assert.Equal((new DateTime(2026, 7, 10), new DateTime(2026, 7, 10)), chunks[3]);
    }

    [Fact]
    public void EnumerateDayChunks_Empty_WhenToBeforeFrom()
    {
        Assert.Empty(ScheduleConfigHelper.EnumerateDayChunks(
            new DateTime(2026, 7, 10), new DateTime(2026, 7, 1), 7));
    }
}
