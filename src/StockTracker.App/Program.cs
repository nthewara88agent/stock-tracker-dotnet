using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using StockTracker.App.Auth;
using StockTracker.App.Data;
using StockTracker.App.Models;
using StockTracker.App.Services;
using StockTracker.App.Components;
using StockTracker.Shared.Dtos;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.IO.Compression;
using System.Text;
using MudBlazor.Services;

// Tell Npgsql to accept DateTime.Local as UTC (Identity uses DateTime.Now internally)
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Response compression
builder.Services.AddResponseCompression(opts =>
{
    opts.EnableForHttps = true;
    opts.MimeTypes = ResponseCompressionDefaults.MimeTypes;
    opts.Providers.Add<BrotliCompressionProvider>();
    opts.Providers.Add<GzipCompressionProvider>();
});
builder.Services.Configure<BrotliCompressionProviderOptions>(opts => opts.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(opts => opts.Level = CompressionLevel.Fastest);

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

// JWT (for API endpoints)
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
builder.Services.AddSingleton<PriceCacheService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<PriceCacheService>());
builder.Services.AddScoped<PortfolioService>();
builder.Services.AddSingleton<DevUserService>();

// MudBlazor
builder.Services.AddMudServices();

// Blazor Server
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// CORS (for API)
builder.Services.AddCors(opt => opt.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

// Output caching
builder.Services.AddOutputCache(opts =>
{
    opts.AddBasePolicy(b => b.Expire(TimeSpan.FromSeconds(5)));
});

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

app.UseResponseCompression();
app.UseCors();
app.UseStaticFiles();

// When AUTH_DISABLED, inject a fake dev user identity for API endpoints
if (authDisabled)
{
    app.Use(async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
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
        }
        await next();
    });
}

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.UseOutputCache();

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
    var holdings = await db.Holdings.AsNoTracking().Where(h => h.UserId == userId).ToListAsync();
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

// ===== Portfolio Summary (cached) =====
app.MapGet("/api/portfolio/summary", async (AppDbContext db, ClaimsPrincipal user, PriceCacheService priceCache) =>
{
    var userId = GetUserId(user);
    var portfolioService = new PortfolioService(db, priceCache);
    return Results.Ok(await portfolioService.GetSummary(userId));
}).RequireAuthorization().CacheOutput();

// ===== CGT Report =====
app.MapGet("/api/cgt/report", async (AppDbContext db, ClaimsPrincipal user, PriceCacheService priceCache, CgtService cgtService) =>
{
    var userId = GetUserId(user);
    var portfolioService = new PortfolioService(db, priceCache);
    return Results.Ok(await portfolioService.GetCgtReport(userId, cgtService));
}).RequireAuthorization();

// ===== Blazor =====
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
