using Android.App;
using Android.Content.PM;
using Android.OS;

namespace LimosDispatch.Mobile;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
[IntentFilter([Android.Content.Intent.ActionView], Categories = [Android.Content.Intent.CategoryDefault, Android.Content.Intent.CategoryBrowsable], DataScheme = "limosdispatch", DataHost = "ride")]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Platforms.Android.AndroidPush.Receive(Intent);
        ReceiveLink(Intent);
        if (OperatingSystem.IsAndroidVersionAtLeast(31))
            SplashScreen?.SetOnExitAnimationListener(new ImmediateSplashExit());
        // The status bar uses the navy brand color, so its icons must stay light.
        if (Window is not null &&
            AndroidX.Core.View.WindowCompat.GetInsetsController(Window, Window.DecorView) is { } controller)
            controller.AppearanceLightStatusBars = false;
    }
    protected override void OnNewIntent(Android.Content.Intent? intent) { base.OnNewIntent(intent); Platforms.Android.AndroidPush.Receive(intent); ReceiveLink(intent); }
    private static void ReceiveLink(Android.Content.Intent? intent)
    {
        if (intent?.Action == Android.Content.Intent.ActionView)
            IPlatformApplication.Current?.Services.GetRequiredService<RideLinkService>().Receive(intent.DataString);
    }
    [System.Runtime.Versioning.SupportedOSPlatform("android31.0")]
    private sealed class ImmediateSplashExit : Java.Lang.Object, global::Android.Window.ISplashScreenOnExitAnimationListener
    {
        public void OnSplashScreenExit(global::Android.Window.SplashScreenView? view) => view?.Remove();
    }
}
