using LimosDispatch.Core.Models;
namespace LimosDispatch.Core.Helpers;
/// <summary>Coalesces location providers; one initial position, then measured movement only.</summary>
public sealed class TrackingPointFilter
{
    public GpsPoint? LastAccepted { get; private set; }
    private long lastMovement;
    public void Reset(GpsPoint? previous = null) { LastAccepted = previous; lastMovement = 0; }
    public bool IsMoving(long now) => lastMovement > 0 && now >= lastMovement && now - lastMovement <= 10000;
    public bool Accept(GpsPoint point)
    {
        if (!double.IsFinite(point.Latitude) || !double.IsFinite(point.Longitude) ||
            !double.IsFinite(point.Accuracy) || point.Accuracy < 0 ||
            Math.Abs(point.Latitude) > 90 || Math.Abs(point.Longitude) > 180 ||
            point.CapturedAt <= 0 || point.CapturedAt > 253402300799999) return false;
        if (LastAccepted is { } last)
        {
            if (point.CapturedAt - last.CapturedAt < 2000) return false;
            var distance = DistanceMeters(last.Latitude, last.Longitude, point.Latitude, point.Longitude);
            var noise = Math.Max(3, Math.Min(10, Math.Max(last.Accuracy, point.Accuracy) / 2));
            if (distance < noise) return false;
            // A low speed fix is usually stationary jitter. Large measured displacement can still
            // establish movement when a provider has no useful speed estimate (including emulators).
            if (point.SpeedMetersPerSecond is >= 0 and < 0.5 && distance < Math.Max(15, point.Accuracy * 2)) return false;
            lastMovement = point.CapturedAt;
        }
        LastAccepted = point; return true;
    }
    public static double DistanceMeters(double latitude1, double longitude1, double latitude2, double longitude2)
    {
        const double rad = Math.PI / 180;
        var lat = (latitude2 - latitude1) * rad; var lon = (longitude2 - longitude1) * rad;
        var h = Math.Pow(Math.Sin(lat / 2), 2) + Math.Cos(latitude1 * rad) * Math.Cos(latitude2 * rad) * Math.Pow(Math.Sin(lon / 2), 2);
        return 6371000 * 2 * Math.Asin(Math.Sqrt(Math.Clamp(h, 0, 1)));
    }
}
