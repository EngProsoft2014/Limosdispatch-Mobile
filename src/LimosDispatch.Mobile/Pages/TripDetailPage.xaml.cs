using LimosDispatch.Mobile.ViewModels;
namespace LimosDispatch.Mobile.Pages;
public partial class TripDetailPage : ContentPage
{
    private readonly TripDetailViewModel model;
    private readonly IDispatcherTimer timer;
    private readonly GoogleAddresses addresses;
    private string locatedAddress="";
    private bool locating;
    private bool visible;
    private double lastScrollY;
    private CancellationTokenSource? refreshLifetime;
    private readonly RealtimeService realtime;
    public TripDetailPage(TripDetailViewModel model,GoogleAddresses addresses,RealtimeService realtime)
    {
        InitializeComponent(); BindingContext = this.model = model; this.addresses=addresses; this.realtime=realtime;
        model.PropertyChanged+=(_,e)=>{if(e.PropertyName==nameof(model.ExtraFormVisible)&&!model.ExtraFormVisible)HideExtraKeyboard();};
        timer = Dispatcher.CreateTimer(); timer.Interval = TimeSpan.FromSeconds(30);
        timer.Tick += async (_, _) => await RefreshInBackground();
    }
    private void OnStageMapBindingChanged(object? sender, EventArgs e)
    {
        if (sender is ImageButton button)
        {
            button.IsVisible = model?.CanViewStageLocations == true && button.BindingContext is JourneyEvent { HasLocation: true };
            SemanticProperties.SetDescription(button, LocalizationService.Text("View on map"));
        }
    }

    private async void OnStageMapClicked(object? sender, EventArgs e)
    {
        if (!model.CanViewStageLocations ||
            sender is not ImageButton { BindingContext: JourneyEvent { HasLocation: true, StageLocation: { } location } }) return;

        var coordinates = FormattableString.Invariant($"{location.Latitude},{location.Longitude}");
        await LimosDispatch.Core.Helpers.UserFeedback.RunAsync(async () =>
            await Launcher.Default.OpenAsync(new Uri($"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString(coordinates)}")));
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing(); visible=true; model.Subscribe();
        realtime.ForegroundChanged+=OnForeground; ResetRefreshLifetime();
        var expected=refreshLifetime;
        await LimosDispatch.Core.Helpers.UserFeedback.RunAsync(model.LoadAsync);
        if(!visible||expected!=refreshLifetime)return;
        await LimosDispatch.Core.Helpers.UserFeedback.RunAsync(LocatePickup);
        if (visible && expected == refreshLifetime && lastScrollY > 0 && DetailsScroll.ScrollY == 0)
            await DetailsScroll.ScrollToAsync(0, lastScrollY, false);
        ScheduleRefresh();
    }
    private async void OnPullToRefresh(object? sender, EventArgs e)
    {
        if (sender is not RefreshView refresh) return;
        try { await LimosDispatch.Core.Helpers.UserFeedback.RunAsync(model.LoadAsync); }
        finally { refresh.IsRefreshing = false; }
    }

    private void ResetRefreshLifetime()
    {
        refreshLifetime?.Cancel(); refreshLifetime?.Dispose();
        refreshLifetime=visible&&realtime.IsForeground?new():null;
    }
    private void ScheduleRefresh()
    {
        timer.Stop();
        if(!visible||!realtime.IsForeground||refreshLifetime is null||model.IsClosed)return;
        timer.Interval=TimeSpan.FromSeconds(model.Trip.IsActive?30:60); timer.Start();
    }
    private async Task RefreshInBackground()
    {
        if(!visible||!realtime.IsForeground||refreshLifetime is not {} current)return;
        if(model.IsBusy){ScheduleRefresh();return;}
        await LimosDispatch.Core.Helpers.UserFeedback.RunAsync(()=>model.RefreshInBackgroundAsync(current.Token));
        if(current==refreshLifetime)ScheduleRefresh();
    }
    private void OnForeground(bool foreground)=>Dispatcher.Dispatch(()=>
    {
        timer.Stop(); ResetRefreshLifetime();
        if(foreground&&visible)_=RefreshInBackground();
    });
    private void HideExtraKeyboard()
    {
#if ANDROID
        if(ExtraDescriptionEditor.Handler?.PlatformView is global::Android.Views.View native && Platform.CurrentActivity?.GetSystemService(global::Android.Content.Context.InputMethodService) is global::Android.Views.InputMethods.InputMethodManager input)
            input.HideSoftInputFromWindow(native.WindowToken,global::Android.Views.InputMethods.HideSoftInputFlags.None);
#elif IOS
        if(ExtraDescriptionEditor.Handler?.PlatformView is UIKit.UIView native)native.ResignFirstResponder();
#endif
        ExtraDescriptionEditor.Unfocus();ExtraQuantityEntry.Unfocus();
    }
    private async Task LocatePickup()
    {
        if(locating||locatedAddress==model.Trip.From||string.IsNullOrWhiteSpace(model.Trip.From))return;
        locating=true;try {var address=model.Trip.From;var p=await addresses.LocateAsync(address);if(model.Trip.From!=address)return;
            PickupLocationMap.SetMarkers([new Controls.FleetMarker("passenger",model.Trip.Name,address,p.Latitude,p.Longitude,"person")]);
            PickupLocationMap.MoveToRegion(Microsoft.Maui.Maps.MapSpan.FromCenterAndRadius(new Location(p.Latitude,p.Longitude),Microsoft.Maui.Maps.Distance.FromKilometers(1)));locatedAddress=address;
        } finally {locating=false;}
    }
    protected override void OnDisappearing()
    { lastScrollY=DetailsScroll.ScrollY; visible=false; timer.Stop(); realtime.ForegroundChanged-=OnForeground; ResetRefreshLifetime(); model.Unsubscribe(); base.OnDisappearing(); }
}
