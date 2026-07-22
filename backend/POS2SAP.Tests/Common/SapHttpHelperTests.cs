using POS2SAP.API.Common;

namespace POS2SAP.Tests.Common;

public class SapHttpHelperTests
{
    [Fact]
    public void GetTimeoutSeconds_Default_WhenMissing()
    {
        Assert.Equal(SapHttpHelper.DefaultTimeoutSeconds,
            SapHttpHelper.GetTimeoutSeconds(new Dictionary<string, string>()));
    }

    [Fact]
    public void GetTimeoutSeconds_UsesConfiguredValue()
    {
        var cfg = new Dictionary<string, string> { [gbVar.CfgSapHttpTimeoutSeconds] = "120" };
        Assert.Equal(120, SapHttpHelper.GetTimeoutSeconds(cfg));
    }

    [Theory]
    [InlineData("5", 10)]     // below min → clamp to 10
    [InlineData("999", 300)]  // above max → clamp to 300
    public void GetTimeoutSeconds_Clamps(string raw, int expected)
    {
        var cfg = new Dictionary<string, string> { [gbVar.CfgSapHttpTimeoutSeconds] = raw };
        Assert.Equal(expected, SapHttpHelper.GetTimeoutSeconds(cfg));
    }

    [Fact]
    public void GetTimeoutSeconds_Invalid_FallsBackToDefault()
    {
        var cfg = new Dictionary<string, string> { [gbVar.CfgSapHttpTimeoutSeconds] = "abc" };
        Assert.Equal(SapHttpHelper.DefaultTimeoutSeconds, SapHttpHelper.GetTimeoutSeconds(cfg));
    }

    [Fact]
    public void GetTimeout_MatchesSeconds()
    {
        var cfg = new Dictionary<string, string> { [gbVar.CfgSapHttpTimeoutSeconds] = "45" };
        Assert.Equal(TimeSpan.FromSeconds(45), SapHttpHelper.GetTimeout(cfg));
    }
}
