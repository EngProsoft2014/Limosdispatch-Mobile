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
    private bool notificationPromptShown, notificationPromptInProgress;
    private string? estimatedRoute;
    private readonly PushService push;
    public TripDetailPage(TripDetailViewModel model,GoogleAddresses addresses,RealtimeService realtime, PushService push)
    {
        InitializeComponent(); BindingContext = this.model = model; this.addresses=addresses; this.realtime=realtime; this.push=push;
        EnableCopy(PassengerCopyLabel);
        EnableCopy(PickupCopyLabel);
        EnableCopy(DropoffCopyLabel);
        model.PropertyChanged+=(_,e)=>{if(e.PropertyName==nameof(model.ExtraFormVisible)&&!model.ExtraFormVisible)HideExtraKeyboard();};
        timer = Dispatcher.CreateTimer(); timer.Interval = TimeSpan.FromSeconds(30);
        timer.Tick += async (_, _) => await RefreshInBackground();
    }
    private async void OnAdjustFare(object? sender, EventArgs e) => await model.AdjustFareAsync();

    private static void EnableCopy(Label label)
    {
        var copy = new Command(async () =>
        {
            var value = label.Text;
            if (string.IsNullOrWhiteSpace(value)) return;
            await LimosDispatch.Core.Helpers.UserFeedback.RunAsync(async () =>
            {
                await Clipboard.Default.SetTextAsync(value);
                await FeedbackService.ShowCopiedAsync();
            });
        });
        label.Behaviors.Add(new CommunityToolkit.Maui.Behaviors.TouchBehavior
        {
            Command = copy,
            LongPressCommand = copy,
            LongPressDuration = 500
        });
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
        _ = RequestNotificationsAsync();
        await LimosDispatch.Core.Helpers.UserFeedback.RunAsync(model.LoadAsync);
        if(!visible||expected!=refreshLifetime)return;
        await LimosDispatch.Core.Helpers.UserFeedback.RunAsync(LocatePickup);
        if (visible && expected == refreshLifetime && lastScrollY > 0 && DetailsScroll.ScrollY == 0)
            await DetailsScroll.ScrollToAsync(0, lastScrollY, false);
        ScheduleRefresh();
        _ = UpdateRouteEstimateAsync(expected?.Token ?? CancellationToken.None);
    }

    private async Task RequestNotificationsAsync()
    {
        if (notificationPromptShown || notificationPromptInProgress || !model.ShouldRemindAboutNotifications) return;
        notificationPromptInProgress = true;
        try
        {
            await LimosDispatch.Core.Helpers.UserFeedback.RunAsync(async () =>
            {
                if (await PushService.CanNotifyAsync(false) || !visible) return;
                notificationPromptShown = true;
                if (await PushService.CanNotifyAsync(true))
                {
                    await push.RegisterAsync(false);
                    return;
                }
                if (visible && await DisplayAlertAsync(
                        LocalizationService.Text("Enable notifications"),
                        LocalizationService.Text("Notifications are disabled in your phone settings."),
                        LocalizationService.Text("Settings"), LocalizationService.Text("Cancel")))
                    AppInfo.Current.ShowSettingsUI();
            });
        }
        finally { notificationPromptInProgress = false; }
    }

    private async Task UpdateRouteEstimateAsync(CancellationToken ct)
    {
        var trip = model.Trip;
        var key = trip.HasDropoff ? trip.Route : "";
        if (key == estimatedRoute) return;
        RouteEstimate.IsVisible = false;
        if (key.Length == 0 || string.IsNullOrWhiteSpace(trip.From)) { estimatedRoute = key; return; }
        var storedMiles = trip.GoogleDistanceMeters > 0 ? trip.GoogleDistanceMeters / 1609.344m : trip.DistanceMiles;
        if (storedMiles > 0)
        {
            RouteEstimate.Text = LocalizationService.Format("Estimated route: {0}", LocalizationService.Format("{0} mi", FormattableString.Invariant($"{storedMiles:0.##}")));
            RouteEstimate.IsVisible = true;
        }
        try
        {
            var route = await addresses.RouteAsync(trip.From, trip.To, ct);
            if (ct.IsCancellationRequested || !visible || model.Trip.Route != key) return;
            var miles = LocalizationService.Format("{0} mi", FormattableString.Invariant($"{route.Meters / 1609.344:0.##}"));
            var minutes = route.DurationSeconds > 0 ? " · " + LocalizationService.Format("{0} min", FormattableString.Invariant($"{Math.Ceiling(route.DurationSeconds / 60)}")) : "";
            RouteEstimate.Text = LocalizationService.Format("Estimated route: {0}", miles + minutes);
            RouteEstimate.IsVisible = true;
            estimatedRoute = key;
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex.GetType().Name); }
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
        if(foreground&&visible) { _=RefreshInBackground(); }
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
