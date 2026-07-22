using POS2SAP.API.DTOs.Sap;

namespace POS2SAP.Tests.Dtos;

public class SapIncomingPaymentJsonHelperTests
{
    [Fact]
    public void Normalize_ClearsCashFields_WhenCashSumZero()
    {
        var n = SapIncomingPaymentJsonHelper.Normalize(new SapIncomingPaymentDto
        {
            CashAcct = "1111",
            CashSum = 0,
            TrsfrAcct = "2222",
            TrsfrSum = 100,
            TrsfrDate = "2026-07-01",
            TrsfrRef = "REF"
        });

        Assert.Equal("", n.CashAcct);
        Assert.Equal(0, n.CashSum);
        Assert.Equal("2222", n.TrsfrAcct);
        Assert.Equal(100, n.TrsfrSum);
    }

    [Fact]
    public void Normalize_ClearsCreditAcct_Always()
    {
        var n = SapIncomingPaymentJsonHelper.Normalize(new SapIncomingPaymentDto
        {
            paymentCreditCards =
            [
                new SapPaymentCreditCardDto
                {
                    CreditSum = 891,
                    CreditCard = "1",
                    CreditAcct = "11201007"
                }
            ]
        });

        Assert.Single(n.paymentCreditCards);
        Assert.Equal("", n.paymentCreditCards[0].CreditAcct);
        Assert.Equal("1", n.paymentCreditCards[0].CreditCard);
    }

    [Fact]
    public void Normalize_FiltersZeroCreditCards()
    {
        var n = SapIncomingPaymentJsonHelper.Normalize(new SapIncomingPaymentDto
        {
            paymentCreditCards =
            [
                new SapPaymentCreditCardDto { CreditSum = 0, CreditCard = "VISA" },
                new SapPaymentCreditCardDto { CreditSum = 50, CreditCard = "MC", CreditAcct = "11201007" }
            ]
        });

        Assert.Single(n.paymentCreditCards);
        Assert.Equal("MC", n.paymentCreditCards[0].CreditCard);
        Assert.Equal("", n.paymentCreditCards[0].CreditAcct);
        Assert.Equal(0, n.paymentCreditCards[0].LineNum);
    }

    [Fact]
    public void Normalize_PreservesVatBranch()
    {
        var n = SapIncomingPaymentJsonHelper.Normalize(new SapIncomingPaymentDto
        {
            VatBranch = "00005"
        });
        Assert.Equal("00005", n.VatBranch);
    }

    [Fact]
    public void Normalize_AppliesDefaults()
    {
        var n = SapIncomingPaymentJsonHelper.Normalize(new SapIncomingPaymentDto());
        Assert.Equal("C", n.DocType);
        Assert.Equal("N", n.PayNoDoc);
        Assert.Equal("THB", n.DocCur);
        Assert.Equal("", n.VatBranch);
    }

    [Fact]
    public void TryExtractArInvoiceDocNum_FromArray()
    {
        var json = """[{"DocNum":"BFM-006|RC1","DocTotal":100}]""";
        Assert.Equal("BFM-006|RC1", SapIncomingPaymentJsonHelper.TryExtractArInvoiceDocNum(json));
    }

    [Fact]
    public void TryExtractArInvoiceDocNum_FromObject_LowerCase()
    {
        var json = """{"docNum":"BFM-006|RC2"}""";
        Assert.Equal("BFM-006|RC2", SapIncomingPaymentJsonHelper.TryExtractArInvoiceDocNum(json));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-json")]
    [InlineData("[]")]
    public void TryExtractArInvoiceDocNum_Invalid_ReturnsNull(string? json)
    {
        Assert.Null(SapIncomingPaymentJsonHelper.TryExtractArInvoiceDocNum(json));
    }

    [Fact]
    public void ToJsonArray_WrapsRootAsArray()
    {
        var json = SapIncomingPaymentJsonHelper.ToJsonArray(new SapIncomingPaymentDto { DocNum = "X" });
        Assert.StartsWith("[", json.TrimStart());
    }
}
