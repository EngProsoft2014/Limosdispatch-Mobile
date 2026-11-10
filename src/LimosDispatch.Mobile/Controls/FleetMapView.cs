using Microsoft.Maui.Maps;
namespace LimosDispatch.Mobile.Controls;
public sealed record FleetMarker(string Id,string Label,string Detail,double Latitude,double Longitude,string Kind="car");
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
    public void SetMarkers(IReadOnlyList<FleetMarker> target,bool animate=false)
    {
        var same=target.Count==destination.Count&&target.All(t=>destination.Any(d=>d.Id==t.Id&&d.Latitude==t.Latitude&&d.Longitude==t.Longitude));
        if(same){UpdateDetails(target);return;}
        destination=target;details=target.ToDictionary(m=>m.Id);
        this.AbortAnimation("markers");var before=Markers.ToDictionary(x=>x.Id);
        if(!animate||!IsReady){Markers=target;SceneChanged?.Invoke();return;}
        new Animation(f=>{Markers=target.Select(t=>Decorate(before.TryGetValue(t.Id,out var old)?t with{Latitude=old.Latitude+(t.Latitude-old.Latitude)*f,Longitude=old.Longitude+(t.Longitude-old.Longitude)*f}:t)).ToList();SceneChanged?.Invoke();},0,1)
            .Commit(this,"markers",32,1500,Easing.Linear,(_,_)=>{Markers=target.Select(Decorate).ToList();SceneChanged?.Invoke();});
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
