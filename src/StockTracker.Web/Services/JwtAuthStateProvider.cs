using System.Security.Claims;
using System.Net.Http.Json;
using System.Text.Json;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;

namespace StockTracker.Web.Services;

public class JwtAuthStateProvider : AuthenticationStateProvider
{
    private readonly ILocalStorageService _localStorage;
    private readonly HttpClient _http;
    private bool? _authDisabled;

    public JwtAuthStateProvider(ILocalStorageService localStorage, HttpClient http)
    {
        _localStorage = localStorage;
        _http = http;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        // Check if auth is disabled on the API
        if (_authDisabled == null)
        {
            try
            {
                var resp = await _http.GetFromJsonAsync<JsonElement>("/api/auth/status");
                _authDisabled = resp.GetProperty("authDisabled").GetBoolean();
            }
            catch { _authDisabled = false; }
        }

        if (_authDisabled == true)
        {
            // Return an authenticated identity so AuthorizeRouteView lets us through
            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, "dev@local"),
                new(ClaimTypes.Email, "dev@local"),
                new(ClaimTypes.NameIdentifier, "dev")
            };
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(claims, "DevAuth")));
        }

        var token = await _localStorage.GetItemAsStringAsync("authToken");
        if (string.IsNullOrWhiteSpace(token))
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));

        token = token.Trim('"');
        _http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var jwtClaims = ParseClaimsFromJwt(token);
        var identity = new ClaimsIdentity(jwtClaims, "jwt");
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    public async Task LoginAsync(string token)
    {
        await _localStorage.SetItemAsStringAsync("authToken", token);
        _http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var claims = ParseClaimsFromJwt(token);
        var identity = new ClaimsIdentity(claims, "jwt");
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity))));
    }

    public async Task LogoutAsync()
    {
        await _localStorage.RemoveItemAsync("authToken");
        _http.DefaultRequestHeaders.Authorization = null;
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()))));
    }

    private static IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
    {
        var payload = jwt.Split('.')[1];
        var jsonBytes = ParseBase64WithoutPadding(payload);
        var pairs = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonBytes) ?? new();
        return pairs.Select(p => new Claim(p.Key, p.Value?.ToString() ?? ""));
    }

    private static byte[] ParseBase64WithoutPadding(string base64)
    {
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }
        return Convert.FromBase64String(base64);
    }
}
