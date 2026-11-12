using Microsoft.Maui.Maps;
namespace LimosDispatch.Mobile.Controls;
public sealed record FleetMarker(string Id,string Label,string Detail,double Latitude,double Longitude,string Kind="car")
{
    public double? Bearing { get; init; }

    // The existing car artwork faces down and to the right at zero rotation.
    public double Rotation => Bearing is double heading ? (heading + 207) % 360 : 0;
}
public sealed class FleetMapView : Microsoft.Maui.Controls.Maps.Map
{
    private IReadOnlyList<FleetMarker> destination = [];
    private Dictionary<string,FleetMarker> details = [];
    public MapSpan? LastRegion { get; private set; }
    public bool IsReady { get; private set; }
    public IReadOnlyList<FleetMarker> Markers { get; private set; } = [];
    public IReadOnlyList<IReadOnlyList<Location>> Routes { get; private set; } = [];
    public event Action? SceneChanged;
    public event Action? Ready;
    public void NotifyReady() { IsReady=true; Ready?.Invoke(); if(LastRegion is {} region)MoveToRegion(region); }
    public void UpdateDetails(IReadOnlyList<FleetMarker> target)
    {
        details=target.ToDictionary(m=>m.Id);
        Markers=Markers.Select(Decorate).ToList();SceneChanged?.Invoke();
    }
    private FleetMarker Decorate(FleetMarker marker)=>details.TryGetValue(marker.Id,out var text)?marker with{Label=text.Label,Detail=text.Detail}:marker;
    public void SetMarkers(IReadOnlyList<FleetMarker> target,bool animate=false, bool orientCars=false)
    {
        var same=target.Count==destination.Count&&target.All(t=>destination.Any(d=>d.Id==t.Id&&d.Latitude==t.Latitude&&d.Longitude==t.Longitude&&(orientCars||d.Bearing==t.Bearing)));
        if(same){UpdateDetails(target);return;}
        if (orientCars)
        {
            var previous = destination.ToDictionary(marker => marker.Id);
            target = target.Select(marker => previous.TryGetValue(marker.Id, out var old) && marker.Kind == "car"
                ? marker with { Bearing = TravelBearing(old, marker) }
                : marker).ToList();
        }
        destination=target;details=target.ToDictionary(m=>m.Id);
        this.AbortAnimation("markers");var before=Markers.ToDictionary(x=>x.Id);
        if(!animate||!IsReady){Markers=target;SceneChanged?.Invoke();return;}
        new Animation(progress =>
        {
            Markers = target.Select(marker => Decorate(before.TryGetValue(marker.Id, out var old)
                ? Interpolate(old, marker, progress) : marker)).ToList();
            SceneChanged?.Invoke();
        }, 0, 1).Commit(this, "markers", 32, 1500, Easing.Linear, (_, cancelled) =>
        {
            if (cancelled) return;
            Markers = target.Select(Decorate).ToList();
            SceneChanged?.Invoke();
        });
    }

    private static double? TravelBearing(FleetMarker previous, FleetMarker current)
    {
        if (LimosDispatch.Core.Helpers.TrackingPointFilter.DistanceMeters(
            previous.Latitude, previous.Longitude, current.Latitude, current.Longitude) < 3)
            return previous.Bearing;

        const double radians = Math.PI / 180;
        var start = previous.Latitude * radians;
        var end = current.Latitude * radians;
        var longitude = (current.Longitude - previous.Longitude) * radians;
        var east = Math.Sin(longitude) * Math.Cos(end);
        var north = Math.Cos(start) * Math.Sin(end) - Math.Sin(start) * Math.Cos(end) * Math.Cos(longitude);
        return (Math.Atan2(east, north) / radians + 360) % 360;
    }

    private static FleetMarker Interpolate(FleetMarker previous, FleetMarker target, double progress)
    {
        var bearing = target.Bearing;
        if (previous.Bearing is double start && bearing is double end)
        {
            var turn = (end - start + 540) % 360 - 180;
            bearing = (start + turn * progress + 360) % 360;
        }

        return target with
        {
            Latitude = previous.Latitude + (target.Latitude - previous.Latitude) * progress,
            Longitude = previous.Longitude + (target.Longitude - previous.Longitude) * progress,
            Bearing = bearing
        };
    }
    public void SetRoutes(IReadOnlyList<IReadOnlyList<Location>> routes) { Routes=routes;SceneChanged?.Invoke(); }
    public new void MoveToRegion(MapSpan span)
    {
        LastRegion=span;
#if IOS
        if(Handler is Platforms.iOS.GoogleMapsHandler h)h.MoveTo(span);
#elif ANDROID
        if(Handler is Platforms.Android.FleetMapHandler h)h.MoveTo(span);
#endif
    }
    protected override void OnHandlerChanging(HandlerChangingEventArgs args) { this.AbortAnimation("markers");IsReady=false;base.OnHandlerChanging(args); }
}
