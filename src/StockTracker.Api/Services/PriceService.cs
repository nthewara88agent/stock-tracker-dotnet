using System.Text.Json;

namespace StockTracker.Api.Services;

public class PriceService
{
    private readonly HttpClient _http;
    private readonly ILogger<PriceService> _logger;

    public PriceService(HttpClient http, ILogger<PriceService> logger)
    {
        _http = http;
        _logger = logger;
        _http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
    }

    public async Task<decimal?> GetLatestPrice(string ticker)
    {
        try
        {
            var symbol = ticker.ToUpper().EndsWith(".AX") ? ticker.ToUpper() : $"{ticker.ToUpper()}.AX";
            var url = $"https://query1.finance.yahoo.com/v8/finance/chart/{symbol}?range=5d&interval=1d";
            var resp = await _http.GetAsync(url);
            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var result = doc.RootElement.GetProperty("chart").GetProperty("result")[0];
            var closes = result.GetProperty("indicators").GetProperty("quote")[0].GetProperty("close");

            // Get last non-null close
            decimal? lastClose = null;
            foreach (var item in closes.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Null)
                    lastClose = item.GetDecimal();
            }
            return lastClose;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch price for {Ticker}", ticker);
            return null;
        }
    }

    public async Task<List<(DateTime Date, decimal Open, decimal High, decimal Low, decimal Close, long Volume)>> GetPriceHistory(string ticker, int days = 365)
    {
        var results = new List<(DateTime, decimal, decimal, decimal, decimal, long)>();
        try
        {
            var symbol = ticker.ToUpper().EndsWith(".AX") ? ticker.ToUpper() : $"{ticker.ToUpper()}.AX";
            var url = $"https://query1.finance.yahoo.com/v8/finance/chart/{symbol}?range={days}d&interval=1d";
            var resp = await _http.GetAsync(url);
            if (!resp.IsSuccessStatusCode) return results;

            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var result = doc.RootElement.GetProperty("chart").GetProperty("result")[0];
            var timestamps = result.GetProperty("timestamp");
            var quote = result.GetProperty("indicators").GetProperty("quote")[0];
            var opens = quote.GetProperty("open");
            var highs = quote.GetProperty("high");
            var lows = quote.GetProperty("low");
            var closes = quote.GetProperty("close");
            var volumes = quote.GetProperty("volume");

            for (int i = 0; i < timestamps.GetArrayLength(); i++)
            {
                if (closes[i].ValueKind == JsonValueKind.Null) continue;
                var date = DateTimeOffset.FromUnixTimeSeconds(timestamps[i].GetInt64()).UtcDateTime;
                results.Add((
                    date,
                    opens[i].ValueKind != JsonValueKind.Null ? opens[i].GetDecimal() : 0,
                    highs[i].ValueKind != JsonValueKind.Null ? highs[i].GetDecimal() : 0,
                    lows[i].ValueKind != JsonValueKind.Null ? lows[i].GetDecimal() : 0,
                    closes[i].GetDecimal(),
                    volumes[i].ValueKind != JsonValueKind.Null ? volumes[i].GetInt64() : 0
                ));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch history for {Ticker}", ticker);
        }
        return results;
    }
}
