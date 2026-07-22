using System.Dynamic;
using POS2SAP.API.Common;
using POS2SAP.API.DTOs.Sap;
using POS2SAP.API.Services.Implementations;
using static POS2SAP.API.Services.Implementations.PosDataService;

namespace POS2SAP.Tests.Services;

public class ArInvoiceMappingTests
{
    private const string TranKey = "TK1";
    private const string Branch = "BFM-006";

    private static ExpandoObject Head(
        decimal retail = 518m,
        decimal pay = 518m,
        decimal receiptDiscount = 0m,
        decimal sc = 0m,
        decimal scVat = 0m,
        decimal scBefore = 0m,
        int billPoint = 0,
        int redeemPoint = 0,
        int balancePoint = 0,
        string memberNo = "",
        string comments = "")
    {
        dynamic h = new ExpandoObject();
        h.TranKey = TranKey;
        h.PosDocNo = "RC01072026/00002";
        h.DocDate = "2026-07-01";
        h.BranchCode = Branch;
        h.BranchName = "FAMTIME (One Bangkok Branch)";
        h.PosId = "78";
        h.CardCode = "CBFM-006";
        h.CardName = "FAMTIME (One Bangkok Branch)";
        h.CustTaxId = "";
        h.Address = "";
        h.CustVatBranch = "";
        h.CustTel = "";
        h.CustMemberNo = memberNo;
        h.VatBranch = "00005";
        h.Comments = comments;
        h.Channel = "Eat In";
        h.CustBillPoint = billPoint;
        h.CustRedeemPoing = redeemPoint;
        h.CustBalancePoint = balancePoint;
        // TotalAmtBefDis = VATable (ex-VAT); ReceiptRetailPrice = VAT-inc for DiscPrcnt
        h.TotalAmtBefDis = Math.Round(retail / 1.07m, 2, MidpointRounding.AwayFromZero);
        h.ReceiptRetailPrice = retail;
        h.ReceiptDiscount = receiptDiscount;
        h.DiscPrcnt = 0m;
        h.DownPaymentNo = "";
        h.DownPaymentAmt = 0m;
        h.DocTotal = pay;
        h.ServiceCharge = sc;
        h.ServiceChargeVat = scVat;
        h.ServiceChargeBeforeVat = scBefore;
        return (ExpandoObject)h;
    }

    private static ExpandoObject Line(
        string itemCode,
        decimal retail,
        decimal weight,
        decimal discPrcnt = 0m,
        decimal qty = 1m,
        string text = "item")
    {
        var after = weight > 0 ? weight : retail;
        var hasLineDisc = discPrcnt > 0;
        var gTotal = hasLineDisc ? weight : retail;
        var lineTotal = Math.Round(gTotal / 1.07m, 2, MidpointRounding.AwayFromZero);
        var price = qty == 0 ? 0 : Math.Round(retail / 1.07m / qty, 2, MidpointRounding.AwayFromZero);
        var priceAfVat = qty == 0 ? 0 : Math.Round((hasLineDisc ? weight : retail) / qty, 2, MidpointRounding.AwayFromZero);

        dynamic l = new ExpandoObject();
        l.TranKey = TranKey;
        l.OrderDetailID = 0;
        l.ItemCode = itemCode;
        l.ItemCategory = "FD";
        l.Text = text;
        l.Quantity = qty;
        l.UomCode = "";
        l.TotalRetailPrice = retail;
        l.WeightPrice = weight;
        l.DiscPrcnt = discPrcnt;
        l.Price = price;
        l.PriceAfVat = priceAfVat;
        l.VatSum = Math.Round(weight * 7m / 107m, 2, MidpointRounding.AwayFromZero);
        l.LineTotal = lineTotal;
        l.GTotal = gTotal;
        return (ExpandoObject)l;
    }

    private static SapArInvoiceHeadDto Build(
        ExpandoObject head,
        List<ExpandoObject> lines,
        ArSynthExtras? extras = null)
    {
        var linesByKey = new Dictionary<string, List<dynamic>>(StringComparer.OrdinalIgnoreCase)
        {
            [TranKey] = lines.Cast<dynamic>().ToList()
        };
        var synth = extras ?? new ArSynthExtras();
        var synthByKey = new Dictionary<string, ArSynthExtras>(StringComparer.OrdinalIgnoreCase)
        {
            [TranKey] = synth
        };
        return BuildArInvoice(head, linesByKey, synthByKey);
    }

    private static decimal SumGTotal(SapArInvoiceHeadDto bill) =>
        bill.DocumentLines.Sum(l => l.GTotal);

    [Fact]
    public void NonMember_BasicBill_MapsHeadAndLines()
    {
        var bill = Build(Head(retail: 518, pay: 518),
        [
            Line("T06_22", 299, 299, text: "Pappardelle"),
            Line("T06_24", 219, 219, text: "Linguine")
        ]);

        Assert.Equal("BFM-006|RC01072026/00002", bill.DocNum);
        Assert.Equal("", bill.CustMemberNo);
        Assert.Equal(0, bill.DiscPrcnt);
        Assert.Equal(518, bill.DocTotal);
        Assert.Equal(Math.Round(518m / 1.07m, 2, MidpointRounding.AwayFromZero), bill.TotalAmtBefDis);
        Assert.Equal(2, bill.DocumentLines.Count);
        Assert.Equal(518, SumGTotal(bill));
        Assert.All(bill.DocumentLines, l => Assert.Equal(gbVar.SapVatPrcnt, l.VatPrcnt));
        Assert.All(bill.DocumentLines, l => Assert.Equal(gbVar.SapVatGroup, l.VatGroup));
    }

    [Fact]
    public void Member_MapsPointsAndMemberCode()
    {
        var bill = Build(
            Head(retail: 518, pay: 518, memberNo: "0818118775", billPoint: 25, redeemPoint: 20, balancePoint: 110),
            [Line("T06_22", 518, 518)]);

        Assert.Equal("0818118775", bill.CustMemberNo);
        Assert.Equal(25, bill.CustBillPoint);
        Assert.Equal(20, bill.CustRedeemPoing);
        Assert.Equal(110, bill.CustBalancePoint);
    }

    [Fact]
    public void BillDiscount_SetsHeadDiscPrcnt_LinesStayZeroDisc()
    {
        // retail 1000, bill discount 100 → head DiscPrcnt = 10
        var bill = Build(
            Head(retail: 1000, pay: 900, receiptDiscount: 100),
            [Line("Fo4_5", 1000, 1000)]);

        Assert.Equal(10m, bill.DiscPrcnt);
        Assert.Equal(900, bill.DocTotal);
        Assert.All(bill.DocumentLines, l => Assert.Equal(0m, l.DiscPrcnt));
    }

    [Fact]
    public void UniformBillPromo_MapsHeadDiscPrcnt_LinesAtFullRetail()
    {
        // RC01072026/01250 — KTC 13% on every item; SAP expects head 13%, line DiscPrcnt 0
        var bill = Build(
            Head(retail: 1175, pay: 1124.47m, receiptDiscount: 152.75m,
                sc: 102.22m, scVat: 6.69m, scBefore: 95.53m),
            [
                Line("BR010_10", 85, 73.95m, discPrcnt: 13m, text: "Fresh Lemonade"),
                Line("Fo6_20", 275, 239.25m, discPrcnt: 13m, text: "Pappardelle"),
                Line("Fo8_3", 295, 256.65m, discPrcnt: 13m, text: "truffle pizza"),
                Line("Fo4_7", 235, 204.45m, discPrcnt: 13m, text: "Spicy chicken"),
                Line("BR01_3", 25, 21.75m, discPrcnt: 13m, text: "Rynn Sparkling Water"),
                Line("BR07_6", 45, 39.15m, discPrcnt: 13m, text: "Coke Zero"),
                Line("Fo12_1", 215, 187.05m, discPrcnt: 13m, text: "Spinach Ravioli")
            ]);

        Assert.Equal(13m, bill.DiscPrcnt);
        Assert.Equal(1124.47m, bill.DocTotal);
        var products = bill.DocumentLines.Where(l => l.ItemCode != gbVar.SapArItemServiceCharge).ToList();
        Assert.Equal(7, products.Count);
        Assert.All(products, l => Assert.Equal(0m, l.DiscPrcnt));
        Assert.Equal(85m, products.Single(l => l.ItemCode == "BR010_10").GTotal);
        Assert.Equal(275m, products.Single(l => l.ItemCode == "Fo6_20").GTotal);
        var sc = bill.DocumentLines.Single(l => l.ItemCode == gbVar.SapArItemServiceCharge);
        Assert.Equal(102.22m, sc.GTotal);
        Assert.Equal("SC", sc.ItemCategory);
    }

    [Fact]
    public void MixedLineDiscount_KeepsLineDiscPrcnt_HeadStaysZero()
    {
        // Per-item promo % differs → true line discount (SAP case 1 pattern)
        var bill = Build(
            Head(retail: 1458.15m, pay: 1458.15m, receiptDiscount: 0m, comments: "Discount"),
            [
                Line("T04_2", 219, 186.15m, discPrcnt: 15m, text: "Kra Pao Salmon"),
                Line("Fo6_18", 522, 469.8m, discPrcnt: 10m, qty: 2, text: "Carbonara"),
                Line("Fo4_5", 750, 750m, discPrcnt: 0m, qty: 3, text: "Pesto Porkchop")
            ]);

        Assert.Equal(0m, bill.DiscPrcnt);
        Assert.Equal(15m, bill.DocumentLines.Single(l => l.ItemCode == "T04_2").DiscPrcnt);
        Assert.Equal(10m, bill.DocumentLines.Single(l => l.ItemCode == "Fo6_18").DiscPrcnt);
        Assert.Equal(0m, bill.DocumentLines.Single(l => l.ItemCode == "Fo4_5").DiscPrcnt);
        Assert.Equal(186.15m, bill.DocumentLines.Single(l => l.ItemCode == "T04_2").GTotal);
    }

    [Fact]
    public void LineDiscount_SetsLineDiscPrcnt_HeadDiscStaysZero()
    {
        // line disc already reflected in WeightPrice; ReceiptDiscount may include line disc amount
        var bill = Build(
            Head(retail: 219, pay: 186.15m, receiptDiscount: 32.85m),
            [Line("T04_2", 219, 186.15m, discPrcnt: 15m)]);

        Assert.Equal(0m, bill.DiscPrcnt);
        Assert.Single(bill.DocumentLines);
        Assert.Equal(15m, bill.DocumentLines[0].DiscPrcnt);
        Assert.Equal(186.15m, bill.DocumentLines[0].GTotal);
    }

    [Fact]
    public void ServiceCharge_AddsScLine()
    {
        var bill = Build(
            Head(retail: 1000, pay: 1100, sc: 100, scVat: 6.54m, scBefore: 93.46m),
            [Line("Fo8_2", 1000, 1000)]);

        var sc = bill.DocumentLines.Single(l => l.ItemCode == gbVar.SapArItemServiceCharge);
        Assert.Equal(1, sc.Quantity);
        Assert.Equal("SC", sc.ItemCategory);
        Assert.Equal(100, sc.GTotal);
        Assert.Equal("Service Charge", sc.Text);
        Assert.Equal(1100, SumGTotal(bill));
    }

    [Fact]
    public void Coupon_AddsNegativeRvDcLine_AndCommentsCode()
    {
        var extras = new ArSynthExtras();
        extras.Coupons.Add((100m, "126070042"));

        var bill = Build(
            Head(retail: 1000, pay: 1000, comments: ""),
            [Line("Fo2_1", 1000, 1000)],
            extras);

        var coupon = bill.DocumentLines.Single(l => l.ItemCode == gbVar.SapArItemCoupon);
        Assert.Equal(-1, coupon.Quantity);
        Assert.Equal(-100m, coupon.GTotal);
        Assert.Equal(gbVar.SapArCatCoupon, coupon.ItemCategory);
        Assert.Equal(900m, bill.DocTotal);
        Assert.Contains("126070042", bill.Comments);
        Assert.Equal(900m, SumGTotal(bill));
    }

    [Fact]
    public void GiftVoucher_AddsNegativeRvCpLine()
    {
        var extras = new ArSynthExtras();
        extras.GiftVouchers.Add((200m, "0000389"));

        var bill = Build(
            Head(retail: 605m, pay: 665.5m, sc: 60.5m, scVat: 3.96m, scBefore: 56.54m),
            [
                Line("Fo8_3", 295, 295),
                Line("Fo4_7", 235, 235),
                Line("F1_4", 75, 75)
            ],
            extras);

        // DocTotal = PayPrice - voucher; lines + SC - voucher must match
        Assert.Equal(465.5m, bill.DocTotal);
        var gv = bill.DocumentLines.Single(l => l.ItemCode == gbVar.SapArItemGiftVoucher);
        Assert.Equal(-200m, gv.GTotal);
        Assert.Equal(gbVar.SapArCatGiftVoucher, gv.ItemCategory);
        Assert.Contains("0000389", gv.Text);
        Assert.Equal(bill.DocTotal, SumGTotal(bill));
    }

    [Fact]
    public void Freebie_AddsNegativeOpLine_AndDoesNotThrowDecimalToInt()
    {
        // freebie line: WeightPrice=0, TotalRetailPrice>0 — regression for decimal→int Sum bug
        var bill = Build(
            Head(retail: 1195, pay: 1166, receiptDiscount: 135, sc: 106, scVat: 6.93m, scBefore: 99.07m),
            [
                Line("Fo8_3", 295, 295),
                Line("D1_7", 135, 0, text: "Pecan Caramel Custard"), // freebie
                Line("Fo7_3", 145, 145),
                Line("Fo6_22", 165, 165),
                Line("Fo7_1", 155, 155),
                Line("BR01_2", 25, 25),
                Line("Fo6_20", 275, 275)
            ]);

        var free = bill.DocumentLines
            .Where(l => l.ItemCategory == gbVar.SapArCatFreebie && l.GTotal < 0)
            .ToList();
        Assert.Single(free);
        Assert.Equal("", free[0].ItemCode);
        Assert.Equal(-1, free[0].Quantity);
        Assert.Equal(-135m, free[0].GTotal);
        Assert.Equal("OP", free[0].ItemCategory);
        Assert.Equal("Pecan Caramel Custard", free[0].Text); // fallback product name when no promo row
        // head disc should exclude freebie amount from ReceiptDiscount
        Assert.Equal(0m, bill.DiscPrcnt);
        Assert.Equal(bill.DocTotal, SumGTotal(bill));
    }

    [Fact]
    public void Freebie_UsesPromotionName_WhenPromotionIdGreaterThanZero()
    {
        var extras = new ArSynthExtras();
        // bill-level promo (OrderDetailID=0) matching freebie retail — like RC01072026/02492
        extras.Promos.Add(new ArPromoRow(0, 170, 135m, "Redeem 12Point Pecan Caramel Custard"));
        extras.Promos.Add(new ArPromoRow(0, 0, 50m, "should-ignore-bill-end")); // PromotionID=0 = ส่วนลดท้ายบิล

        var bill = Build(
            Head(retail: 430, pay: 295, receiptDiscount: 135),
            [
                Line("Fo8_3", 295, 295),
                Line("D1_7", 135, 0, text: "Pecan Caramel Custard")
            ],
            extras);

        var free = bill.DocumentLines.Single(l => l.ItemCategory == "OP" && l.GTotal < 0);
        Assert.Equal("", free.ItemCode);
        Assert.Equal("Redeem 12Point Pecan Caramel Custard", free.Text);
    }

    [Fact]
    public void Freebie_IgnoresPromotionIdZero_UsesProductNameFallback()
    {
        var extras = new ArSynthExtras();
        extras.Promos.Add(new ArPromoRow(0, 0, 135m, "Bill End Discount")); // ส่วนลดท้ายบิล

        var bill = Build(
            Head(retail: 430, pay: 295, receiptDiscount: 135),
            [
                Line("Fo8_3", 295, 295),
                Line("D1_7", 135, 0, text: "Pecan Caramel Custard")
            ],
            extras);

        var free = bill.DocumentLines.Single(l => l.ItemCategory == "OP" && l.GTotal < 0);
        Assert.Equal("Pecan Caramel Custard", free.Text);
    }

    [Fact]
    public void Redeem_UsesRvRd_WhenRedeemPointsPresent()
    {
        var bill = Build(
            Head(retail: 1200, pay: 1284, redeemPoint: 100, receiptDiscount: 315.65m),
            [
                Line("Fo8_2", 1599.65m, 1599.65m),
                Line("Fo8_2", 315.65m, 0, text: "free pizza") // free via redeem
            ]);

        var rd = bill.DocumentLines.Single(l => l.ItemCode == gbVar.SapArItemRedeem);
        Assert.Equal(gbVar.SapArCatRedeem, rd.ItemCategory);
        Assert.Equal(-1, rd.Quantity);
        Assert.Contains("100", rd.Text);
        Assert.DoesNotContain(bill.DocumentLines, l => l.ItemCategory == gbVar.SapArCatFreebie);
    }

    [Fact]
    public void MakeNegativeLine_HasNegativeVatAndTotals()
    {
        var line = MakeNegativeLine(0, "DOC", Branch, gbVar.SapArItemCoupon, gbVar.SapArCatCoupon,
            "ส่วนลด", 93.46m, 100m, 6.54m);

        Assert.Equal(-1, line.Quantity);
        Assert.Equal(-93.46m, line.LineTotal);
        Assert.Equal(-100m, line.GTotal);
        Assert.Equal(-6.54m, line.VatSum);
        Assert.Equal(93.46m, line.Price);
        Assert.Equal(100m, line.PriceAfVat);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("0", false)]
    [InlineData("Fo8_2", true)]
    public void IsBillableItemCode_FiltersZeros(string? code, bool expected)
    {
        Assert.Equal(expected, IsBillableItemCode(code));
    }
}
