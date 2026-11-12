using CoreLocation;
using Google.Maps;
using LimosDispatch.Mobile.Controls;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Maps;
using UIKit;
namespace LimosDispatch.Mobile.Platforms.iOS;
public sealed class GoogleMapsHandler : ViewHandler<FleetMapView,MapView>
{
    private readonly Dictionary<string,Marker> markers=[];
    private readonly List<Polyline> routes=[];
    private IReadOnlyList<IReadOnlyList<Location>>? renderedRoutes;
    public GoogleMapsHandler():base(ViewMapper){}
    protected override MapView CreatePlatformView() { AppDelegate.EnsureGoogleMapsInitialized();return new MapView(new MapViewOptions{Camera=CameraPosition.FromCamera(29.7604,-95.3698,10)}); }
    protected override void ConnectHandler(MapView v) {base.ConnectHandler(v);v.MyLocationEnabled=false;VirtualView.SceneChanged+=Render;VirtualView.NotifyReady();Render();}
    protected override void DisconnectHandler(MapView v) {VirtualView.SceneChanged-=Render;v.Clear();foreach(var m in markers.Values)m.Dispose();markers.Clear();foreach(var p in routes)p.Dispose();routes.Clear();base.DisconnectHandler(v);}
    private void Render()
    {
        var ids=VirtualView.Markers.Select(x=>x.Id).ToHashSet();foreach(var id in markers.Keys.Where(x=>!ids.Contains(x)).ToList()){markers[id].Map=null;markers[id].Dispose();markers.Remove(id);}
        foreach(var p in VirtualView.Markers)
        {
            var pos=new CLLocationCoordinate2D(p.Latitude,p.Longitude);
            if(!markers.TryGetValue(p.Id,out var marker)) {marker=Marker.FromPosition(pos);marker.Icon=UIImage.FromBundle(p.Kind=="person"?"marker_person.png":"marker_car.png");marker.GroundAnchor=new CoreGraphics.CGPoint(.5,p.Kind=="person"?.85:.5);marker.Map=PlatformView;markers[p.Id]=marker;}
            marker.Position=pos;marker.Title=p.Label;marker.Snippet=p.Detail;
            marker.Flat = p.Bearing.HasValue;
            marker.Rotation = p.Rotation;
        }
        if(!ReferenceEquals(renderedRoutes,VirtualView.Routes))
        {
            foreach(var p in routes){p.Map=null;p.Dispose();}routes.Clear();renderedRoutes=VirtualView.Routes;
            foreach(var segment in renderedRoutes.Where(x=>x.Count>1)){using var path=new MutablePath();foreach(var p in segment)path.AddCoordinate(new CLLocationCoordinate2D(p.Latitude,p.Longitude));var line=Polyline.FromPath(path);line.StrokeColor=UIColor.FromRGB(183,154,91);line.StrokeWidth=5;line.Map=PlatformView;routes.Add(line);}
        }
    }
    public void MoveTo(MapSpan region) {var zoom=(float)Math.Clamp(Math.Log2(360/Math.Max(region.LongitudeDegrees,.0001)),2,20);PlatformView.Camera=CameraPosition.FromCamera(region.Center.Latitude,region.Center.Longitude,zoom);}
}
