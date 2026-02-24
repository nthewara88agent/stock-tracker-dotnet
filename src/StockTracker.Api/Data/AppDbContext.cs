using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using StockTracker.Api.Models;

namespace StockTracker.Api.Data;

public class AppDbContext : IdentityDbContext<AppUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Holding> Holdings => Set<Holding>();
    public DbSet<PriceHistory> PriceHistories => Set<PriceHistory>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<Holding>(e =>
        {
            e.HasIndex(h => h.UserId);
            e.HasIndex(h => h.Ticker);
            e.Property(h => h.Quantity).HasPrecision(18, 6);
            e.Property(h => h.BuyPrice).HasPrecision(18, 6);
            e.Property(h => h.Brokerage).HasPrecision(18, 2);
        });
        builder.Entity<PriceHistory>(e =>
        {
            e.HasIndex(p => new { p.Ticker, p.Date }).IsUnique();
            e.Property(p => p.Open).HasPrecision(18, 6);
            e.Property(p => p.High).HasPrecision(18, 6);
            e.Property(p => p.Low).HasPrecision(18, 6);
            e.Property(p => p.Close).HasPrecision(18, 6);
        });
    }
}
