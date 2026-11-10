using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using LimosDispatch.Core.Helpers;
namespace LimosDispatch.Mobile.Services;

public static class FeedbackService
{
    private static string lastMessage = "";
    private static DateTimeOffset lastShown;
    private static readonly SemaphoreSlim confirmation = new(1, 1);
    private static Color Resource(string key, string fallback) => Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color ? color : Color.FromArgb(fallback);
    private static SnackbarOptions Options() => new()
    {
        BackgroundColor = Resource("Surface", "#FFFFFF"), TextColor = Resource("Ink", "#152936"), ActionButtonTextColor = Resource("AccentText", "#8C703A"),
        CornerRadius = new CornerRadius(14), Font = Microsoft.Maui.Font.OfSize(LocalizationService.Instance.Code == "ar" ? "ElMessiriRegular" : "OpenSansRegular", 14), ActionButtonFont = Microsoft.Maui.Font.OfSize(LocalizationService.Instance.Code == "ar" ? "ElMessiriBold" : "OpenSansSemibold", 14)
    };
    public static Task ShowErrorAsync(string message) => MainThread.InvokeOnMainThreadAsync(async () =>
    {
        if (message == lastMessage && DateTimeOffset.UtcNow - lastShown < TimeSpan.FromSeconds(4)) return;
        lastMessage = message; lastShown = DateTimeOffset.UtcNow;
        await Snackbar.Make(LocalizationService.Text(message), actionButtonText: LocalizationService.Text("OK"), duration: TimeSpan.FromSeconds(6), visualOptions: Options()).Show();
    });
    public static Task<bool> ConfirmAsync(string message, string actionText, Func<bool>? stillValid = null) => MainThread.InvokeOnMainThreadAsync(async () =>
    {
        if (!await confirmation.WaitAsync(0)) return false;
        try
        {
            var root = Application.Current?.Windows.FirstOrDefault()?.Page;
            if (root is null) return false;
            // Native Android Snackbar actions can pass touches to Shell's bottom tabs.
            // Present the actionable bar in a modal layer; ordinary notices remain native Snackbars.
            var bar = new ConfirmationBarPage(message, actionText);
            await root.Navigation.PushModalAsync(bar, false);
            var accepted = await bar.Result;
            return accepted && Application.Current?.Windows.FirstOrDefault()?.Page == root && (stillValid?.Invoke() ?? true);
        }
        catch (Exception ex) { await UserFeedback.ReportAsync(ex); return false; }
        finally { confirmation.Release(); }
    });
    public static Task<bool> ConfirmSignOutAsync(Func<bool> stillValid) => ConfirmAsync("Sign out of this company workspace?", "Sign out", stillValid);
    public static Task ConfirmExitAsync(Func<bool> stillAtRoot, Action exit) => UserFeedback.RunAsync(async () =>
    {
        if (await ConfirmAsync("Would you like to close the app?", "Exit", stillAtRoot))
            await UserFeedback.RunAsync(() => { if (stillAtRoot()) exit(); return Task.CompletedTask; });
    });
}
