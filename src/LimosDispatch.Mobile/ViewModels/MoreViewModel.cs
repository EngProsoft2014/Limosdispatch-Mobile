using LimosDispatch.Core.GenericRepository;
namespace LimosDispatch.Mobile.ViewModels;
public sealed class MoreViewModel : BaseViewModel
{
    private readonly AuthService auth; private readonly NavigationService nav; private readonly TrackingService tracking; private readonly RealtimeService realtime; private readonly IGenericRepository api;
    public UserProfile? User => auth.User;
    public string TrackingHealth => tracking.Health;
    public string Version => LocalizationService.Format("Version {0}", AppInfo.Current.VersionString);
    public IReadOnlyList<LanguageOption> Languages => LocalizationService.Instance.Languages;
    public LanguageOption SelectedLanguage { get => Languages.First(l => l.Code == LocalizationService.Instance.Code); set { if (value is not null) { LocalizationService.Instance.Apply(value.Code); Raise(); } } }
    public bool HasDriverSettings => User?.IsDriver == true;
    private readonly ThemeService themes;
    public IReadOnlyList<string> Themes => themes.Themes;
    public string SelectedTheme { get => themes.Selected; set { if (value is not null && value != themes.Selected) { themes.Apply(value); Raise(); } } }
    public bool AnnouncementsEnabled
    {
        get => Preferences.Default.Get("assignment-announcements", true);
        set { Preferences.Default.Set("assignment-announcements", value); Raise(); }
    }
    public bool KeepScreenAwake
    {
        get => Preferences.Default.Get("journey-screen-awake", false);
        set { Preferences.Default.Set("journey-screen-awake", value); if (tracking.IsRunning) DeviceDisplay.Current.KeepScreenOn = value; Raise(); }
    }
    public AsyncCommand LogoutCommand { get; }
    public AsyncCommand WebsiteCommand { get; }
    public MoreViewModel(AuthService auth, NavigationService nav, TrackingService tracking, RealtimeService realtime, IGenericRepository api, ThemeService themes, PushService push)
    {
        this.auth = auth; this.nav = nav; this.tracking = tracking; this.realtime = realtime; this.api = api; this.themes = themes;
        LogoutCommand = new(() => Run(async () =>
        {
            if (tracking.IsRunning) throw new InvalidOperationException("Complete the active journey before signing out so dispatch can continue tracking you.");
            var user = auth.User;
            if (!await FeedbackService.ConfirmSignOutAsync(() => auth.User == user)) return;
            if (tracking.IsRunning) throw new InvalidOperationException("Complete the active journey before signing out so dispatch can continue tracking you.");
            await push.UnregisterAsync(); await realtime.ResetAsync(); await auth.LogoutAsync(api); nav.OpenLogin();
        }));
        WebsiteCommand = new(() => Run(async () => await Launcher.Default.OpenAsync(new Uri($"https://{User!.Company}.limosdispatch.com/"))));
        RefreshCommand = new(LoadAsync);
    }
    public override Task LoadAsync() { Raise(nameof(TrackingHealth)); return Task.CompletedTask; }
}
