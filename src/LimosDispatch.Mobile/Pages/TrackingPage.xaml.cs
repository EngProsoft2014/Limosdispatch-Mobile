using LimosDispatch.Mobile.ViewModels;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
namespace LimosDispatch.Mobile.Pages;
public partial class TrackingPage : ContentPage
{
    private readonly TrackingViewModel model;
    private bool centered;
    private readonly TrackingHistoryViewModel history;
    private readonly IServiceProvider services;
    private bool historyMode;
    public TrackingPage(TrackingViewModel model,TrackingHistoryViewModel history,IServiceProvider services) { InitializeComponent(); this.history=history;this.services=services;HistoryPanel.BindingContext=history; BindingContext = this.model = model; Shell.SetTitleView(this, new Controls.CenteredBrandHeader { Title = LocalizationService.Text("Tracking") });
        FleetMap.Ready += RenderPins;
        FleetMap.SizeChanged += (_, _) => { if (!centered && model.MapMode) RenderPins(); };
    }
    protected override void OnAppearing() { base.OnAppearing(); model.LocationsChanged += RenderPins; model.StatusChanged += RenderStatus; if(!historyMode)model.Start(); }
    protected override void OnDisappearing() { model.Stop(); model.LocationsChanged -= RenderPins; model.StatusChanged -= RenderStatus; base.OnDisappearing(); }
    private void OnLiveTab(object? sender,EventArgs e){historyMode=false;HistoryPanel.IsVisible=false;LivePanel.IsVisible=true;LiveTab.Style=(Style)Application.Current!.Resources["BaseButton"];HistoryTab.Style=(Style)Application.Current.Resources["Secondary"];model.Start();}
    private async void OnHistoryTab(object? sender,EventArgs e){historyMode=true;model.Stop();model.FullScreen=false;ApplyFullScreen();LivePanel.IsVisible=false;HistoryPanel.IsVisible=true;HistoryTab.Style=(Style)Application.Current!.Resources["BaseButton"];LiveTab.Style=(Style)Application.Current.Resources["Secondary"];await history.LoadAsync();}
    private async void OnHistoryDriverTapped(object? sender,TappedEventArgs e)
    {
        if(sender is BindableObject {BindingContext:Driver driver})await LimosDispatch.Core.Helpers.UserFeedback.RunAsync(async()=>{var page=services.GetRequiredService<DriverHistoryPage>();page.Configure(driver);await Navigation.PushAsync(page);});
    }
    private void OnListClicked(object? sender, EventArgs e) { model.MapMode = false; ApplyFullScreen(); }
    private void OnMapClicked(object? sender, EventArgs e) { model.MapMode = true; centered = false; Dispatcher.Dispatch(RenderPins); }
    private void OnExpandClicked(object? sender, EventArgs e) { model.FullScreen = !model.FullScreen; ApplyFullScreen(); }
    private void ApplyFullScreen()
    {
        TrackingTabs.IsVisible=!model.FullScreen;
        Shell.SetNavBarIsVisible(this, !model.FullScreen);
        Shell.SetTabBarIsVisible(this, !model.FullScreen);
        ExpandButton.Source = model.FullScreen ? "collapse.png" : "expand.png";
        SemanticProperties.SetDescription(ExpandButton, model.FullScreen ? "Exit full screen map" : "Full screen map");
    }
    protected override bool OnBackButtonPressed()
    {
        return TryExitFullScreen() || base.OnBackButtonPressed();
    }
    public bool TryExitFullScreen() { if (!model.FullScreen) return false; model.FullScreen = false; ApplyFullScreen(); return true; }
    private void RenderPins() => RenderPins(true);
    private void RenderStatus() => FleetMap.UpdateDetails(model.Drivers.Select(d => new Controls.FleetMarker(d.Id,d.DisplayName,LocalizationService.Text(d.Freshness),d.Latitude,d.Longitude)).ToList());
    private void RenderPins(bool animate)
    {
        FleetMap.SetMarkers(model.Drivers.Select(d => new Controls.FleetMarker(d.Id,d.DisplayName,LocalizationService.Text(d.Freshness),d.Latitude,d.Longitude)).ToList(),animate);
        if (!centered && model.MapMode && FleetMap.IsReady && model.Drivers.Count > 0)
        { var first = model.Drivers[0]; centered = true; FleetMap.MoveToRegion(MapSpan.FromCenterAndRadius(new Location(first.Latitude, first.Longitude), Distance.FromKilometers(15))); }
    }
    private void OnDriverTapped(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject { BindingContext: TrackedDriver driver })
        { model.MapMode = true; centered=true; FleetMap.MoveToRegion(MapSpan.FromCenterAndRadius(new Location(driver.Latitude, driver.Longitude), Distance.FromKilometers(2))); }
    }
}

