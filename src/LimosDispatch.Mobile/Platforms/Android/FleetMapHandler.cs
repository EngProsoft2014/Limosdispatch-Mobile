using Android.Gms.Maps;
using Android.Gms.Maps.Model;
using Android.Graphics;
using LimosDispatch.Mobile.Controls;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Maps;
using AMapView=Android.Gms.Maps.MapView;
namespace LimosDispatch.Mobile.Platforms.Android;
public sealed class FleetMapHandler : ViewHandler<FleetMapView,AMapView>
{
    private GoogleMap? map;
    private ReadyCallback? callback;
    private readonly Dictionary<string,Marker> markers=[];
    private readonly List<Polyline> routes=[];
    private IReadOnlyList<IReadOnlyList<Microsoft.Maui.Devices.Sensors.Location>>? renderedRoutes;
    private readonly Dictionary<string,BitmapDescriptor> icons=[];
    public FleetMapHandler():base(ViewMapper){}
    protected override AMapView CreatePlatformView() { var v=new AMapView(Context);v.OnCreate(null);v.OnResume();return v; }
    protected override void ConnectHandler(AMapView v) {base.ConnectHandler(v);VirtualView.SceneChanged+=Render;callback=new(this);v.GetMapAsync(callback);}
    protected override void DisconnectHandler(AMapView v)
    {
        VirtualView.SceneChanged-=Render;map=null;foreach(var m in markers.Values){m.Remove();m.Dispose();}markers.Clear();foreach(var p in routes){p.Remove();p.Dispose();}routes.Clear();
        foreach(var i in icons.Values)i.Dispose();icons.Clear();callback?.Dispose();callback=null;v.OnPause();v.OnDestroy();base.DisconnectHandler(v);
    }
    private sealed class ReadyCallback(FleetMapHandler owner):Java.Lang.Object,IOnMapReadyCallback
    {public void OnMapReady(GoogleMap google) {if(owner.VirtualView is null)return;owner.map=google;google.UiSettings.ZoomControlsEnabled=true;google.UiSettings.MapToolbarEnabled=false;owner.VirtualView.NotifyReady();owner.Render();}}
    private BitmapDescriptor Icon(string kind)
    {
        if(icons.TryGetValue(kind,out var result))return result;
        int id=Context.Resources!.GetIdentifier(kind=="person"?"marker_person":"marker_car","drawable",Context.PackageName);
        using var original=BitmapFactory.DecodeResource(Context.Resources,id)!;
        int size=(int)((kind=="person"?56:64)*Context.Resources.DisplayMetrics!.Density);
        using var scaled=Bitmap.CreateScaledBitmap(original,size,(int)Math.Round((double)size*original.Height/original.Width),true);
        return icons[kind]=BitmapDescriptorFactory.FromBitmap(scaled);
    }
    private void Render()
    {
        if(map is null)return;
        var ids=VirtualView.Markers.Select(x=>x.Id).ToHashSet();foreach(var id in markers.Keys.Where(x=>!ids.Contains(x)).ToList()){markers[id].Remove();markers[id].Dispose();markers.Remove(id);}
        foreach(var p in VirtualView.Markers)
        {
            var pos=new LatLng(p.Latitude,p.Longitude);
            if(!markers.TryGetValue(p.Id,out var marker)) { using var options=new MarkerOptions();options.SetPosition(pos).SetTitle(p.Label).SetSnippet(p.Detail).SetIcon(Icon(p.Kind)).Anchor(.5f,p.Kind=="person"?.85f:.5f);marker=map.AddMarker(options);if(marker is null)continue;markers[p.Id]=marker; }
            else {marker.Position=pos;marker.Title=p.Label;marker.Snippet=p.Detail;}
        }
        if(!ReferenceEquals(renderedRoutes,VirtualView.Routes))
        {
            foreach(var p in routes){p.Remove();p.Dispose();}routes.Clear();renderedRoutes=VirtualView.Routes;
            foreach(var segment in renderedRoutes.Where(x=>x.Count>1)) {using var options=new PolylineOptions(); options.InvokeColor(global::Android.Graphics.Color.Rgb(183,154,91)).InvokeWidth(6*Context.Resources!.DisplayMetrics!.Density);foreach(var p in segment)options.Add(new LatLng(p.Latitude,p.Longitude));var line=map.AddPolyline(options);if(line is not null)routes.Add(line);}
        }
    }
    public void MoveTo(MapSpan region)
    {
        if(map is null)return;
        var zoom=(float)Math.Clamp(Math.Log2(360/Math.Max(region.LongitudeDegrees,.0001)),2,20);
        map.MoveCamera(CameraUpdateFactory.NewLatLngZoom(new LatLng(region.Center.Latitude,region.Center.Longitude),zoom));
    }
}
