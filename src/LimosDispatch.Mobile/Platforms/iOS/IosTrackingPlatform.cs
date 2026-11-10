using CoreLocation;
using Foundation;
using LimosDispatch.Mobile.Services;
namespace LimosDispatch.Mobile.Platforms.iOS;
public sealed class IosTrackingPlatform : NSObject, ITrackingPlatform
{
    private CLLocationManager? manager;
    public event Action<GpsPoint>? PointCaptured;
    public Task StartAsync()
    {
        manager = new CLLocationManager { DesiredAccuracy = CLLocation.AccuracyBest, DistanceFilter = 3,
            AllowsBackgroundLocationUpdates = true, PausesLocationUpdatesAutomatically = false, ShowsBackgroundLocationIndicator = true,
            ActivityType = CLActivityType.AutomotiveNavigation };
        manager.LocationsUpdated += OnLocations;
        manager.StartUpdatingLocation();
        return Task.CompletedTask;
    }
    private void OnLocations(object? sender, CLLocationsUpdatedEventArgs e)
    {
        foreach (var l in e.Locations)
            if (l.HorizontalAccuracy >= 0) PointCaptured?.Invoke(new GpsPoint((long)(l.Timestamp.SecondsSince1970 * 1000), l.Coordinate.Latitude, l.Coordinate.Longitude, l.HorizontalAccuracy) { SpeedMetersPerSecond=l.Speed>=0?l.Speed:null });
    }
    public void Stop()
    {
        if (manager is null) return;
        manager.StopUpdatingLocation(); manager.LocationsUpdated -= OnLocations; manager.Dispose(); manager = null;
    }
}
