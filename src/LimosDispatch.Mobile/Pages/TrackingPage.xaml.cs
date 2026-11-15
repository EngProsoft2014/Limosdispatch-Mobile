using LimosDispatch.Mobile.ViewModels;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
namespace LimosDispatch.Mobile.Pages;
public partial class TrackingPage : ContentPage
{
    private readonly TrackingViewModel model;
    private bool centered, visible, lookingUpStreet;
    private string? followedId;
    private Location? lastStreetLocation, lastCameraLocation;
    private DateTimeOffset lastStreetLookup;

    private readonly TrackingHistoryViewModel history;
    private readonly IServiceProvider services;
    private bool historyMode;
    public TrackingPage(TrackingViewModel model,TrackingHistoryViewModel history,IServiceProvider services) { InitializeComponent(); this.history=history;this.services=services;HistoryPanel.BindingContext=history; BindingContext = this.model = model; Shell.SetTitleView(this, new Controls.CenteredBrandHeader { Title = LocalizationService.Text("Tracking") });
        FleetMap.Ready += RenderPins;
        FleetMap.SizeChanged += (_, _) => { if (!centered && model.MapMode) RenderPins(); };
    }
    protected override void OnAppearing() { base.OnAppearing(); visible = true; model.LocationsChanged += RenderPins; model.StatusChanged += RenderStatus; if(!historyMode)model.Start(); }
    protected override void OnDisappearing() { visible = false; model.Stop(); model.LocationsChanged -= RenderPins; model.StatusChanged -= RenderStatus; base.OnDisappearing(); }
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
        FleetMap.SetMarkers(model.Drivers.Select(d => new Controls.FleetMarker(d.Id,d.DisplayName,LocalizationService.Text(d.Freshness),d.Latitude,d.Longitude)).ToList(), animate, orientCars: true);
        if (!model.MapMode || !FleetMap.IsReady || model.Drivers.Count == 0)
        {
            VehicleStreetCard.IsVisible = false;
            return;
        }
        if (!centered)
        {
            var first = model.Drivers[0];
            centered = true;
            if (!string.IsNullOrWhiteSpace(model.TripId)) FocusDriver(first);
            else FleetMap.MoveToRegion(MapSpan.FromCenterAndRadius(new Location(first.Latitude, first.Longitude), Distance.FromKilometers(15)));
        }
        var driver = model.Drivers.FirstOrDefault(d => d.Id == followedId);
        if (driver is null) { VehicleStreetCard.IsVisible = false; return; }
        VehicleStreetCard.IsVisible = true;
        FocusedVehicleName.Text = driver.DisplayName;
        if (lastCameraLocation is null || lastCameraLocation.Latitude != driver.Latitude || lastCameraLocation.Longitude != driver.Longitude)
        {
            lastCameraLocation = new Location(driver.Latitude, driver.Longitude);
            FleetMap.Recenter(lastCameraLocation);
        }
        _ = UpdateStreetAsync(driver);
    }

    private void FocusDriver(TrackedDriver driver)
    {
        if (followedId != driver.Id)
        {
            followedId = driver.Id;
            lastStreetLocation = null;
            lastStreetLookup = default;
            FocusedStreet.IsVisible = false;
            lastCameraLocation = new Location(driver.Latitude, driver.Longitude);
            FleetMap.MoveToRegion(MapSpan.FromCenterAndRadius(lastCameraLocation, Distance.FromMeters(200)));
        }
    }

    private async Task UpdateStreetAsync(TrackedDriver driver)
    {
        if (!visible || lookingUpStreet || DateTimeOffset.UtcNow - lastStreetLookup < TimeSpan.FromSeconds(20)) return;
        if (lastStreetLocation is { } last && LimosDispatch.Core.Helpers.TrackingPointFilter.DistanceMeters(
            last.Latitude, last.Longitude, driver.Latitude, driver.Longitude) < 60) return;
        lookingUpStreet = true;
        lastStreetLookup = DateTimeOffset.UtcNow;
        var location = new Location(driver.Latitude, driver.Longitude);
        try
        {
            var matches = await Geocoding.Default.GetPlacemarksAsync(location.Latitude, location.Longitude);
            if (!visible || followedId != driver.Id || !model.MapMode) return;
            var place = matches?.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.Thoroughfare));
            lastStreetLocation = place is null ? null : location;
            FocusedStreet.Text = place is null ? "" : string.Join(", ", new[] { place.Thoroughfare, place.Locality }.Where(s => !string.IsNullOrWhiteSpace(s)));
            FocusedStreet.IsVisible = place is not null;
        }
        catch (Exception) { if (visible && followedId == driver.Id) FocusedStreet.IsVisible = false; }
        finally { lookingUpStreet = false; }
    }

    private void OnDriverTapped(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject { BindingContext: TrackedDriver driver })
        {
            model.MapMode = true;
            centered = true;
            FocusDriver(driver);
            RenderPins(false);
        }
    }
}
