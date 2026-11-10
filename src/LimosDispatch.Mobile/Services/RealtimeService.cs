using LimosDispatch.Core.Constants;
using Microsoft.AspNetCore.SignalR.Client;
namespace LimosDispatch.Mobile.Services;
public sealed class RealtimeService(AuthService auth)
{
    private HubConnection? hub;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly HashSet<string> revisions = [];
    public event Action? TripsChanged;
    public event Action<bool>? ForegroundChanged;
    public bool IsForeground { get; private set; } = true;
    public async Task ResumeAsync()
    {
        IsForeground = true;
        ForegroundChanged?.Invoke(true);
        TripsChanged?.Invoke();
        if (auth.User?.IsDriver != true) return;
        await gate.WaitAsync();
        try
        {
            if (hub is null)
            {
                hub = new HubConnectionBuilder().WithUrl(ApiRoutes.BaseUrl + ApiRoutes.DriverEvents,
                    o => o.AccessTokenProvider = async () => await auth.GetTokenAsync()).WithAutomaticReconnect().Build();
                hub.On<Assignment>("TripAssigned", a =>
                {
                    var key = $"{a.TripId}:{a.AssignmentRevision}";
                    if (!revisions.Add(key)) return;
                    if (revisions.Count > 200) { revisions.Clear(); revisions.Add(key); }
                    // FCM owns the user alert; SignalR only refreshes the current data.
                    MainThread.BeginInvokeOnMainThread(() => TripsChanged?.Invoke());
                });
                hub.Reconnected += _ => { MainThread.BeginInvokeOnMainThread(() => TripsChanged?.Invoke()); return Task.CompletedTask; };
            }
            if (hub.State == HubConnectionState.Disconnected) await hub.StartAsync();
        }
        catch (Exception ex) { await LimosDispatch.Core.Helpers.UserFeedback.ReportAsync(ex); }
        finally { gate.Release(); }
    }
    public async Task PauseAsync()
    { IsForeground = false; ForegroundChanged?.Invoke(false); await gate.WaitAsync(); try { if (hub is not null) await hub.StopAsync(); } finally { gate.Release(); } }
    public async Task ResetAsync()
    { await gate.WaitAsync(); try { if (hub is not null) await hub.DisposeAsync(); hub = null; revisions.Clear(); } finally { gate.Release(); } }
    public sealed record Assignment(string TripId, long AssignmentRevision, long PickupAt);
}
