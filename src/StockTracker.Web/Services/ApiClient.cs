using System.Net.Http.Json;
using StockTracker.Shared.Dtos;

namespace StockTracker.Web.Services;

public class ApiClient
{
    private readonly HttpClient _http;

    public ApiClient(HttpClient http) => _http = http;

    // Auth
    public async Task<AuthResponse?> Register(string email, string password) =>
        await PostJson<AuthResponse>("/api/auth/register", new RegisterRequest(email, password));

    public async Task<AuthResponse?> Login(string email, string password) =>
        await PostJson<AuthResponse>("/api/auth/login", new LoginRequest(email, password));

    // Holdings
    public async Task<List<HoldingDto>> GetHoldings() =>
        await _http.GetFromJsonAsync<List<HoldingDto>>("/api/holdings") ?? new();

    public async Task<HoldingDto?> CreateHolding(CreateHoldingRequest req) =>
        await PostJson<HoldingDto>("/api/holdings", req);

    public async Task<HoldingDto?> UpdateHolding(int id, UpdateHoldingRequest req)
    {
        var resp = await _http.PutAsJsonAsync($"/api/holdings/{id}", req);
        return resp.IsSuccessStatusCode ? await resp.Content.ReadFromJsonAsync<HoldingDto>() : null;
    }

    public async Task DeleteHolding(int id) => await _http.DeleteAsync($"/api/holdings/{id}");

    // Portfolio
    public async Task<PortfolioSummaryDto?> GetPortfolioSummary() =>
        await _http.GetFromJsonAsync<PortfolioSummaryDto>("/api/portfolio/summary");

    // CGT
    public async Task<CgtReportDto?> GetCgtReport() =>
        await _http.GetFromJsonAsync<CgtReportDto>("/api/cgt/report");

    // Prices
    public async Task<Dictionary<string, decimal?>?> FetchPrices(List<string> tickers) =>
        await PostJson<Dictionary<string, decimal?>>("/api/prices/fetch", new FetchPricesRequest(tickers));

    private async Task<T?> PostJson<T>(string url, object data)
    {
        var resp = await _http.PostAsJsonAsync(url, data);
        return resp.IsSuccessStatusCode ? await resp.Content.ReadFromJsonAsync<T>() : default;
    }
}
