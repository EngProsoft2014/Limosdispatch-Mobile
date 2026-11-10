using LimosDispatch.Core.GenericRepository;
using LimosDispatch.Core.Models;
namespace LimosDispatch.Core.Services;

public sealed class PushApi(IGenericRepository api)
{
    public Task<PushInbox> InboxAsync(string? cursor = null, CancellationToken ct = default) => api.GetAsync<PushInbox>("api/notifications?limit=20" + (string.IsNullOrEmpty(cursor) ? "" : "&cursor=" + Uri.EscapeDataString(cursor)), ct);
    public Task<PushCount> CountAsync(CancellationToken ct = default) => api.GetAsync<PushCount>("api/notifications/unread-count", ct);
    public Task<PushCount> ReadAsync(string id) => api.PostAsync<PushCount>("api/notifications/" + Id(id) + "/read", new { });
    public Task<PushCount> ReadAllAsync() => api.PostAsync<PushCount>("api/notifications/read-all", new { });
    public async Task<object> RegisterAsync(PushDevice device)
    {
        if (!Guid.TryParse(device.InstallationId, out _) || device.Platform is not ("android" or "ios") || string.IsNullOrWhiteSpace(device.RegistrationToken) || device.RegistrationToken.Length > 4096 || device.RegistrationToken.Any(char.IsWhiteSpace))
            throw new InvalidOperationException("The push device registration is invalid.");
        try { return await api.PostAsync<object>("api/push/devices", device); }
        catch (ApiException ex) when (ex.Status == System.Net.HttpStatusCode.Conflict)
        {
            // The handoff explicitly makes registration idempotent and permits a 409 retry.
            return await api.PostAsync<object>("api/push/devices", device);
        }
    }
    public Task<object> UnregisterAsync(string installationId) => Guid.TryParse(installationId, out _) ? api.DeleteAsync<object>("api/push/devices/" + installationId) : throw new ArgumentException("Invalid installation ID.");
    public async Task<Trip> TripAsync(string id) => (await api.GetAsync<TripResponse>("api/trips/" + Id(id))).Trip ?? throw new InvalidDataException("This ride is no longer available.");
    private static string Id(string id) => PushTarget.ValidId(id) ? Uri.EscapeDataString(id) : throw new ArgumentException("Invalid notification or trip ID.");
    public sealed class TripResponse { public Trip? Trip { get; set; } }
}
