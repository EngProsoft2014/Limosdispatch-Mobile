namespace LimosDispatch.Core.Models;

// Preserve the complete driver response; start permission is server-owned.
public sealed class DriverJourney
{
    public Trip Trip { get; set; } = new();
    public bool CanStart { get; set; }
    public bool TrackingAllowed { get; set; }
    public int StartWindowMinutes { get; set; } = 120;
    public long? StartAvailableAt { get; set; }
    public DriverLicense? License { get; set; }
}
public sealed class JourneyEvent
{
    public string Status { get; set; } = "";
    public long At { get; set; }
    public GpsPoint? StageLocation { get; set; }
    public bool HasLocation => StageLocation is { Latitude: >= -90 and <= 90, Longitude: >= -180 and <= 180, CapturedAt: > 0 };
    [System.Text.Json.Serialization.JsonIgnore] public string StageLabel => LimosDispatch.Core.Helpers.TripWorkflow.StageLabel(Status);
    public string LocalTime => At > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(At).ToLocalTime().ToString("g") : "";
}
public sealed class DriverLicense
{
    public string Name { get; set; } = "";
    public string LimoLicenseNumber { get; set; } = "";
    public string LimoLicenseExpiry { get; set; } = "";
}
public sealed record TripExtraRequest(string TripId, string RequestId, string Type, decimal Quantity, string Description);
