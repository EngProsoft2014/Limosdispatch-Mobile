using Plugin.Firebase.CloudMessaging;
using UserNotifications;
namespace LimosDispatch.Mobile.Platforms.iOS;

// Plugin 4.0.1 skips the native completion callback for a silenced foreground
// message. Preserve its Firebase routing while completing every native callback.
internal sealed class SafeNotificationDelegate(FirebaseCloudMessagingImplementation firebase) : UNUserNotificationCenterDelegate
{
    private static SafeNotificationDelegate? retained;
    internal static void Install()
    {
        retained ??= new((FirebaseCloudMessagingImplementation)CrossFirebaseCloudMessaging.Current);
        UNUserNotificationCenter.Current.Delegate = retained;
    }
    public override void WillPresentNotification(UNUserNotificationCenter center, UNNotification notification, Action<UNNotificationPresentationOptions> completionHandler)
    {
        var completed = 0;
        void Complete(UNNotificationPresentationOptions options) { if (Interlocked.Exchange(ref completed, 1) == 0) completionHandler(options); }
        try { firebase.WillPresentNotification(center, notification, Complete); }
        catch (Exception) { System.Diagnostics.Debug.WriteLine("A foreground notification could not be processed."); }
        finally { Complete(UNNotificationPresentationOptions.None); }
    }
    public override void DidReceiveNotificationResponse(UNUserNotificationCenter center, UNNotificationResponse response, Action completionHandler)
    {
        var completed = 0;
        void Complete() { if (Interlocked.Exchange(ref completed, 1) == 0) completionHandler(); }
        try { firebase.DidReceiveNotificationResponse(center, response, Complete); }
        catch (Exception) { System.Diagnostics.Debug.WriteLine("A notification response could not be processed."); }
        finally { Complete(); }
    }
}
