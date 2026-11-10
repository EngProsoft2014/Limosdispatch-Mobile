using System.Net;
using System.Text.Json;
using LimosDispatch.Core.GenericRepository;
using LimosDispatch.Core.Helpers;
namespace LimosDispatch.Mobile.Services;
// Cold-start taps wait for an authenticated Shell. Only routing IDs are persisted.
public sealed class RideLinkService(AuthService auth, PushApi api, NavigationService nav, RideNotifications inbox)
{
    private const string PendingKey = "pending-push-ride-v1";
    private PushTarget? pending;
    private bool opening, loaded;
    public void Receive(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "limosdispatch" || uri.Host != "ride") return;
        var company = uri.Query.TrimStart('?').Split('&').Select(p => p.Split('=', 2)).FirstOrDefault(p => p[0] == "company");
        var target = PushTarget.Create(company?.Length == 2 ? Uri.UnescapeDataString(company[1]) : "", Uri.UnescapeDataString(uri.AbsolutePath.Trim('/')));
        if (target is not null) Receive(target);
    }
    public void Receive(PushTarget target) => _ = UserFeedback.RunAsync(() => OpenAsync(target));
    public async Task OpenAsync(PushTarget target)
    {
        var valid = PushTarget.Create(target.Company, target.TripId, target.NotificationId); if (valid is null) return;
        pending = valid; loaded = true; Preferences.Default.Set(PendingKey, JsonSerializer.Serialize(pending));
        await OpenPendingAsync();
    }
    private void Restore()
    {
        if (loaded) return; loaded = true;
        try
        {
            var json = Preferences.Default.Get(PendingKey, ""); if (string.IsNullOrEmpty(json)) return;
            var saved = JsonSerializer.Deserialize<PushTarget>(json);
            if (saved is not null) pending = PushTarget.Create(saved.Company, saved.TripId, saved.NotificationId);
        }
        catch (JsonException) { Preferences.Default.Remove(PendingKey); }
    }
    private void Consume(PushTarget request)
    {
        if (pending != request) return;
        pending = null; Preferences.Default.Remove(PendingKey);
    }
    public async Task OpenPendingAsync()
    {
        Restore();
        if (opening || nav.IsRestoringSession || !nav.IsWorkspaceOpen || auth.User is null || pending is not { } request) return;
        opening = true;
        try
        {
            var user = auth.User;
            if (!user.Company.Equals(request.Company, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("This notification belongs to another company. Sign in to that company to view the ride.");
            Trip trip;
            try { trip = await api.TripAsync(request.TripId); }
            catch (ApiException ex) when (ex.Status == HttpStatusCode.NotFound) { Consume(request); throw new InvalidOperationException("This ride is no longer available."); }
            if (auth.User != user || pending != request) return;
            await nav.OpenTripAsync(trip); Consume(request);
            if (!string.IsNullOrEmpty(request.NotificationId)) await inbox.MarkReadAsync(request.NotificationId);
        }
        finally { opening = false; if (pending is { } next && next != request) MainThread.BeginInvokeOnMainThread(() => _ = UserFeedback.RunAsync(OpenPendingAsync)); }
    }
}
