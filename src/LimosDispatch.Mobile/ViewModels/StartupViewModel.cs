using System.Windows.Input;
namespace LimosDispatch.Mobile.ViewModels;

public sealed class StartupViewModel : BaseViewModel
{
    private readonly SessionRestoreService sessions;
    private readonly NavigationService navigation;
    private readonly CompanySettingsService settings;
    private readonly RealtimeService realtime;
    private bool started;
    public AsyncCommand RetryCommand { get; }
    public ICommand SignInCommand { get; }
    public StartupViewModel(SessionRestoreService sessions, NavigationService navigation, CompanySettingsService settings, RealtimeService realtime)
    {
        this.sessions = sessions; this.navigation = navigation; this.settings = settings; this.realtime = realtime;
        RetryCommand = new(() => Run(Restore));
        SignInCommand = new Command(() => navigation.OpenLogin());
    }
    public override Task LoadAsync()
    {
        if (started) return Task.CompletedTask;
        started = true;
        return Run(Restore);
    }
    private async Task Restore()
    {
        var user = await sessions.RestoreAsync();
        if (user is null || user.MustChangePassword) { navigation.OpenLogin(); return; }
        await settings.LoadAsync(user);
        navigation.OpenWorkspace(user);
        _ = realtime.ResumeAsync();
    }
}
