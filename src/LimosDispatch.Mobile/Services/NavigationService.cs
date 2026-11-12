using LimosDispatch.Mobile.Pages;
using LimosDispatch.Mobile.ViewModels;
namespace LimosDispatch.Mobile.Services;
public sealed class NavigationService(IServiceProvider services)
{
    private Page Root => Application.Current!.Windows[0].Page!;
    public bool IsRestoringSession { get; private set; }
    public bool IsWorkspaceOpen => Application.Current?.Windows.FirstOrDefault()?.Page is Shell;
    public Page CreateStartup() { IsRestoringSession = true; return services.GetRequiredService<StartupPage>(); }
    public Page CreateLogin() => new NavigationPage(services.GetRequiredService<LoginPage>()) { BarBackgroundColor = Color.FromArgb("#12202D"), BarTextColor = Colors.White };
    public void OpenWorkspace(UserProfile user) { IsRestoringSession = false; Application.Current!.Windows[0].Page = new AppShell(services, user); services.GetRequiredService<OfflineSyncCoordinator>().Start(); MainThread.BeginInvokeOnMainThread(() => { _ = LimosDispatch.Core.Helpers.UserFeedback.RunAsync(services.GetRequiredService<RideLinkService>().OpenPendingAsync); _ = LimosDispatch.Core.Helpers.UserFeedback.RunAsync(services.GetRequiredService<PushService>().StartSessionAsync); }); }
    public void OpenLogin() { IsRestoringSession = false; if (Application.Current?.Windows.Count > 0) Application.Current.Windows[0].Page = CreateLogin(); }
    public Task PushAsync<T>() where T : Page => Root.Navigation.PushAsync(services.GetRequiredService<T>());
    public async Task PopAsync() => await Root.Navigation.PopAsync();
    public async Task<GoogleAddress?> SelectAddressAsync(string title)
    {
        var page = services.GetRequiredService<AddressSearchPage>();
        page.Configure(title);
        await Root.Navigation.PushModalAsync(page);
        return await page.Result;
    }
    public async Task<Passenger?> SelectPassengerAsync()
    {
        var page = services.GetRequiredService<PassengerSearchPage>();
        await Root.Navigation.PushModalAsync(page);
        return await page.Result;
    }
    public async Task OpenTripAsync(Trip trip)
    {
        var tracking = services.GetRequiredService<TrackingService>();
        if (tracking.ActiveTripId is { } active && active != trip.Id)
            throw new InvalidOperationException("Finish the current journey before opening another trip. Location sharing is still running.");
        var page = services.GetRequiredService<TripDetailPage>();
        ((TripDetailViewModel)page.BindingContext).SetTrip(trip);
        await Root.Navigation.PushAsync(page);
    }
    public async Task EditTripAsync(string id){var page=services.GetRequiredService<CreateRidePage>();((CreateRideViewModel)page.BindingContext).ConfigureEdit(id);await Root.Navigation.PushAsync(page);}
    public async Task ReviewExtraAsync(string tripId,TripExtra extra){var page=services.GetRequiredService<ExtraReviewPage>();((ExtraReviewViewModel)page.BindingContext).Configure(tripId,extra);await Root.Navigation.PushAsync(page);}
    public async Task OpenExpenseAsync(Expense expense){var page=services.GetRequiredService<ExpenseDetailPage>();((ViewModels.ExpenseDetailViewModel)page.BindingContext).Configure(expense);await Root.Navigation.PushAsync(page);}
    public async Task EditExpenseAsync(Expense expense){var page=services.GetRequiredService<AddExpensePage>();((ViewModels.AddExpenseViewModel)page.BindingContext).Configure(expense);await Root.Navigation.PushAsync(page);}
    public async Task OpenTrackingAsync(string tripId)
    {
        var page = services.GetRequiredService<TrackingPage>();
        var model = (TrackingViewModel)page.BindingContext;
        model.TripId = tripId;
        model.MapMode = true;
        await Root.Navigation.PushAsync(page);
    }
    public async Task OpenRouteHistoryAsync(Trip trip)
    {
        var page = services.GetRequiredService<RoutePlaybackPage>();
        page.Configure(trip);
        await Root.Navigation.PushAsync(page);
    }

    public Task<bool> ConfirmAsync(string title, string message) => FeedbackService.ConfirmAsync(message, "Confirm");
}
