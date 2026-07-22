using POS2SAP.API.Services.Implementations;

namespace POS2SAP.Tests.Services;

public class SapResponseParseTests
{
    [Fact]
    public void ArInvoice_Success_ExtractsSapDocNum()
    {
        var raw = """{"Status":"Success","SAPDocNum":"12345"}""";
        var (ok, doc, err) = SapArInvoiceService.ParseSapResponseBody(raw);
        Assert.True(ok);
        Assert.Equal("12345", doc);
        Assert.Null(err);
    }

    [Fact]
    public void ArInvoice_Failed_ExtractsErrMsg()
    {
        var raw = """{"Status":"Failed","errMsg":"CardCode missing"}""";
        var (ok, doc, err) = SapArInvoiceService.ParseSapResponseBody(raw);
        Assert.False(ok);
        Assert.Equal("CardCode missing", err);
        Assert.Null(doc);
    }

    [Fact]
    public void ArInvoice_ArrayRoot_UsesFirstElement()
    {
        var raw = """[{"status":"Success","sapDocNum":999}]""";
        var (ok, doc, _) = SapArInvoiceService.ParseSapResponseBody(raw);
        Assert.True(ok);
        Assert.Equal("999", doc);
    }

    [Fact]
    public void ArInvoice_NonJson_TreatedAsSuccess()
    {
        var (ok, doc, err) = SapArInvoiceService.ParseSapResponseBody("OK");
        Assert.True(ok);
        Assert.Null(doc);
        Assert.Null(err);
    }

    [Fact]
    public void IncomingPayment_Failed_CaseInsensitive()
    {
        var raw = """{"status":"failed","ErrMsg":"timeout"}""";
        var (ok, _, err) = SapIncomingPaymentService.ParseSapResponseBody(raw);
        Assert.False(ok);
        Assert.Equal("timeout", err);
    }

    [Fact]
    public void Delivery_Draft_SetsIsDraft()
    {
        var raw = """{"Status":"Draft","DraftKey":"D-1","errMsg":"pending approval"}""";
        var (ok, doc, err, draft) = SapDeliveryService.ParseSapResponseBody(raw);
        Assert.False(ok);
        Assert.True(draft);
        Assert.Equal("D-1", doc);
        Assert.Equal("pending approval", err);
    }

    [Fact]
    public void Delivery_Success_OnlyWhenStatusSuccess()
    {
        // Delivery is stricter: missing Status ≠ success
        var raw = """{"SAPDocNum":"1"}""";
        var (ok, _, _, draft) = SapDeliveryService.ParseSapResponseBody(raw);
        Assert.False(ok);
        Assert.False(draft);
    }

    [Fact]
    public void Delivery_ExplicitSuccess()
    {
        var raw = """{"Status":"Success","SAPDocNum":"DL-9"}""";
        var (ok, doc, err, draft) = SapDeliveryService.ParseSapResponseBody(raw);
        Assert.True(ok);
        Assert.False(draft);
        Assert.Equal("DL-9", doc);
        Assert.Null(err);
    }
}
