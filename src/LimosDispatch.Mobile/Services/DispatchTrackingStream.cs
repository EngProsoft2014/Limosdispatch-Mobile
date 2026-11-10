using Microsoft.AspNetCore.SignalR.Client;
namespace LimosDispatch.Mobile.Services;
public sealed class DispatchTrackingStream(AuthService auth,TrackingHubOptions options)
{
    private readonly SemaphoreSlim gate=new(1,1);
    private HubConnection? hub;
    private long requested,active;
    private volatile bool subscribed;
    public bool IsConnected=>subscribed&&hub?.State==HubConnectionState.Connected;
    public event Action<LocationFrame>? PositionReceived;
    public event Action<TrackingHeartbeat>? HeartbeatReceived;
    public event Action<TrackingSnapshot>? SnapshotReceived;
    public event Action? StateChanged;
    private bool Current(HubConnection connection,long generation,CancellationToken ct)=>!ct.IsCancellationRequested&&generation==Interlocked.Read(ref requested)&&ReferenceEquals(hub,connection);
    private void Changed(bool value){subscribed=value;StateChanged?.Invoke();}
    private async Task SubscribeAsync(HubConnection connection,long generation,CancellationToken ct)
    {
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(12));
        var snapshot=await connection.InvokeAsync<TrackingSnapshot>("SubscribeDispatch",timeout.Token);
        if(!Current(connection,generation,ct))return;
        SnapshotReceived?.Invoke(snapshot??new());Changed(true);
    }
    public async Task StartAsync(CancellationToken ct)
    {
        var generation=Interlocked.Increment(ref requested);
        await gate.WaitAsync(ct);
        try
        {
            if(generation!=Interlocked.Read(ref requested)||ct.IsCancellationRequested)return;
            if(hub is not null){await hub.DisposeAsync();hub=null;}
            active=generation;Changed(false);
            var connection=TrackingHubConnection.Create(auth,options);hub=connection;
            connection.On<LocationFrame>("DriverLocationUpdated",frame=>{if(frame is not null&&Current(connection,generation,ct))PositionReceived?.Invoke(frame);});
            connection.On<TrackingHeartbeat>("DriverTrackingHeartbeat",beat=>{if(beat is not null&&Current(connection,generation,ct))HeartbeatReceived?.Invoke(beat);});
            connection.Reconnecting+=_=>{if(Current(connection,generation,ct))Changed(false);return Task.CompletedTask;};
            connection.Closed+=_=>{if(Current(connection,generation,ct))Changed(false);return Task.CompletedTask;};
            connection.Reconnected+=async _=>{if(!Current(connection,generation,ct))return;try{await SubscribeAsync(connection,generation,ct);}catch(Exception){if(Current(connection,generation,ct))Changed(false);}};
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(12));
            try { await connection.StartAsync(timeout.Token);await SubscribeAsync(connection,generation,ct); }
            catch(Exception){if(Current(connection,generation,ct))Changed(false);await connection.DisposeAsync();if(ReferenceEquals(hub,connection))hub=null;}
        }
        finally{gate.Release();}
    }
    public async Task RefreshAsync(CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try {if(hub is {State:HubConnectionState.Connected} connection)await SubscribeAsync(connection,active,ct);}
        finally{gate.Release();}
    }
    public async Task StopAsync()
    {
        var stop=Interlocked.Increment(ref requested);subscribed=false;
        await gate.WaitAsync();
        try {if(active<stop&&hub is not null){var old=hub;hub=null;await old.DisposeAsync();}}
        catch(Exception){hub=null;}
        finally{gate.Release();}
    }
}
