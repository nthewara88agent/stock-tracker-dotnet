using StockTracker.Shared.Dtos;

namespace StockTracker.App.Services;

public class CgtService
{
    public CgtReportDto Calculate(List<(int Id, string Ticker, DateTime BuyDate, decimal Quantity, decimal BuyPrice, decimal CurrentPrice, decimal Brokerage)> holdings)
    {
        var items = new List<CgtHoldingDto>();
        decimal totalGains = 0, totalLosses = 0;

        foreach (var h in holdings)
        {
            var costBasis = (h.BuyPrice * h.Quantity) + h.Brokerage;
            var marketValue = h.CurrentPrice * h.Quantity;
            var capitalGain = marketValue - costBasis;
            var daysHeld = (DateTime.UtcNow - h.BuyDate).Days;
            var eligible = daysHeld > 365;
            var discounted = capitalGain > 0 && eligible ? capitalGain * 0.5m : capitalGain > 0 ? capitalGain : 0;

            if (capitalGain > 0) totalGains += capitalGain;
            else totalLosses += Math.Abs(capitalGain);

            items.Add(new CgtHoldingDto
            {
                HoldingId = h.Id, Ticker = h.Ticker, BuyDate = h.BuyDate,
                Quantity = h.Quantity, BuyPrice = h.BuyPrice, CurrentPrice = h.CurrentPrice,
                CapitalGain = capitalGain, EligibleForDiscount = eligible,
                DiscountedGain = discounted, DaysHeld = daysHeld, Brokerage = h.Brokerage
            });
        }

        var netGain = totalGains - totalLosses;
        var totalDiscounted = items.Where(i => i.CapitalGain > 0).Sum(i => i.DiscountedGain) - totalLosses;

        return new CgtReportDto
        {
            TotalCapitalGains = totalGains, TotalCapitalLosses = totalLosses,
            NetCapitalGain = netGain, DiscountedGain = Math.Max(0, totalDiscounted),
            Holdings = items
        };
    }
}
