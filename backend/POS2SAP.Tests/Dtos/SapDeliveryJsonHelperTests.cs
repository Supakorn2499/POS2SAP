using POS2SAP.API.DTOs.Sap;

namespace POS2SAP.Tests.Dtos;

public class SapDeliveryJsonHelperTests
{
    [Theory]
    [InlineData(null, "0")]
    [InlineData("  2  ", "2")]
    [InlineData(2, "2")]
    [InlineData(2.5, "2.5")]
    [InlineData(2.0, "2")]
    public void FormatQuantity_HandlesEdgeCases(object? value, string expected)
    {
        Assert.Equal(expected, SapDeliveryJsonHelper.FormatQuantity(value));
    }

    [Fact]
    public void Normalize_FillsNullsAndFallsBackWhsCode()
    {
        var src = new SapDeliveryDto
        {
            DocNum = "BFM-006|RC1",
            BranchCode = "BFM-006",
            DocumentLines =
            [
                new SapDeliveryLineDto
                {
                    ItemCode = "Fo8_2",
                    Quantity = "1",
                    WhsCode = ""
                }
            ]
        };

        var n = SapDeliveryJsonHelper.Normalize(src);
        Assert.Equal("BFM-006|RC1", n.DocNum);
        Assert.Equal("", n.Comments);
        Assert.Single(n.DocumentLines);
        Assert.Equal("BFM-006|RC1", n.DocumentLines[0].DocNum);
        Assert.Equal("BFM-006", n.DocumentLines[0].WhsCode);
        Assert.Equal("1", n.DocumentLines[0].Quantity);
        Assert.Equal(0, n.DocumentLines[0].LineNum);
    }

    [Fact]
    public void ToJson_IsSingleObject_NotArray()
    {
        var json = SapDeliveryJsonHelper.ToJson(new SapDeliveryDto { DocNum = "X" });
        Assert.StartsWith("{", json.TrimStart());
        Assert.DoesNotContain("[{\"DocNum\"", json);
    }
}
