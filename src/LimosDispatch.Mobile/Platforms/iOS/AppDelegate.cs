using Foundation;

namespace LimosDispatch.Mobile;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    public override bool OpenUrl(UIKit.UIApplication application, NSUrl url, NSDictionary options)
    {
        IPlatformApplication.Current?.Services.GetRequiredService<RideLinkService>().Receive(url.AbsoluteString);
        return true;
    }
    private static readonly Lazy<bool> mapsReady = new(() => Google.Maps.MapServices.ProvideApiKey("AIzaSyBvVpannsx7x7CpyXOksDG_D9ofq1jiUzo"));
    internal static void EnsureGoogleMapsInitialized() => _ = mapsReady.Value;
	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
