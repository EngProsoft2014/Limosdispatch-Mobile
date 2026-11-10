namespace LimosDispatch.Core.Models;
public sealed class Passenger
{
    public string Id { get; set; } = "";
    public string CustomerId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string AccountKind { get; set; } = "Individual";
    public string Type { get; set; } = "Individual";
    public string DiscountId { get; set; } = "";
    public string Address { get; set; } = "";
    public string ReferralSource { get; set; } = "";
    public string Contact => string.Join(" · ", new[] { Phone, Email }.Where(v => !string.IsNullOrWhiteSpace(v)));
    public string Initials => string.Concat((Name ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => char.ToUpperInvariant(p[0])));
}
