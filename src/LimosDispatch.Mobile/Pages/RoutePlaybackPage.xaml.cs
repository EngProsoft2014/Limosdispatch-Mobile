using LimosDispatch.Mobile.ViewModels;
using LimosDispatch.Mobile.Controls;
using LimosDispatch.Core.Helpers;
using Microsoft.Maui.Maps;
using System.Diagnostics;
namespace LimosDispatch.Mobile.Pages;
public partial class RoutePlaybackPage:ContentPage
{
    private readonly RoutePlaybackViewModel model;private readonly IDispatcherTimer timer;private readonly Stopwatch clock=new();private long lastTick;private bool fullScreen;
    public RoutePlaybackPage(RoutePlaybackViewModel model)
    {
        InitializeComponent();BindingContext=this.model=model;Shell.SetTitleView(this,new CenteredBrandHeader{Title=""});
        timer=Dispatcher.CreateTimer();timer.Interval=TimeSpan.FromMilliseconds(100);timer.Tick+=(_,_)=>{var now=clock.ElapsedMilliseconds;model.Tick(now-lastTick);lastTick=now;};
        model.Loaded+=DrawRoute;model.PositionChanged+=DrawPosition;RouteMap.Ready+=Fit;
    }
    public void Configure(Trip trip)=>model.Trip=trip;
    protected override async void OnAppearing(){base.OnAppearing();clock.Restart();lastTick=0;timer.Start();if(!model.HasPoints)await model.LoadAsync();}
    protected override void OnDisappearing(){timer.Stop();clock.Stop();model.Playing=false;base.OnDisappearing();}
    private void DrawRoute()
    {
        var segments=new List<IReadOnlyList<Location>>();var current=new List<Location>();GpsPoint? last=null;
        foreach(var p in model.Points){if(last is not null&&p.CapturedAt-last.CapturedAt>120000){segments.Add(current);current=[];}current.Add(new(p.Latitude,p.Longitude));last=p;}if(current.Count>0)segments.Add(current);
        RouteMap.SetRoutes(segments);Fit();DrawPosition();
    }
    private void DrawPosition()
    {
        if(!model.HasPoints){RouteMap.SetMarkers([]);return;}var p=RoutePlayback.At(model.Points,model.CurrentTimestamp, out var bearing);
        RouteMap.SetMarkers([new FleetMarker("replay",model.Trip.DriverName,model.CurrentTime,p.Latitude,p.Longitude) { Bearing = bearing }]);
        if(model.Follow)RouteMap.MoveToRegion(MapSpan.FromCenterAndRadius(new Location(p.Latitude,p.Longitude),Distance.FromKilometers(.8)));
    }
    private void Fit()
    {
        if(!model.HasPoints)return;var minLat=model.Points.Min(p=>p.Latitude);var maxLat=model.Points.Max(p=>p.Latitude);var minLng=model.Points.Min(p=>p.Longitude);var maxLng=model.Points.Max(p=>p.Longitude);
        RouteMap.MoveToRegion(new MapSpan(new Location((minLat+maxLat)/2,(minLng+maxLng)/2),Math.Max(.015,(maxLat-minLat)*1.5),Math.Max(.015,(maxLng-minLng)*1.5)));
    }
    private void OnFullScreen(object? sender,EventArgs e){fullScreen=!fullScreen;ApplyFullScreen();}
    private void ApplyFullScreen()
    {
        Shell.SetNavBarIsVisible(this,!fullScreen);Shell.SetTabBarIsVisible(this,!fullScreen);
        SummaryCard.IsVisible=!fullScreen;PlaybackPanel.IsVisible=!fullScreen;
        FullScreenActions.IsVisible=fullScreen;FullScreenTransport.IsVisible=fullScreen&&model.HasPoints;
        Grid.SetRowSpan(RouteMap,fullScreen?2:1);
    }
    public bool TryExitFullScreen(){if(!fullScreen)return false;fullScreen=false;ApplyFullScreen();return true;}
    protected override bool OnBackButtonPressed()=>TryExitFullScreen()||base.OnBackButtonPressed();
    private void OnFit(object? sender,EventArgs e){model.Follow=false;Fit();}
    private void OnPlay(object? sender,EventArgs e)=>model.Toggle();
    private void OnStart(object? sender,EventArgs e){model.Playing=false;model.Position=0;DrawPosition();}
    private void OnEnd(object? sender,EventArgs e){model.Playing=false;model.Position=model.Maximum;DrawPosition();}
    private void OnScrub(object? sender,EventArgs e)=>model.Playing=false;
    private void OnEvents(object? sender,EventArgs e)=>model.ShowEvents=!model.ShowEvents;
    private void OnEvent(object? sender,TappedEventArgs e){if(sender is BindableObject{BindingContext:PlaybackEvent entry}&&model.HasPoints){model.Playing=false;model.Position=entry.At-model.Points[0].CapturedAt;DrawPosition();}}
}
