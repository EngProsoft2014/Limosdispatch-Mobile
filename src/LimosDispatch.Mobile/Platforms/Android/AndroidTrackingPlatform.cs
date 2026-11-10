using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Locations;
using Android.OS;
using Android.Runtime;
using LimosDispatch.Mobile.Services;
using ALocation = Android.Locations.Location;
namespace LimosDispatch.Mobile.Platforms.Android;
public sealed class AndroidTrackingPlatform : ITrackingPlatform
{
    internal static event Action<GpsPoint>? LocationReceived;
    public event Action<GpsPoint>? PointCaptured;
    public Task StartAsync()
    {
        LocationReceived += Forward;
        var context = global::Android.App.Application.Context;
        try { context.StartForegroundService(new Intent(context, typeof(JourneyLocationService))); }
        catch { LocationReceived -= Forward; throw; }
        return Task.CompletedTask;
    }
    private void Forward(GpsPoint p) => PointCaptured?.Invoke(p);
    public void Stop()
    { LocationReceived -= Forward; var context = global::Android.App.Application.Context; context.StopService(new Intent(context, typeof(JourneyLocationService))); }
    internal static void Emit(GpsPoint point) => LocationReceived?.Invoke(point);
}
[Service(Exported = false, ForegroundServiceType = ForegroundService.TypeLocation)]
public sealed class JourneyLocationService : Service, ILocationListener
{
    private LocationManager? manager;
    public override IBinder? OnBind(Intent? intent) => null;
    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        const string channel = "journey-location";
        var notifications = (NotificationManager)GetSystemService(NotificationService)!;
        notifications.CreateNotificationChannel(new NotificationChannel(channel, "Active journey location", NotificationImportance.Low));
        var launchIntent = PackageManager!.GetLaunchIntentForPackage(PackageName!)!;
        var pending = PendingIntent.GetActivity(this, 0, launchIntent, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
        var notification = new Notification.Builder(this, channel).SetContentTitle("LimosDispatch · journey active")
            .SetContentText("Sharing your location with dispatch during this journey.")
            .SetSmallIcon(global::Android.Resource.Drawable.IcMenuMyLocation).SetOngoing(true).SetContentIntent(pending).Build();
        if (OperatingSystem.IsAndroidVersionAtLeast(29)) StartForeground(731, notification, ForegroundService.TypeLocation);
        else StartForeground(731, notification);
        manager = (LocationManager)GetSystemService(LocationService)!;
        try
        {
            SubscribeProvider();
        }
        catch (Java.Lang.SecurityException) { StopSelf(); }
        // Do not resurrect a service without its authenticated trip context after process death.
        return StartCommandResult.NotSticky;
    }
    public void OnLocationChanged(ALocation location)
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(31) ? location.Mock : location.IsFromMockProvider) return;
        AndroidTrackingPlatform.Emit(new GpsPoint(location.Time, location.Latitude, location.Longitude, location.Accuracy) { SpeedMetersPerSecond=location.HasSpeed?location.Speed:null });
    }
    private void SubscribeProvider()
    {
        if (manager is null) return;
        manager.RemoveUpdates(this);
        // Preserve high GPS accuracy and a lower-frequency network fallback indoors.
        // The shared filter coalesces both providers before storage and upload.
        foreach (var provider in new[] { LocationManager.GpsProvider, LocationManager.NetworkProvider })
            if (manager.IsProviderEnabled(provider)) manager.RequestLocationUpdates(provider, provider==LocationManager.GpsProvider?1000:5000, 3, this, Looper.MainLooper);
    }
    private void RefreshProvider()
    {
        try { SubscribeProvider(); } catch (Java.Lang.SecurityException) { StopSelf(); }
    }
    public void OnProviderDisabled(string provider) => RefreshProvider();
    public void OnProviderEnabled(string provider) => RefreshProvider();
    public void OnStatusChanged(string? provider, [GeneratedEnum] Availability status, Bundle? extras) { }
    public override void OnDestroy() { manager?.RemoveUpdates(this); StopForeground(StopForegroundFlags.Remove); base.OnDestroy(); }
}
