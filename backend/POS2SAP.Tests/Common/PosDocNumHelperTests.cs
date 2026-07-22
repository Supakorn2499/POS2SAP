using POS2SAP.API.Common;

namespace POS2SAP.Tests.Common;

public class PosDocNumHelperTests
{
    [Fact]
    public void Build_JoinsBranchAndReceipt()
    {
        Assert.Equal("BFM-006|RC01072026/00001", PosDocNumHelper.Build("BFM-006", "RC01072026/00001"));
    }

    [Fact]
    public void Build_ReturnsCompositeUnchanged_WhenAlreadyComposite()
    {
        var composite = "BFM-006|RC01072026/00001";
        Assert.Equal(composite, PosDocNumHelper.Build("OTHER", composite));
    }

    [Fact]
    public void Build_UsesUnderscore_WhenBranchEmpty()
    {
        Assert.Equal("_|RC1", PosDocNumHelper.Build("", "RC1"));
        Assert.Equal("_|RC1", PosDocNumHelper.Build(null, "RC1"));
    }

    [Fact]
    public void TryParse_Composite_Succeeds()
    {
        Assert.True(PosDocNumHelper.TryParse("BFM-006|RC01072026/00001", out var branch, out var receipt));
        Assert.Equal("BFM-006", branch);
        Assert.Equal("RC01072026/00001", receipt);
    }

    [Fact]
    public void TryParse_LegacyBareReceipt_ReturnsFalseButSetsReceipt()
    {
        Assert.False(PosDocNumHelper.TryParse("RC01072026/00001", out var branch, out var receipt));
        Assert.Equal("", branch);
        Assert.Equal("RC01072026/00001", receipt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryParse_Empty_ReturnsFalse(string? docNum)
    {
        Assert.False(PosDocNumHelper.TryParse(docNum, out _, out _));
    }

    [Fact]
    public void Matches_Composite_RequiresBranchAndReceipt()
    {
        Assert.True(PosDocNumHelper.Matches("BFM-006|RC1", "BFM-006", "RC1"));
        Assert.False(PosDocNumHelper.Matches("BFM-006|RC1", "BFM-007", "RC1"));
        Assert.False(PosDocNumHelper.Matches("BFM-006|RC1", "BFM-006", "RC2"));
    }

    [Fact]
    public void Matches_LegacyBareReceipt_IgnoresBranch()
    {
        Assert.True(PosDocNumHelper.Matches("RC1", "BFM-006", "RC1"));
        Assert.False(PosDocNumHelper.Matches("RC1", "BFM-006", "RC2"));
    }

    [Fact]
    public void ExtractReceiptNumbers_DedupesCompositeAndBare()
    {
        var result = PosDocNumHelper.ExtractReceiptNumbers(new[]
        {
            "BFM-006|RC1",
            "RC1",
            "BFM-007|RC2",
            "",
            "  "
        });
        Assert.Equal(2, result.Count);
        Assert.Contains("RC1", result);
        Assert.Contains("RC2", result);
    }
}
