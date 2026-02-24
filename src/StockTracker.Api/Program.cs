using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StockTracker.Api.Auth;
using StockTracker.Api.Data;
using StockTracker.Api.Models;
using StockTracker.Api.Services;
using StockTracker.Shared.Dtos;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// Identity
builder.Services.AddIdentity<AppUser, IdentityRole>(opt =>
{
    opt.Password.RequireDigit = false;
    opt.Password.RequireUppercase = false;
    opt.Password.RequireNonAlphanumeric = false;
    opt.Password.RequiredLength = 6;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// JWT
var jwtKey = builder.Configuration["Jwt:Key"] ?? "SuperSecretKeyThatIsAtLeast32CharactersLong!!";
builder.Services.AddAuthentication(opt =>
{
    opt.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    opt.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(opt =>
{
    opt.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "StockTracker",
        ValidAudience = builder.Configuration["Jwt:Audience"] ?? "StockTracker",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };
});
builder.Services.AddAuthorization();

// Services
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddSingleton<CgtService>();
builder.Services.AddHttpClient<PriceService>();

// CORS
builder.Services.AddCors(opt => opt.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var authDisabled = builder.Configuration.GetValue<bool>("AUTH_DISABLED");

var app = builder.Build();

// Auto-migrate + seed dev user
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    if (authDisabled)
    {
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var devUser = await userManager.FindByEmailAsync("dev@local");
        if (devUser == null)
        {
            devUser = new AppUser { UserName = "dev@local", Email = "dev@local" };
            await userManager.CreateAsync(devUser, "Dev123!");
        }
    }
}

app.UseCors();

// When AUTH_DISABLED, inject a fake dev user identity BEFORE auth middleware
if (authDisabled)
{
    app.Use(async (context, next) =>
    {
        using var scope = context.RequestServices.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var devUser = await userManager.FindByEmailAsync("dev@local");
        if (devUser != null)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, devUser.Id),
                new(ClaimTypes.Email, devUser.Email!)
            };
            context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "DevAuth"));
        }
        await next();
    });
}

app.UseAuthentication();
app.UseAuthorization();

// ===== Health =====
app.MapGet("/api/health", () => Results.Ok(new { status = "healthy" }));
app.MapGet("/api/auth/status", () => Results.Ok(new { authDisabled }));

// ===== Auth Endpoints =====
app.MapPost("/api/auth/register", async (RegisterRequest req, UserManager<AppUser> userManager, JwtTokenService jwt) =>
{
    var user = new AppUser { UserName = req.Email, Email = req.Email };
    var result = await userManager.CreateAsync(user, req.Password);
    if (!result.Succeeded)
        return Results.BadRequest(result.Errors.Select(e => e.Description));
    var token = jwt.GenerateToken(user);
    return Results.Ok(new AuthResponse(token, user.Email!));
});

app.MapPost("/api/auth/login", async (LoginRequest req, UserManager<AppUser> userManager, JwtTokenService jwt) =>
{
    var user = await userManager.FindByEmailAsync(req.Email);
    if (user == null || !await userManager.CheckPasswordAsync(user, req.Password))
        return Results.Unauthorized();
    var token = jwt.GenerateToken(user);
    return Results.Ok(new AuthResponse(token, user.Email!));
});

// ===== Holdings CRUD =====
string GetUserId(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier)!;

app.MapGet("/api/holdings", async (AppDbContext db, ClaimsPrincipal user) =>
{
    var userId = GetUserId(user);
    var holdings = await db.Holdings.Where(h => h.UserId == userId).ToListAsync();
    return Results.Ok(holdings.Select(h => new HoldingDto
    {
        Id = h.Id, Ticker = h.Ticker, BuyDate = h.BuyDate,
        Quantity = h.Quantity, BuyPrice = h.BuyPrice, Brokerage = h.Brokerage
    }));
}).RequireAuthorization();

app.MapPost("/api/holdings", async (CreateHoldingRequest req, AppDbContext db, ClaimsPrincipal user) =>
{
    var holding = new Holding
    {
        UserId = GetUserId(user), Ticker = req.Ticker.ToUpper(), BuyDate = req.BuyDate,
        Quantity = req.Quantity, BuyPrice = req.BuyPrice, Brokerage = req.Brokerage
    };
    db.Holdings.Add(holding);
    await db.SaveChangesAsync();
    return Results.Created($"/api/holdings/{holding.Id}", new HoldingDto
    {
        Id = holding.Id, Ticker = holding.Ticker, BuyDate = holding.BuyDate,
        Quantity = holding.Quantity, BuyPrice = holding.BuyPrice, Brokerage = holding.Brokerage
    });
}).RequireAuthorization();

app.MapPut("/api/holdings/{id}", async (int id, UpdateHoldingRequest req, AppDbContext db, ClaimsPrincipal user) =>
{
    var userId = GetUserId(user);
    var holding = await db.Holdings.FirstOrDefaultAsync(h => h.Id == id && h.UserId == userId);
    if (holding == null) return Results.NotFound();
    holding.Ticker = req.Ticker.ToUpper();
    holding.BuyDate = req.BuyDate;
    holding.Quantity = req.Quantity;
    holding.BuyPrice = req.BuyPrice;
    holding.Brokerage = req.Brokerage;
    await db.SaveChangesAsync();
    return Results.Ok(new HoldingDto
    {
        Id = holding.Id, Ticker = holding.Ticker, BuyDate = holding.BuyDate,
        Quantity = holding.Quantity, BuyPrice = holding.BuyPrice, Brokerage = holding.Brokerage
    });
}).RequireAuthorization();

app.MapDelete("/api/holdings/{id}", async (int id, AppDbContext db, ClaimsPrincipal user) =>
{
    var userId = GetUserId(user);
    var holding = await db.Holdings.FirstOrDefaultAsync(h => h.Id == id && h.UserId == userId);
    if (holding == null) return Results.NotFound();
    db.Holdings.Remove(holding);
    await db.SaveChangesAsync();
    return Results.NoContent();
}).RequireAuthorization();

// ===== Prices =====
app.MapPost("/api/prices/fetch", async (FetchPricesRequest req, PriceService priceService, AppDbContext db) =>
{
    var results = new Dictionary<string, decimal?>();
    foreach (var ticker in req.Tickers)
    {
        var price = await priceService.GetLatestPrice(ticker);
        results[ticker.ToUpper()] = price;
        if (price.HasValue)
        {
            var existing = await db.PriceHistories
                .FirstOrDefaultAsync(p => p.Ticker == ticker.ToUpper() && p.Date.Date == DateTime.UtcNow.Date);
            if (existing == null)
            {
                db.PriceHistories.Add(new PriceHistory
                {
                    Ticker = ticker.ToUpper(), Date = DateTime.UtcNow.Date,
                    Close = price.Value, Open = price.Value, High = price.Value, Low = price.Value
                });
            }
            else
            {
                existing.Close = price.Value;
            }
        }
    }
    await db.SaveChangesAsync();
    return Results.Ok(results);
}).RequireAuthorization();

app.MapGet("/api/prices/history/{ticker}", async (string ticker, AppDbContext db) =>
{
    var history = await db.PriceHistories
        .Where(p => p.Ticker == ticker.ToUpper())
        .OrderByDescending(p => p.Date)
        .Take(365)
        .Select(p => new PriceDto(p.Ticker, p.Date, p.Open, p.High, p.Low, p.Close, p.Volume))
        .ToListAsync();
    return Results.Ok(history);
}).RequireAuthorization();

// ===== Portfolio Summary =====
app.MapGet("/api/portfolio/summary", async (AppDbContext db, ClaimsPrincipal user, PriceService priceService) =>
{
    var userId = GetUserId(user);
    var holdings = await db.Holdings.Where(h => h.UserId == userId).ToListAsync();
    if (!holdings.Any()) return Results.Ok(new PortfolioSummaryDto());

    var prices = new Dictionary<string, decimal>();
    foreach (var ticker in holdings.Select(h => h.Ticker).Distinct())
    {
        var price = await priceService.GetLatestPrice(ticker);
        if (price.HasValue) prices[ticker] = price.Value;
    }

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

    // Set allocation
    var result = summaries.Select(s => s with { Allocation = totalValue != 0 ? (s.MarketValue / totalValue) * 100 : 0 }).ToList();

    return Results.Ok(new PortfolioSummaryDto
    {
        TotalValue = totalValue, TotalCost = totalCost,
        TotalPnL = totalValue - totalCost,
        TotalPnLPercent = totalCost != 0 ? ((totalValue - totalCost) / totalCost) * 100 : 0,
        Holdings = result
    });
}).RequireAuthorization();

// ===== CGT Report =====
app.MapGet("/api/cgt/report", async (AppDbContext db, ClaimsPrincipal user, PriceService priceService, CgtService cgtService) =>
{
    var userId = GetUserId(user);
    var holdings = await db.Holdings.Where(h => h.UserId == userId).ToListAsync();
    if (!holdings.Any()) return Results.Ok(new CgtReportDto());

    var prices = new Dictionary<string, decimal>();
    foreach (var ticker in holdings.Select(h => h.Ticker).Distinct())
    {
        var price = await priceService.GetLatestPrice(ticker);
        if (price.HasValue) prices[ticker] = price.Value;
    }

    var data = holdings.Select(h => (
        h.Id, h.Ticker, h.BuyDate, h.Quantity, h.BuyPrice,
        prices.GetValueOrDefault(h.Ticker, h.BuyPrice), h.Brokerage
    )).ToList();

    return Results.Ok(cgtService.Calculate(data));
}).RequireAuthorization();

app.Run();
