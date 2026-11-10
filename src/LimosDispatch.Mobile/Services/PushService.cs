using LimosDispatch.Core.Helpers;
using Plugin.Firebase.CloudMessaging;
namespace LimosDispatch.Mobile.Services;

public sealed class PushService(AuthService auth, PushApi api, RideNotifications inbox, RideLinkService links)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly HashSet<string> seen = [];
    private bool initialized;
    private volatile bool suspended;
    private string seenScope = "";
    public string Status { get; private set; } = "Setting up device notifications…";
    public bool Registered { get; private set; }
    public event Action? Changed;
    private void SetStatus(string status, bool registered = false) { Status = status; Registered = registered; MainThread.BeginInvokeOnMainThread(() => Changed?.Invoke()); }
    public string InstallationId
    {
        get
        {
            var id = Preferences.Default.Get("push-installation-id", "");
            if (!Guid.TryParse(id, out _)) { id = Guid.NewGuid().ToString(); Preferences.Default.Set("push-installation-id", id); }
            return id;
        }
    }
    public void Initialize()
    {
        if (initialized) return;
        var messaging = CrossFirebaseCloudMessaging.Current;
        messaging.TokenChanged += (_, _) => _ = UserFeedback.RunAsync(() => RegisterAsync(false));
        messaging.NotificationTapped += (_, args) =>
        {
            try
            {
                var target = PushTarget.FromData(args.Notification.Data);
                if (target is not null) MainThread.BeginInvokeOnMainThread(() => links.Receive(target));
            }
            catch { _ = FeedbackService.ShowErrorAsync("The notification could not be opened. Open Notifications and try again."); }
        };
        messaging.NotificationReceived += (_, args) =>
        {
            try
            {
                var data = args.Notification.Data; var target = PushTarget.FromData(data); var user = auth.User;
                var scope = user is null ? "" : user.Company + "|" + user.Id;
                bool silent;
                lock (seen)
                {
                    if (scope != seenScope) { seen.Clear(); seenScope = scope; }
                    silent = suspended || target is null || user is null || !target.Company.Equals(user.Company, StringComparison.OrdinalIgnoreCase) || !seen.Add(target.NotificationId);
                    if (seen.Count > 500) { seen.Clear(); if (target is not null) seen.Add(target.NotificationId); }
                }
                if (silent && data is not null) data["is_silent_in_foreground"] = "true";
                if (!silent) MainThread.BeginInvokeOnMainThread(() =>
                {
                    inbox.RefreshInBackground();
                    if (target is not null && data?["type"] == "ride_assigned" && Preferences.Default.Get("assignment-announcements", true))
                        try { SemanticScreenReader.Default.Announce(LocalizationService.Text("A new journey has been assigned.")); } catch { }
                });
            }
            catch { _ = FeedbackService.ShowErrorAsync("Notifications could not be refreshed. Open Notifications and try again."); }
        };
        messaging.Error += (_, _) => { if (auth.User is not null) _ = FeedbackService.ShowErrorAsync("Push notifications are unavailable. Check notification permissions and your connection."); };
        auth.SignedOut += () => { suspended = true; SetStatus("Setting up device notifications…"); };
        initialized = true;
    }
    public Task StartSessionAsync() { suspended = false; return RegisterAsync(true); }
    public async Task RegisterAsync(bool requestPermission)
    {
        if (auth.User is null || auth.User.MustChangePassword) return;
        if (!initialized) { SetStatus("Device notifications are unavailable. Tap Enable notifications to retry."); throw new InvalidOperationException("Push notifications could not be registered. Check your connection and notification permissions."); }
        await gate.WaitAsync();
        try
        {
            if (suspended || auth.User is null) return;
            var user = auth.User;
            SetStatus("Setting up device notifications…");
            if (!await CanNotifyAsync(requestPermission)) { SetStatus("Notifications are disabled in your phone settings."); return; }
            var token = await CrossFirebaseCloudMessaging.Current.GetTokenAsync().WaitAsync(TimeSpan.FromSeconds(25));
            if (auth.User != user || user is null) return;
            await api.RegisterAsync(new(InstallationId, DeviceInfo.Platform == DevicePlatform.iOS ? "ios" : "android", token, AppInfo.Current.VersionString));
            if (auth.User == user) SetStatus("Device notifications are enabled.", true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        { SetStatus("Device notifications are unavailable. Tap Enable notifications to retry."); throw new InvalidOperationException("Push notifications could not be registered. Check your connection and notification permissions."); }
        finally { gate.Release(); }
    }
    private static async Task<bool> CanNotifyAsync(bool requestPermission)
    {
#if ANDROID
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            var status = requestPermission ? await Permissions.RequestAsync<Permissions.PostNotifications>() : await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
            if (status != PermissionStatus.Granted) return false;
        }
        return AndroidX.Core.App.NotificationManagerCompat.From(global::Android.App.Application.Context)!.AreNotificationsEnabled();
#elif IOS
        if (requestPermission)
        {
            var result = await UserNotifications.UNUserNotificationCenter.Current.RequestAuthorizationAsync(UserNotifications.UNAuthorizationOptions.Alert | UserNotifications.UNAuthorizationOptions.Sound | UserNotifications.UNAuthorizationOptions.Badge);
            if (!result.Item1) return false;
        }
        var settings = await UserNotifications.UNUserNotificationCenter.Current.GetNotificationSettingsAsync();
        if (settings.AuthorizationStatus is not (UserNotifications.UNAuthorizationStatus.Authorized or UserNotifications.UNAuthorizationStatus.Provisional or UserNotifications.UNAuthorizationStatus.Ephemeral)) return false;
        UIKit.UIApplication.SharedApplication.RegisterForRemoteNotifications();
        return true;
#else
        return await Task.FromResult(false);
#endif
    }
    public async Task UnregisterAsync()
    {
        await gate.WaitAsync();
        try { suspended = true; if (auth.User is not null) await api.UnregisterAsync(InstallationId); }
        catch { suspended = false; throw; }
        finally { gate.Release(); }
        inbox.Clear(); lock (seen) { seen.Clear(); seenScope = ""; }
    }
    public Task ResumeAsync()
    {
        inbox.Start();
        return UserFeedback.RunAsync(async () => { await links.OpenPendingAsync(); await RegisterAsync(false); });
    }
}
