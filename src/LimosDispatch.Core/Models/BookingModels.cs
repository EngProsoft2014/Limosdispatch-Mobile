namespace LimosDispatch.Core.Models;
public sealed class FleetVehicle
{
    public string Name { get; set; } = "";
    public string Plate { get; set; } = "";
    public string Status { get; set; } = "";
    public string Category { get; set; } = "";
    public string DisplayName => string.IsNullOrWhiteSpace(Plate) ? Name : $"{Name} · {Plate}";
}
public sealed class TierPricing
{
    public List<string> Types { get; set; } = [];
    public List<PriceBand> Distance { get; set; } = [];
    public List<PriceBand> Hourly { get; set; } = [];
    public TierFare Quote(string mode, string vehicleType, decimal quantity)
    {
        if (mode is not ("Distance" or "Hourly") || quantity <= 0) throw new InvalidOperationException("Choose a journey type and positive duration or distance.");
        var index = Types.IndexOf(vehicleType);
        if (index < 0) throw new InvalidOperationException("Choose a configured vehicle type.");
        var bands = (mode == "Hourly" ? Hourly : Distance).OrderBy(b => b.From).ToList();
        var band = bands.FirstOrDefault(b => quantity >= b.From && (quantity < b.To || b == bands.Last() && quantity == b.To));
        if (band is null || band.Prices.Count <= index || band.Prices[index] < 0) throw new InvalidOperationException("No price is configured for this distance or duration and vehicle type.");
        var rate = band.Prices[index];
        return new TierFare(rate, band.From, band.To, decimal.Round(rate * (mode == "Hourly" ? quantity : 1), 2, MidpointRounding.AwayFromZero));
    }
}
public sealed class PriceBand
{
    public decimal From { get; set; }
    public decimal To { get; set; }
    public List<decimal> Prices { get; set; } = [];
}
public sealed record TierFare(decimal Rate, decimal From, decimal To, decimal Total);
public sealed class TripExtra
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    [System.Text.Json.Serialization.JsonConverter(typeof(FlexibleDecimalConverter))] public decimal Quantity { get; set; }
    public string Unit { get; set; } = "";
    public string Description { get; set; } = "";
    public string Status { get; set; } = "";
    public long RequestedAt { get; set; }
    public string RequestedBy { get; set; } = "";
    public long? ReviewedAt { get; set; }
    public string ReviewedBy { get; set; } = "";
    public string ReviewNote { get; set; } = "";
    public decimal Amount { get; set; }
    public string RequestedTime => RequestedAt > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(RequestedAt).ToLocalTime().ToString("g") : "";
    public string ReviewedTime => ReviewedAt is > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(ReviewedAt.Value).ToLocalTime().ToString("g") : "";
    [System.Text.Json.Serialization.JsonIgnore] public bool IsPending=>Status=="Pending";
    public bool IsApproved => Status == "Approved";
    public bool IsReviewed => ReviewedAt is > 0;
}
public sealed record RideNotice(string Key, string Title, string Description, Trip Trip, long CreatedAt);
