using System.Text.Json;
using System.Text.Json.Serialization;
namespace LimosDispatch.Core.Models;

public sealed record LoginRequest(string Company, string Email, string Password);
public sealed class TokenPair
{
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
    public bool MustChangePassword { get; set; }
    public string Company { get; set; } = "";
}
public sealed class Permission { public bool View { get; set; } public bool Add { get; set; } public bool Edit { get; set; } public bool Delete { get; set; } }
public sealed class UserProfile
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "";
    public string Company { get; set; } = "";
    public string? DriverId { get; set; }
    public bool MustChangePassword { get; set; }
    public Dictionary<string, Permission> Permissions { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public bool IsDriver => Role.Equals("Driver", StringComparison.OrdinalIgnoreCase);
    public bool IsAdmin => Role.Equals("Admin", StringComparison.OrdinalIgnoreCase);
    public bool Can(string module, string action = "View") => IsAdmin || Permissions.Any(p =>
        p.Key.Equals(module, StringComparison.OrdinalIgnoreCase) && action switch
        { "Add" => p.Value.Add, "Edit" => p.Value.Edit, "Delete" => p.Value.Delete, _ => p.Value.View });
    public bool CanDispatch => Can("Dispatch") || Can("Reservations");
    public bool CanAssign => Can("Reservations", "Edit");
    public bool CanCreateTrip => Can("Reservations", "Add");
}

// The deployed API mixes JSON numbers and numeric strings, including empty strings.
public sealed class FlexibleDecimalConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type t, JsonSerializerOptions o)
    {
        if (reader.TokenType == JsonTokenType.Number) return reader.GetDecimal();
        if (reader.TokenType == JsonTokenType.Null) return 0;
        if (reader.TokenType == JsonTokenType.String && decimal.TryParse(reader.GetString(), System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var n)) return n;
        if (reader.TokenType == JsonTokenType.String) return 0;
        throw new JsonException("Invalid numeric value");
    }
    public override void Write(Utf8JsonWriter w, decimal v, JsonSerializerOptions o) => w.WriteNumberValue(v);
}
public sealed class Trip
{
    public string PassengerId { get; set; } = "";
    public string CustomerId { get; set; } = "";
    [JsonConverter(typeof(FlexibleDecimalConverter))] public decimal FareBeforeDiscount { get; set; }
    [JsonConverter(typeof(FlexibleDecimalConverter))] public decimal CustomerDiscount { get; set; }
    [JsonConverter(typeof(FlexibleDecimalConverter))] public decimal CouponDiscount { get; set; }
    [JsonConverter(typeof(FlexibleDecimalConverter))] public decimal TotalAfterDiscount { get; set; }
    [JsonConverter(typeof(FlexibleDecimalConverter))] public decimal CreditCardFeeRate { get; set; }
    [JsonConverter(typeof(FlexibleDecimalConverter))] public decimal CreditCardFeeFixed { get; set; }
    [JsonConverter(typeof(FlexibleDecimalConverter))] public decimal CreditCardFee { get; set; }
    public string CouponCode { get; set; } = "";
    public string CouponId { get; set; } = "";
    public JsonElement? CustomerDiscountSnapshot { get; set; }
    public string PaymentMethod { get; set; } = "Cash";
    public List<ChildSeat> ChildSeats { get; set; } = [];
    [JsonPropertyName("childseat")] public string ChildSeatSummary { get; set; } = "Not required";
    [JsonConverter(typeof(FlexibleBooleanConverter))] public bool IsTest { get; set; }
    public string BookingSource { get; set; } = "";
    public List<TripExtra> Extras { get; set; } = [];
    public List<JourneyEvent> History { get; set; } = [];
    public string RouteFrom { get; set; } = "";
    public string RouteTo { get; set; } = "";
    [JsonConverter(typeof(FlexibleDecimalConverter))] public decimal GoogleDistanceMeters { get; set; }
    public string RouteDistanceSource { get; set; } = "";
    [JsonConverter(typeof(FlexibleDecimalConverter))] public decimal PricingRate { get; set; }
    [JsonConverter(typeof(FlexibleDecimalConverter))] public decimal PricingBandFrom { get; set; }
    [JsonConverter(typeof(FlexibleDecimalConverter))] public decimal PricingBandTo { get; set; }
    public string Id { get; set; } = "";
    public string ClientRequestId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public string CustomerEmail { get; set; } = "";
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public string Date { get; set; } = "";
    public string Time { get; set; } = "";
    public string Timezone { get; set; } = "America/Chicago";
    public long PickupAt { get; set; }
    public string Status { get; set; } = "Unassigned";
    public string Paid { get; set; } = "Pending";
    public string DriverId { get; set; } = "";
    public string DriverName { get; set; } = "";
    public string Vehicle { get; set; } = "";
    public string VehicleType { get; set; } = "";
    public string TripType { get; set; } = "Distance";
    public string Service { get; set; } = "";
    public string Notes { get; set; } = "";
    public string Flight { get; set; } = "";
    [JsonConverter(typeof(FlexibleDecimalConverter))] public decimal Price { get; set; }
    [JsonConverter(typeof(FlexibleDecimalConverter))] public decimal Hours { get; set; }
    [JsonConverter(typeof(FlexibleDecimalConverter))] public decimal People { get; set; }
    [JsonConverter(typeof(FlexibleDecimalConverter))] public decimal DistanceMiles { get; set; }
    [JsonIgnore] public string LocalSyncStatus {get;set;}="";
    [JsonIgnore] public string StageLabel => string.IsNullOrEmpty(LocalSyncStatus)?LimosDispatch.Core.Helpers.TripWorkflow.StageLabel(Status):LocalSyncStatus;
    public string Schedule => $"{Date} · {Time} · {Timezone}";
    [JsonIgnore] public bool HasDropoff => TripType != "Hourly" && !string.IsNullOrWhiteSpace(To);
    public string Route => HasDropoff ? $"{From} → {To}" : From;
    public bool IsActive => Status is "Enroute" or "Arrived" or "Onboard";
}
public sealed class TripList { public List<Trip> Trips { get; set; } = []; public long ServerNow { get; set; } public int StartWindowMinutes { get; set; } = 120; }
public sealed class Records<T> { public long Version { get; set; } public List<T> Rows { get; set; } = []; }
public sealed record RecordInput<T>(long Version, T Record);
public sealed class Receipt { public string Name { get; set; } = ""; public string Data { get; set; } = ""; }
public sealed class Expense
{
    public string Id { get; set; } = "";
    public string ClientRequestId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public decimal Amount { get; set; }
    public string Date { get; set; } = "";
    public string Driver { get; set; } = "";
    public string Scope { get; set; } = "General";
    public string Vehicle { get; set; } = "";
    public string CostKind { get; set; } = "Operating expense";
    public string Notes { get; set; } = "";
    public Receipt? Receipt { get; set; }
    [JsonExtensionData] public Dictionary<string,JsonElement>? Additional { get; set; }
    [JsonIgnore] public string ReceiptLabel => Receipt is null ? "No receipt" : "Receipt attached";
    [JsonIgnore] public bool HasReceipt => Receipt is not null;
    [JsonIgnore]public bool HasVehicle=>!string.IsNullOrWhiteSpace(Vehicle);[JsonIgnore]public bool HasNotes=>!string.IsNullOrWhiteSpace(Notes);
}
public sealed class Driver
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string TrackingId { get; set; } = "";
    public string PayrollId { get; set; } = "";
    public string Vehicle { get; set; } = "";
    public string Status { get; set; } = "";
}
public sealed class CompanyInfo { public string Currency { get; set; } = ""; public string Name { get; set; } = ""; public string Address {get;set;}="";public string Phone{get;set;}="";public string Email{get;set;}="";public string Website{get;set;}="";public JsonElement Logo{get;set;} }
public sealed class EarningsEnvelope { public Statement Statement { get; set; } = new(); public CompanyInfo Company { get; set; } = new(); }
public sealed class Statement
{
    public string DriverId {get;set;}="";
    public int SnapshotVersion {get;set;}=2;
    [JsonExtensionData] public Dictionary<string,JsonElement>? Additional {get;set;}
    public string DriverName { get; set; } = "";
    public string Month { get; set; } = "";
    public JsonElement System { get; set; }
    public List<EarningRow> Rows { get; set; } = [];
    public decimal Total { get; set; }
    public bool Finalized { get; set; }
    [JsonIgnore] public bool NeedsReview => Rows.Any(r=>r.NeedsReview);
    [JsonIgnore] public bool IsSalary => System.ValueKind==JsonValueKind.Object && System.TryGetProperty("type",out var t)&&t.GetString()=="salary";
    [JsonIgnore] public string State => Finalized ? "Finalized statement" : "Draft statement";
    [JsonIgnore] public string Contract => System.ValueKind == JsonValueKind.Object && System.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
}
public sealed class EarningRow
{
    public string Id {get;set;}="";public string Name {get;set;}="";public string Date {get;set;}="";public string Time {get;set;}="";public string From {get;set;}="";public string To {get;set;}="";public string Rule {get;set;}="";public string Error {get;set;}="";public string TripType {get;set;}="";
    public string? ExternalSource {get;set;}public string? ExternalReference {get;set;}public string? SourceKind {get;set;}
    [JsonConverter(typeof(NullableMoneyConverter))]public decimal? Amount {get;set;}
    [JsonConverter(typeof(NullableMoneyConverter))]public decimal? Miles {get;set;}
    [JsonConverter(typeof(NullableMoneyConverter))]public decimal? BaseHours {get;set;}
    [JsonConverter(typeof(NullableMoneyConverter))]public decimal? ExtraMinutes {get;set;}
    [JsonConverter(typeof(NullableMoneyConverter))]public decimal? Hours {get;set;}
    [JsonConverter(typeof(NullableMoneyConverter))]public decimal? HourlyRate {get;set;}
    [JsonConverter(typeof(NullableMoneyConverter))]public decimal? Fare {get;set;}
    [JsonExtensionData]public Dictionary<string,JsonElement>? Additional {get;set;}
    [JsonIgnore]public bool NeedsReview=>Amount is null||!string.IsNullOrWhiteSpace(Error);
    [JsonIgnore]public bool HasError=>!string.IsNullOrWhiteSpace(Error);
    [JsonIgnore]public bool HasMiles=>Miles is not null;
    [JsonIgnore]public bool IsHourly=>TripType.Equals("Hourly",StringComparison.OrdinalIgnoreCase);
    [JsonIgnore]public bool IsImported=>!string.IsNullOrWhiteSpace(ExternalSource);
}
public sealed class DriverLocation
{
    public double Latitude { get; set; } = double.NaN;
    public double Longitude { get; set; } = double.NaN;
    public double Accuracy { get; set; }
    public long ReceivedAt { get; set; }
    public long CapturedAt { get; set; }
    public bool Sharing { get; set; }
}
public sealed class TrackedDriver
{
    public string Id { get; set; } = "";
    public string DriverId { get; set; } = "";
    public string Name { get; set; } = "";
    public string DriverName { get; set; } = "";
    public string TripId { get; set; } = "";
    public DriverLocation? Location { get; set; }
    [JsonIgnore] public double Latitude => Location?.Latitude ?? double.NaN;
    [JsonIgnore] public double Longitude => Location?.Longitude ?? double.NaN;
    [JsonIgnore] public double Accuracy => Location?.Accuracy ?? 0;
    [JsonIgnore] public long UpdatedAt => Location?.ReceivedAt ?? 0;
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? DriverName : Name;
    public long LastContactAt { get; set; }
    public string Motion { get; set; } = "";
    public string Freshness => FreshnessAt(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    public string FreshnessAt(long now) => Math.Max(LastContactAt, UpdatedAt) == 0 ? "Update time unavailable" : now - Math.Max(LastContactAt, UpdatedAt) > 90000 ? "Connection delayed or unconfirmed" : Motion == "stationary" ? "Stationary" : Motion == "moving" ? "Moving" : "Recently updated";
}
public sealed class TrackingEnvelope { public List<TrackedDriver> Drivers { get; set; } = []; }
public sealed record GpsPoint(long CapturedAt, double Latitude, double Longitude, double Accuracy)
{
    [JsonIgnore] public double? SpeedMetersPerSecond { get; init; }
}
public sealed record LocationBatch(List<GpsPoint> Points);
