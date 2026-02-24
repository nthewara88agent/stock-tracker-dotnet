using Microsoft.AspNetCore.Identity;

namespace StockTracker.Api.Models;

public class AppUser : IdentityUser { }

public class Holding
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public string Ticker { get; set; } = "";
    public DateTime BuyDate { get; set; }
    public decimal Quantity { get; set; }
    public decimal BuyPrice { get; set; }
    public decimal Brokerage { get; set; }
    public AppUser? User { get; set; }
}

public class PriceHistory
{
    public int Id { get; set; }
    public string Ticker { get; set; } = "";
    public DateTime Date { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public long Volume { get; set; }
}
