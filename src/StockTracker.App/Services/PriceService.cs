using System.Text.Json;

namespace StockTracker.App.Services;

public class PriceService
{
    private readonly HttpClient _http;
    private readonly ILogger<PriceService> _logger;

    public PriceService(HttpClient http, ILogger<PriceService> logger)
    {
        _http = http;
        _logger = logger;
        _http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0");
    }

    public async Task<decimal?> GetLatestPrice(string ticker)
    {
        try
        {
            var symbol = ticker.ToUpper();
            var url = $"https://query1.finance.yahoo.com/v8/finance/chart/{symbol}?range=5d&interval=1d";
            var resp = await _http.GetAsync(url);
            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var result = doc.RootElement.GetProperty("chart").GetProperty("result")[0];
            var meta = result.GetProperty("meta");
            if (meta.TryGetProperty("regularMarketPrice", out var rmp))
                return rmp.GetDecimal();
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch price for {Ticker}", ticker);
            return null;
        }
    }
}
