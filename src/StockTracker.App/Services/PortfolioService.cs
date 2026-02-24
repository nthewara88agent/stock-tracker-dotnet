using Microsoft.EntityFrameworkCore;
using StockTracker.App.Data;
using StockTracker.Shared.Dtos;

namespace StockTracker.App.Services;

public class PortfolioService
{
    private readonly AppDbContext _db;
    private readonly PriceCacheService _priceCache;

    public PortfolioService(AppDbContext db, PriceCacheService priceCache)
    {
        _db = db;
        _priceCache = priceCache;
    }

    public async Task<PortfolioSummaryDto> GetSummary(string userId)
    {
        var holdings = await _db.Holdings
            .AsNoTracking()
            .Where(h => h.UserId == userId)
            .ToListAsync();

        if (!holdings.Any()) return new PortfolioSummaryDto();

        var tickers = holdings.Select(h => h.Ticker).Distinct();
        var prices = await _priceCache.GetPricesAsync(tickers);

        decimal totalValue = 0, totalCost = 0;
        var summaries = new List<HoldingSummaryDto>();

        foreach (var h in holdings)
        {
            var currentPrice = prices.GetValueOrDefault(h.Ticker, h.BuyPrice);
            var costBasis = (h.BuyPrice * h.Quantity) + h.Brokerage;
            var marketValue = currentPrice * h.Quantity;
            totalValue += marketValue;
            totalCost += costBasis;

            summaries.Add(new HoldingSummaryDto
            {
                Id = h.Id, Ticker = h.Ticker, Quantity = h.Quantity,
                BuyPrice = h.BuyPrice, CurrentPrice = currentPrice,
                CostBasis = costBasis, MarketValue = marketValue,
                PnL = marketValue - costBasis,
                PnLPercent = costBasis != 0 ? ((marketValue - costBasis) / costBasis) * 100 : 0,
                BuyDate = h.BuyDate, Brokerage = h.Brokerage
            });
        }

        var result = summaries.Select(s => s with { Allocation = totalValue != 0 ? (s.MarketValue / totalValue) * 100 : 0 }).ToList();

        return new PortfolioSummaryDto
        {
            TotalValue = totalValue, TotalCost = totalCost,
            TotalPnL = totalValue - totalCost,
            TotalPnLPercent = totalCost != 0 ? ((totalValue - totalCost) / totalCost) * 100 : 0,
            Holdings = result
        };
    }

    public async Task<List<HoldingDto>> GetHoldings(string userId)
    {
        return await _db.Holdings
            .AsNoTracking()
            .Where(h => h.UserId == userId)
            .Select(h => new HoldingDto
            {
                Id = h.Id, Ticker = h.Ticker, BuyDate = h.BuyDate,
                Quantity = h.Quantity, BuyPrice = h.BuyPrice, Brokerage = h.Brokerage
            })
            .ToListAsync();
    }

    public async Task<CgtReportDto> GetCgtReport(string userId, CgtService cgtService)
    {
        var holdings = await _db.Holdings
            .AsNoTracking()
            .Where(h => h.UserId == userId)
            .ToListAsync();

        if (!holdings.Any()) return new CgtReportDto();

        var tickers = holdings.Select(h => h.Ticker).Distinct();
        var prices = await _priceCache.GetPricesAsync(tickers);

        var data = holdings.Select(h => (
            h.Id, h.Ticker, h.BuyDate, h.Quantity, h.BuyPrice,
            prices.GetValueOrDefault(h.Ticker, h.BuyPrice), h.Brokerage
        )).ToList();

        return cgtService.Calculate(data);
    }
}
