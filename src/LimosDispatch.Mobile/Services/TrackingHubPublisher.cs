using Microsoft.AspNetCore.SignalR.Client;
namespace LimosDispatch.Mobile.Services;
public sealed class TrackingHubPublisher : ITrackingPublisher
{
    private readonly AuthService auth;
    private readonly TrackingHubOptions options;
    private readonly SemaphoreSlim gate=new(1,1);
    private HubConnection? hub;
    private string scope="";
    public TrackingHubPublisher(AuthService auth,TrackingHubOptions options)
    {this.auth=auth;this.options=options;auth.SignedOut+=()=>_ = StopAsync();}
    private async Task<HubConnection> ConnectAsync(CancellationToken ct)
    {
        var current=TrackingHubConnection.Scope(auth);
        if(hub is not null&&scope!=current){await hub.DisposeAsync();hub=null;}
        if(hub is null){scope=current;hub=TrackingHubConnection.Create(auth,options);}
        if(hub.State==HubConnectionState.Disconnected)await hub.StartAsync(ct);
        if(hub.State!=HubConnectionState.Connected)throw new HttpRequestException("Live tracking is reconnecting.");
        return hub;
    }
    public async Task<LocationAck> PublishAsync(string tripId,List<GpsPoint> points,CancellationToken ct=default)
    {
        await gate.WaitAsync(ct);
        try
        {
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(12));
            var connection=await ConnectAsync(timeout.Token);
            return await connection.InvokeAsync<LocationAck>("PublishLocations",tripId,new LocationBatch(points),timeout.Token)
                ?? throw new InvalidDataException("Location upload was not acknowledged. It will be retried.");
        }
        finally{gate.Release();}
    }
    public async Task HeartbeatAsync(string tripId,string motion,CancellationToken ct=default)
    {
        if(motion is not("moving" or "stationary"))throw new ArgumentException("Invalid tracking motion state.");
        await gate.WaitAsync(ct);
        try
        {
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(12));
            var connection=await ConnectAsync(timeout.Token);
            await connection.InvokeAsync("Heartbeat",tripId,motion,timeout.Token);
        }
        finally{gate.Release();}
    }
    public async Task StopAsync()
    {
        await gate.WaitAsync();
        try { if(hub is not null){var old=hub;hub=null;await old.DisposeAsync();}scope=""; }
        catch(Exception){hub=null;scope="";}
        finally{gate.Release();}
    }
}
