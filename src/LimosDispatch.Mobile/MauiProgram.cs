using LimosDispatch.Core.Constants;
using LimosDispatch.Core.GenericRepository;
using LimosDispatch.Mobile.Pages;
using LimosDispatch.Mobile.ViewModels;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Maui;
using Microsoft.Maui.LifecycleEvents;
namespace LimosDispatch.Mobile;
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>().UseMauiCommunityToolkit().UseMauiMaps().ConfigureFonts(fonts =>
        { fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular"); fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold"); fonts.AddFont("ElMessiri-Regular.ttf", "ElMessiriRegular"); fonts.AddFont("ElMessiri-Bold.ttf", "ElMessiriBold"); });
        builder.ConfigureLifecycleEvents(events =>
        {
#if ANDROID
            events.AddAndroid(android => android.OnCreate((activity, _) =>
            {
                InitializeFirebase(() =>
                {
                    Plugin.Firebase.Core.Platforms.Android.CrossFirebase.Initialize(activity, () => Platform.CurrentActivity!);
                    Platforms.Android.AndroidPush.Configure();
                    IPlatformApplication.Current!.Services.GetRequiredService<PushService>().Initialize();
                });
            }));
#elif IOS
            events.AddiOS(ios => ios.WillFinishLaunching((_, _) =>
            {
                InitializeFirebase(() =>
                {
                    Plugin.Firebase.Core.Platforms.iOS.CrossFirebase.Initialize();
                    Plugin.Firebase.CloudMessaging.FirebaseCloudMessagingImplementation.Initialize(skipApnsDeviceTokenRequest: true);
                    IPlatformApplication.Current!.Services.GetRequiredService<PushService>().Initialize();
                    Platforms.iOS.SafeNotificationDelegate.Install();
                });
                return false;
            }));
#endif
        });
#if ANDROID
        builder.ConfigureMauiHandlers(handlers => handlers.AddHandler<Controls.FleetMapView, Platforms.Android.FleetMapHandler>());
#elif IOS
        builder.ConfigureMauiHandlers(handlers => handlers.AddHandler<Controls.FleetMapView, Platforms.iOS.GoogleMapsHandler>());
#endif
        // XAML supplies the rounded field surface; remove the platform underline.
#if ANDROID
        Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping(nameof(Entry.HorizontalTextAlignment), (handler, _) =>
            handler.PlatformView.Gravity = (LocalizationService.Instance.Code == "ar" ? Android.Views.GravityFlags.Right : Android.Views.GravityFlags.Left) | Android.Views.GravityFlags.CenterVertical);
        Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("FieldSurface", (handler, _) =>
            handler.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent));
        Microsoft.Maui.Handlers.PickerHandler.Mapper.AppendToMapping("FieldSurface", (handler, _) =>
            handler.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent));
        Microsoft.Maui.Handlers.DatePickerHandler.Mapper.AppendToMapping("FieldSurface", (handler, _) =>
            handler.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent));
        Microsoft.Maui.Handlers.TimePickerHandler.Mapper.AppendToMapping("FieldSurface", (handler, _) =>
            handler.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent));
        Microsoft.Maui.Handlers.EditorHandler.Mapper.AppendToMapping("FieldSurface", (handler, _) =>
            handler.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent));
        Microsoft.Maui.Handlers.SearchBarHandler.Mapper.AppendToMapping("FieldSurface", (handler, _) =>
        {
            handler.PlatformView.SetBackgroundColor(Android.Graphics.Color.Transparent);
            handler.PlatformView.FindViewById(Resource.Id.search_plate)?.SetBackgroundColor(Android.Graphics.Color.Transparent);
            handler.PlatformView.FindViewById(Resource.Id.submit_area)?.SetBackgroundColor(Android.Graphics.Color.Transparent);
        });
#elif IOS
        Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping(nameof(Entry.HorizontalTextAlignment), (handler, _) =>
            handler.PlatformView.TextAlignment = LocalizationService.Instance.Code == "ar" ? UIKit.UITextAlignment.Right : UIKit.UITextAlignment.Left);
        Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("FieldSurface", (handler, _) =>
            handler.PlatformView.BorderStyle = UIKit.UITextBorderStyle.None);
        Microsoft.Maui.Handlers.PickerHandler.Mapper.AppendToMapping("FieldSurface", (handler, _) =>
            handler.PlatformView.BorderStyle = UIKit.UITextBorderStyle.None);
        Microsoft.Maui.Handlers.DatePickerHandler.Mapper.AppendToMapping("FieldSurface", (handler, _) =>
            handler.PlatformView.BorderStyle = UIKit.UITextBorderStyle.None);
        Microsoft.Maui.Handlers.TimePickerHandler.Mapper.AppendToMapping("FieldSurface", (handler, _) =>
            handler.PlatformView.BorderStyle = UIKit.UITextBorderStyle.None);
        Microsoft.Maui.Handlers.EditorHandler.Mapper.AppendToMapping("FieldSurface", (handler, _) =>
            handler.PlatformView.Layer.BorderWidth = 0);
        Microsoft.Maui.Handlers.SearchBarHandler.Mapper.AppendToMapping("FieldSurface", (handler, _) =>
            handler.PlatformView.SearchBarStyle = UIKit.UISearchBarStyle.Minimal);
#endif
        builder.Services.AddSingleton(new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri(ApiRoutes.BaseUrl), Timeout = TimeSpan.FromSeconds(30) });
        builder.Services.AddSingleton<ILoginMemoryStorage, SecureLoginMemoryStorage>();
        builder.Services.AddSingleton<ThemeService>();
        builder.Services.AddSingleton<SessionRestoreService>();
        builder.Services.AddTransient<StartupPage>();
        builder.Services.AddTransient<StartupViewModel>();
        builder.Services.AddSingleton<LoginMemory>();
        builder.Services.AddSingleton<SecureSessionStore>();
        builder.Services.AddSingleton<ISessionStore>(s => s.GetRequiredService<SecureSessionStore>()); builder.Services.AddSingleton<AuthService>();
        builder.Services.AddSingleton<IAccessTokenProvider>(s => s.GetRequiredService<AuthService>());
        builder.Services.AddSingleton<IGenericRepository, ApiRepository>(); builder.Services.AddSingleton<DispatchService>(); builder.Services.AddSingleton<NavigationService>();
        builder.Services.AddSingleton<RealtimeService>(); builder.Services.AddSingleton<ReceiptService>(); builder.Services.AddSingleton<TrackingHubOptions>(); builder.Services.AddSingleton<ITrackingPublisher,TrackingHubPublisher>(); builder.Services.AddSingleton<TrackingService>();
        builder.Services.AddSingleton<IOfflineSyncStore>(_=>{var path=Path.Combine(FileSystem.AppDataDirectory,"offline-sync-v1.db3");return new SqliteOfflineSyncStore(path,()=>OfflineStoreKey.LoadAsync(path));});
        builder.Services.AddSingleton<OfflineSyncService>(sp=>{var auth=sp.GetRequiredService<AuthService>();return new OfflineSyncService(auth,sp.GetRequiredService<IOfflineSyncStore>(),scope=>new ApiRepository(sp.GetRequiredService<HttpClient>(),new OfflineScopedTokens(auth,scope)));});
        builder.Services.AddSingleton<OfflineSyncCoordinator>();
        builder.Services.AddTransient<SynchronizationViewModel>();builder.Services.AddTransient<SynchronizationPage>();
        builder.Services.AddSingleton<FinanceService>();builder.Services.AddSingleton<StatementExportService>();
        builder.Services.AddSingleton<TrackingArchiveService>();
        builder.Services.AddSingleton<CompanySettingsService>();
        builder.Services.AddSingleton<RideNotifications>();
        builder.Services.AddSingleton<PushApi>();
        builder.Services.AddSingleton<PushService>();
        builder.Services.AddSingleton<RideLinkService>();
        builder.Services.AddSingleton(new GoogleAddresses(new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(20) }, "AIzaSyBbR7g9b4LELjU3pH0N6GPphKCE41sx2ws"));
        builder.Services.AddTransient<AddressSearchViewModel>();
        builder.Services.AddTransient<AddressSearchPage>();
        builder.Services.AddSingleton<PassengerService>();
        builder.Services.AddSingleton<BookingQuoteService>();
        builder.Services.AddTransient<PassengerSearchViewModel>();
        builder.Services.AddTransient<PassengerSearchPage>();
        builder.Services.AddTransient<MoreMenuPage>();
        builder.Services.AddTransient<NotificationsPage>();
#if ANDROID
        builder.Services.AddSingleton<ITrackingPlatform, Platforms.Android.AndroidTrackingPlatform>();
#elif IOS
        builder.Services.AddSingleton<ITrackingPlatform, Platforms.iOS.IosTrackingPlatform>();
#endif
        builder.Services.AddTransient<LoginViewModel>(); builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<RidesViewModel>(); builder.Services.AddTransient<RidesPage>();
        builder.Services.AddTransient<ExtraReviewViewModel>();builder.Services.AddTransient<ExtraReviewPage>();
        builder.Services.AddTransient<TripDetailViewModel>(); builder.Services.AddTransient<TripDetailPage>();
        builder.Services.AddTransient<CreateRideViewModel>(); builder.Services.AddTransient<CreateRidePage>();
        builder.Services.AddTransient<ExpensesViewModel>(); builder.Services.AddTransient<ExpensesPage>();
        builder.Services.AddTransient<ExpenseDetailViewModel>();builder.Services.AddTransient<ExpenseDetailPage>();builder.Services.AddTransient<ExpenseCategoriesViewModel>();builder.Services.AddTransient<ExpenseCategoriesPage>();
        builder.Services.AddTransient<AddExpenseViewModel>(); builder.Services.AddTransient<AddExpensePage>();
        builder.Services.AddTransient<PaySystemsViewModel>();builder.Services.AddTransient<PaySystemsPage>();builder.Services.AddTransient<DriverBalancesViewModel>();builder.Services.AddTransient<DriverBalancesPage>();
        builder.Services.AddTransient<EarningsViewModel>(); builder.Services.AddTransient<EarningsPage>();
        builder.Services.AddTransient<TrackingHistoryViewModel>();
        builder.Services.AddTransient<DriverHistoryViewModel>();
        builder.Services.AddTransient<RoutePlaybackViewModel>();
        builder.Services.AddTransient<DriverHistoryPage>();
        builder.Services.AddTransient<RoutePlaybackPage>();
        builder.Services.AddTransient<DispatchTrackingStream>(); builder.Services.AddTransient<TrackingViewModel>(); builder.Services.AddTransient<TrackingPage>();
        builder.Services.AddTransient<MoreViewModel>(); builder.Services.AddTransient<MorePage>();
#if DEBUG
        builder.Logging.AddDebug();
#endif
        return builder.Build();
    }
    private static void InitializeFirebase(Action initialize)
    {
        try { initialize(); }
        catch (Exception) { System.Diagnostics.Debug.WriteLine("Firebase initialization failed. Device notifications remain unavailable; the workspace can still open."); }
    }
}
