using Android.App;
using Android.Content;
using AndroidX.Core.App;
using Plugin.Firebase.CloudMessaging;
namespace LimosDispatch.Mobile.Platforms.Android;

internal static class AndroidPush
{
    internal const string ChannelId = "ride_updates_v2";
    internal static void Configure()
    {
        var context = global::Android.App.Application.Context;
        var manager = (NotificationManager)context.GetSystemService(Context.NotificationService)!;
        var channel = new NotificationChannel(ChannelId, "Ride updates", NotificationImportance.High) { Description = "Ride assignments, website bookings and extra requests" };
        var sound = global::Android.Net.Uri.Parse($"android.resource://{context.PackageName}/raw/limosdispatch_alert");
        var attributes = new global::Android.Media.AudioAttributes.Builder()!
            .SetUsage(global::Android.Media.AudioUsageKind.Notification)!
            .SetContentType(global::Android.Media.AudioContentType.Sonification)!.Build()!;
        channel.SetSound(sound, attributes);
        manager.CreateNotificationChannel(channel);
        FirebaseCloudMessagingImplementation.ChannelId = ChannelId;
        FirebaseCloudMessagingImplementation.SmallIconRef = Resource.Drawable.ic_stat_notification;
        // A distinct identity per notification keeps multiple taps from opening the last ride.
        FirebaseCloudMessagingImplementation.ShowLocalNotificationAction = notification =>
        {
            var target = PushTarget.FromData(notification.Data); if (target is null) return;
            var intent = context.PackageManager!.GetLaunchIntentForPackage(context.PackageName!)!;
            intent.SetFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop);
            intent.SetData(global::Android.Net.Uri.Parse($"limosdispatch://ride/{Uri.EscapeDataString(target.TripId)}?company={Uri.EscapeDataString(target.Company)}&notificationId={target.NotificationId}"));
            foreach (var pair in notification.Data) intent.PutExtra(pair.Key, pair.Value);
            var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(target.NotificationId));
            var id = BitConverter.ToInt32(bytes, 0) & int.MaxValue;
            var pending = PendingIntent.GetActivity(context, id, intent, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
            var builder = new NotificationCompat.Builder(context, ChannelId);
            builder.SetSmallIcon(Resource.Drawable.ic_stat_notification);
            builder.SetContentTitle(notification.Title); builder.SetContentText(notification.Body);
            builder.SetStyle(new NotificationCompat.BigTextStyle().BigText(notification.Body));
            builder.SetPriority(NotificationCompat.PriorityHigh); builder.SetAutoCancel(true); builder.SetContentIntent(pending);
            manager.Notify(id, builder.Build());
        };
    }
    internal static void Receive(Intent? intent)
    {
        try { ReceiveCore(intent); }
        catch (Exception) { _ = FeedbackService.ShowErrorAsync("The notification could not be opened. Open Notifications and try again."); }
    }
    private static void ReceiveCore(Intent? intent)
    {
        if (intent is null) return;
        var data = new Dictionary<string, string>();
        foreach (var key in new[] { "type", "company", "tripId", "notificationId", "revision", "extraId" })
            if (intent.GetStringExtra(key) is { } value) data[key] = value;
        var target = PushTarget.FromData(data);
        if (target is not null)
        {
            IPlatformApplication.Current?.Services.GetRequiredService<RideLinkService>().Receive(target);
            foreach (var key in data.Keys) intent.RemoveExtra(key);
        }
        FirebaseCloudMessagingImplementation.OnNewIntent(intent);
    }
}
