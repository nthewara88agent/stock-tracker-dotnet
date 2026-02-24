using System.Collections.Concurrent;
using StockTracker.App.Data;
using Microsoft.EntityFrameworkCore;

namespace StockTracker.App.Services;

public class PriceCacheService : BackgroundService
{
    private readonly ConcurrentDictionary<string, (decimal Price, DateTime FetchedAt)> _cache = new();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PriceCacheService> _logger;
    private const int CacheTtlMinutes = 15;

    public PriceCacheService(IServiceScopeFactory scopeFactory, ILogger<PriceCacheService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public decimal? GetCachedPrice(string ticker)
    {
        var key = ticker.ToUpper();
        if (_cache.TryGetValue(key, out var entry) &&
            DateTime.UtcNow - entry.FetchedAt < TimeSpan.FromMinutes(CacheTtlMinutes))
            return entry.Price;
        return null;
    }

    public void SetPrice(string ticker, decimal price)
    {
        _cache[ticker.ToUpper()] = (price, DateTime.UtcNow);
    }

    public async Task<Dictionary<string, decimal>> GetPricesAsync(IEnumerable<string> tickers)
    {
        var result = new Dictionary<string, decimal>();
        var toFetch = new List<string>();

        foreach (var t in tickers.Select(t => t.ToUpper()).Distinct())
        {
            var cached = GetCachedPrice(t);
            if (cached.HasValue)
                result[t] = cached.Value;
            else
                toFetch.Add(t);
        }

        if (toFetch.Count > 0)
        {
            using var scope = _scopeFactory.CreateScope();
            var priceService = scope.ServiceProvider.GetRequiredService<PriceService>();

            // Parallel fetch
            var tasks = toFetch.Select(async ticker =>
            {
                var price = await priceService.GetLatestPrice(ticker);
                if (price.HasValue)
                {
                    SetPrice(ticker, price.Value);
                    return (ticker, price: price.Value, found: true);
                }
                return (ticker, price: 0m, found: false);
            });

            var results = await Task.WhenAll(tasks);
            foreach (var r in results)
            {
                if (r.found) result[r.ticker] = r.price;
            }
        }

        return result;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Wait a bit before first refresh
        await Task.Delay(TimeSpan.FromSeconds(30), ct);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                // Get all tickers from DB
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var tickers = await db.Holdings
                    .AsNoTracking()
                    .Select(h => h.Ticker)
                    .Distinct()
                    .ToListAsync(ct);

                if (tickers.Count > 0)
                {
                    var priceService = scope.ServiceProvider.GetRequiredService<PriceService>();
                    var tasks = tickers.Select(async ticker =>
                    {
                        var price = await priceService.GetLatestPrice(ticker);
                        if (price.HasValue) SetPrice(ticker, price.Value);
                    });
                    await Task.WhenAll(tasks);
                    _logger.LogInformation("Refreshed prices for {Count} tickers", tickers.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Price cache refresh failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(CacheTtlMinutes), ct);
        }
    }
}
