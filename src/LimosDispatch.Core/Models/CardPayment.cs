namespace LimosDispatch.Core.Models;
public sealed record ChildSeat(string Type, int Quantity);
public sealed class CardSettings
{
    public bool Configured { get; set; }
    public string PublishableKey { get; set; } = "";
    public string Currency { get; set; } = "usd";
}
public sealed class CardPaymentIntent
{
    public bool Paid { get; set; }
    public string Status { get; set; } = "";
    public string ClientSecret { get; set; } = "";
}
