using StockTracker.App.Services;

namespace StockTracker.Tests;

public class CgtServiceTests
{
    private readonly CgtService _sut = new();

    [Fact]
    public void Calculate_SingleHoldingWithGain_ReturnsCorrectGain()
    {
        var holdings = new List<(int Id, string Ticker, DateTime BuyDate, decimal Quantity, decimal BuyPrice, decimal CurrentPrice, decimal Brokerage)>
        {
            (1, "BHP", DateTime.UtcNow.AddDays(-100), 10m, 40m, 50m, 9.95m)
        };

        var report = _sut.Calculate(holdings);

        Assert.Single(report.Holdings);
        // Gain = (50*10) - (40*10 + 9.95) = 500 - 409.95 = 90.05
        Assert.Equal(90.05m, report.TotalCapitalGains);
        Assert.Equal(0m, report.TotalCapitalLosses);
        Assert.Equal(90.05m, report.NetCapitalGain);
    }

    [Fact]
    public void Calculate_HoldingWithLoss_ReturnsCorrectLoss()
    {
        var holdings = new List<(int Id, string Ticker, DateTime BuyDate, decimal Quantity, decimal BuyPrice, decimal CurrentPrice, decimal Brokerage)>
        {
            (1, "CBA", DateTime.UtcNow.AddDays(-30), 5m, 100m, 80m, 10m)
        };

        var report = _sut.Calculate(holdings);

        // Loss = (80*5) - (100*5 + 10) = 400 - 510 = -110
        Assert.Equal(0m, report.TotalCapitalGains);
        Assert.Equal(110m, report.TotalCapitalLosses);
        Assert.Equal(-110m, report.NetCapitalGain);
    }

    [Fact]
    public void Calculate_HeldOver12Months_EligibleForCgtDiscount()
    {
        var holdings = new List<(int Id, string Ticker, DateTime BuyDate, decimal Quantity, decimal BuyPrice, decimal CurrentPrice, decimal Brokerage)>
        {
            (1, "WES", DateTime.UtcNow.AddDays(-400), 10m, 50m, 70m, 0m)
        };

        var report = _sut.Calculate(holdings);

        var item = report.Holdings.Single();
        Assert.True(item.EligibleForDiscount);
        // Gain = 200, discounted = 100
        Assert.Equal(200m, item.CapitalGain);
        Assert.Equal(100m, item.DiscountedGain);
        Assert.Equal(0, item.CgtDaysRemaining);
    }

    [Fact]
    public void Calculate_HeldUnder12Months_NotEligibleForDiscount()
    {
        var holdings = new List<(int Id, string Ticker, DateTime BuyDate, decimal Quantity, decimal BuyPrice, decimal CurrentPrice, decimal Brokerage)>
        {
            (1, "NAB", DateTime.UtcNow.AddDays(-100), 10m, 30m, 40m, 0m)
        };

        var report = _sut.Calculate(holdings);

        var item = report.Holdings.Single();
        Assert.False(item.EligibleForDiscount);
        Assert.Equal(100m, item.DiscountedGain); // No discount, full gain
        Assert.True(item.CgtDaysRemaining > 0);
    }

    [Fact]
    public void Calculate_EmptyHoldings_ReturnsEmptyReport()
    {
        var holdings = new List<(int Id, string Ticker, DateTime BuyDate, decimal Quantity, decimal BuyPrice, decimal CurrentPrice, decimal Brokerage)>();

        var report = _sut.Calculate(holdings);

        Assert.Empty(report.Holdings);
        Assert.Equal(0m, report.TotalCapitalGains);
        Assert.Equal(0m, report.TotalCapitalLosses);
    }

    [Fact]
    public void Calculate_MixedGainsAndLosses_NetIsCorrect()
    {
        var holdings = new List<(int Id, string Ticker, DateTime BuyDate, decimal Quantity, decimal BuyPrice, decimal CurrentPrice, decimal Brokerage)>
        {
            (1, "BHP", DateTime.UtcNow.AddDays(-50), 10m, 40m, 60m, 0m),  // +200
            (2, "CBA", DateTime.UtcNow.AddDays(-50), 10m, 100m, 80m, 0m), // -200
        };

        var report = _sut.Calculate(holdings);

        Assert.Equal(200m, report.TotalCapitalGains);
        Assert.Equal(200m, report.TotalCapitalLosses);
        Assert.Equal(0m, report.NetCapitalGain);
    }
}
