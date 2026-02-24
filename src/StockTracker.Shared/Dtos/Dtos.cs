namespace StockTracker.Shared.Dtos;

public record RegisterRequest(string Email, string Password);
public record LoginRequest(string Email, string Password);
public record AuthResponse(string Token, string Email);

public record HoldingDto
{
    public int Id { get; init; }
    public string Ticker { get; init; } = "";
    public DateTime BuyDate { get; init; }
    public decimal Quantity { get; init; }
    public decimal BuyPrice { get; init; }
    public decimal Brokerage { get; init; }
}

public record CreateHoldingRequest(string Ticker, DateTime BuyDate, decimal Quantity, decimal BuyPrice, decimal Brokerage);
public record UpdateHoldingRequest(string Ticker, DateTime BuyDate, decimal Quantity, decimal BuyPrice, decimal Brokerage);

public record PriceDto(string Ticker, DateTime Date, decimal Open, decimal High, decimal Low, decimal Close, long Volume);

public record PortfolioSummaryDto
{
    public decimal TotalValue { get; init; }
    public decimal TotalCost { get; init; }
    public decimal TotalPnL { get; init; }
    public decimal TotalPnLPercent { get; init; }
    public List<HoldingSummaryDto> Holdings { get; init; } = new();
}

public record HoldingSummaryDto
{
    public int Id { get; init; }
    public string Ticker { get; init; } = "";
    public decimal Quantity { get; init; }
    public decimal BuyPrice { get; init; }
    public decimal CurrentPrice { get; init; }
    public decimal CostBasis { get; init; }
    public decimal MarketValue { get; init; }
    public decimal PnL { get; init; }
    public decimal PnLPercent { get; init; }
    public decimal Allocation { get; init; }
    public DateTime BuyDate { get; init; }
    public decimal Brokerage { get; init; }
}

public record CgtReportDto
{
    public decimal TotalCapitalGains { get; init; }
    public decimal TotalCapitalLosses { get; init; }
    public decimal NetCapitalGain { get; init; }
    public decimal DiscountedGain { get; init; }
    public List<CgtHoldingDto> Holdings { get; init; } = new();
}

public record CgtHoldingDto
{
    public int HoldingId { get; init; }
    public string Ticker { get; init; } = "";
    public DateTime BuyDate { get; init; }
    public decimal Quantity { get; init; }
    public decimal BuyPrice { get; init; }
    public decimal CurrentPrice { get; init; }
    public decimal CapitalGain { get; init; }
    public bool EligibleForDiscount { get; init; }
    public decimal DiscountedGain { get; init; }
    public int DaysHeld { get; init; }
    public int CgtDaysRemaining { get; init; }
    public decimal Brokerage { get; init; }
}

public record FetchPricesRequest(List<string> Tickers);
