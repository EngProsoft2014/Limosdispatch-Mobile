using System.Text.Json;
using LimosDispatch.Core.Helpers;
namespace LimosDispatch.Mobile.Services;
public interface ITrackingPlatform{event Action<GpsPoint>? PointCaptured;Task StartAsync();void Stop();}
public sealed class TrackingRuntimeOptions
{
    public TimeSpan HeartbeatInterval{get;init;}=TimeSpan.FromSeconds(30);
    public TimeSpan RetryInterval{get;init;}=TimeSpan.FromSeconds(15);
}
public sealed class TrackingService(ITrackingPlatform platform,ITrackingPublisher publisher,AuthService auth,TrackingRuntimeOptions? settings=null,IOfflineSyncStore? outbox=null,OfflineSyncService? offline=null)
{
    private readonly TrackingRuntimeOptions options=settings??new();
    private readonly SemaphoreSlim gate=new(1,1),uploadGate=new(1,1),retrySignal=new(0,1);
    private readonly List<GpsPoint> pending=[];
    private readonly TrackingPointFilter pointFilter=new();private readonly TrackingUploadBackoff uploadBackoff=new();
    private CancellationTokenSource? lifetime;private string? tripId,storageKey;private OfflineScope? trackingScope;
    private DateTimeOffset preparedAt,lastFeedbackAt;private GpsPoint? preparedPoint;
    public bool IsRunning=>tripId is not null;public string? ActiveTripId=>tripId;
    public string Health{get;private set;}="Location sharing is off";public string LastLocation{get;private set;}="";
    public event Action? HealthChanged;public int HistoryIntervalMs{get;private set;}=5000;
    public async Task PrepareAsync()
    {
        if(await Permissions.RequestAsync<Permissions.LocationWhenInUse>()!=PermissionStatus.Granted)throw new InvalidOperationException("Allow location access to start this journey.");
        if(!Geolocation.Default.IsEnabled)throw new InvalidOperationException("Turn on device location to start this journey.");
        if(DateTimeOffset.UtcNow-preparedAt<TimeSpan.FromSeconds(30))return;
        var fix=await Geolocation.Default.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Best,TimeSpan.FromSeconds(20)));
        if(fix is null)throw new InvalidOperationException("Location is unavailable. Enable device location and try again.");
        preparedPoint=new(fix.Timestamp.ToUnixTimeMilliseconds(),fix.Latitude,fix.Longitude,fix.Accuracy??0);preparedAt=DateTimeOffset.UtcNow;
    }
    public async Task<GpsPoint?> CaptureStageLocationAsync()
    {
        try
        {
            if (await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>() != PermissionStatus.Granted ||
                !Geolocation.Default.IsEnabled) return null;

            var fix = await Geolocation.Default.GetLocationAsync(
                new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(8)));
            if (fix is null || (DateTimeOffset.UtcNow - fix.Timestamp).Duration() > TimeSpan.FromSeconds(15))
                return null;
            return new GpsPoint(fix.Timestamp.ToUnixTimeMilliseconds(), fix.Latitude, fix.Longitude, fix.Accuracy ?? 0);
        }
        catch (Exception ex) when (ex is FeatureNotEnabledException or FeatureNotSupportedException or PermissionException or OperationCanceledException)
        {
            return null;
        }
    }

    public async Task ResumeAsync(string id)
    {
        if(tripId!=id){await StartAsync(id);return;}await PrepareAsync();platform.Stop();await platform.StartAsync();OnPoint(preparedPoint!);SetHealth("Location sharing active");
    }
    public async Task StartAsync(string id)
    {
        if(tripId==id)return;if(tripId is not null)throw new InvalidOperationException("Finish the current journey before starting another.");
        if(offline is not null&&await offline.EndedAsync(id))return;
        await PrepareAsync();var scope=OfflineScope.From(auth.User);
        if(outbox is not null&&scope is null)throw new UnauthorizedAccessException();
        var key=$"gps:{auth.User!.Company}:{auth.User.Id}:{id}";
        var saved=await SecureStorage.Default.GetAsync(key);var state=new TrackingQueueState();
        if(saved is not null)
        {
            // Never silently erase unreadable GPS data.
            using var json=JsonDocument.Parse(saved);
            state=json.RootElement.ValueKind==JsonValueKind.Array?new(){Pending=JsonSerializer.Deserialize<List<GpsPoint>>(saved)??[]}:JsonSerializer.Deserialize<TrackingQueueState>(saved)??new();
        }
        if(outbox is not null&&scope is not null)
        {
            foreach(var point in (state.Pending??[]).OrderBy(p=>p.CapturedAt))await outbox.CaptureAsync(scope,id,point);
            if(state.LastAccepted is {} previous)await outbox.UpdateAsync(scope,id,j=>{if(j.LastAccepted is null||previous.CapturedAt>j.LastAccepted.CapturedAt)j.LastAccepted=previous;});
            var row=await outbox.GetAsync(scope,id);state.LastAccepted=row?.LastAccepted;
            state.Pending=await outbox.PointsAsync(scope,id,60,DateTimeOffset.UtcNow.AddMinutes(-59).ToUnixTimeMilliseconds());
            if(saved is not null)SecureStorage.Default.Remove(key);
        }
        await gate.WaitAsync();
        try
        {
            tripId=id;trackingScope=scope;storageKey=key;pending.Clear();pending.AddRange(state.Pending??[]);TrimMemory();
            pointFilter.Reset(state.LastAccepted??pending.LastOrDefault());uploadBackoff.Succeeded();while(retrySignal.Wait(0)){}lifetime=new();
            platform.PointCaptured+=OnPoint;
            try{await platform.StartAsync();}catch{platform.PointCaptured-=OnPoint;tripId=null;trackingScope=null;lifetime.Dispose();lifetime=null;throw;}
            _=RetryPendingAsync(id,lifetime.Token);_=HeartbeatLoopAsync(id,lifetime.Token);
            MainThread.BeginInvokeOnMainThread(()=>DeviceDisplay.Current.KeepScreenOn=Preferences.Default.Get("journey-screen-awake",false));SetHealth("Location sharing active");
        }
        finally{gate.Release();}
        if(preparedPoint is not null)OnPoint(preparedPoint);if(pending.Count>0)SignalRetry();
    }
    private void SignalRetry(){if(retrySignal.CurrentCount==0)retrySignal.Release();}
    private async void OnPoint(GpsPoint point)
    {
        var expected=tripId;if(point is null||expected is null)return;
        try
        {
            await gate.WaitAsync();
            try
            {
                if(tripId!=expected||trackingScope is {} s&&OfflineScope.From(auth.User)!=s)return;
                var previous=pointFilter.LastAccepted;if(!pointFilter.Accept(point))return;
                try{if(outbox is not null&&trackingScope is {} scope)await outbox.CaptureAsync(scope,expected,point);}
                catch{pointFilter.Reset(previous);throw;}
                LastLocation=$"±{point.Accuracy:0} m · {DateTimeOffset.FromUnixTimeMilliseconds(point.CapturedAt).ToLocalTime():g}";
                pending.Add(point);TrimMemory();await PersistLegacyAsync();
            }
            finally{gate.Release();}
            if(uploadBackoff.CanAttempt(DateTimeOffset.UtcNow))await TryFlushAsync();
        }
        catch(OperationCanceledException){}
        catch(Exception ex)
        {
            if(tripId!=expected)return;
            SetHealth("Location upload pending · check connection and permissions");
            if(DateTimeOffset.UtcNow-lastFeedbackAt>TimeSpan.FromSeconds(30)){lastFeedbackAt=DateTimeOffset.UtcNow;await UserFeedback.ReportAsync(ex);}
        }
    }
    private async Task HeartbeatLoopAsync(string expected,CancellationToken ct)
    {
        try
        {
            while(true)
            {
                await Task.Delay(options.HeartbeatInterval,ct);if(tripId!=expected)return;
                string motion;await gate.WaitAsync(ct);try{motion=pointFilter.IsMoving(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())?"moving":"stationary";}finally{gate.Release();}
                try{await publisher.HeartbeatAsync(expected,motion,ct);if(pending.Count==0)SetHealth("Location sharing active");}
                catch(OperationCanceledException)when(ct.IsCancellationRequested){return;}
                catch(Exception){SetHealth("Live connection interrupted · retrying");}
            }
        }
        catch(OperationCanceledException)when(ct.IsCancellationRequested){}
    }
    private async Task RetryPendingAsync(string expected,CancellationToken ct)
    {
        try
        {
            while(true)
            {
                await retrySignal.WaitAsync(ct);
                while(true)
                {
                    await Task.Delay(options.RetryInterval,ct);if(tripId!=expected||pending.Count==0)break;
                    if(!uploadBackoff.CanAttempt(DateTimeOffset.UtcNow))continue;
                    try{await TryFlushAsync();}catch(OperationCanceledException)when(ct.IsCancellationRequested){return;}catch(Exception){SetHealth("Location upload pending · check connection and permissions");}
                }
            }
        }
        catch(OperationCanceledException)when(ct.IsCancellationRequested){}
    }
    private void TrimMemory()
    {
        var cutoff=DateTimeOffset.UtcNow.AddMinutes(-59).ToUnixTimeMilliseconds();pending.RemoveAll(p=>p.CapturedAt<cutoff);
        // This is only the live-upload window. SQLite retains older unacknowledged points for historical sync.
        if(pending.Count>1800)pending.RemoveRange(0,pending.Count-1800);
    }
    private Task PersistLegacyAsync()=>outbox is not null||storageKey is null?Task.CompletedTask:SecureStorage.Default.SetAsync(storageKey,JsonSerializer.Serialize(new TrackingQueueState{Pending=pending,LastAccepted=pointFilter.LastAccepted}));
    private async Task FlushCoreAsync(CancellationToken ct)
    {
        while(true)
        {
            string? expected;OfflineScope? scope;List<GpsPoint> sent;
            await gate.WaitAsync(ct);
            try
            {
                expected=tripId;scope=trackingScope;if(expected is null)return;TrimMemory();
                if(pending.Count==0&&outbox is not null&&scope is not null)pending.AddRange(await outbox.PointsAsync(scope,expected,60,DateTimeOffset.UtcNow.AddMinutes(-59).ToUnixTimeMilliseconds()));
                sent=pending.Take(60).ToList();if(sent.Count==0)return;
            }
            finally{gate.Release();}
            var ack=await publisher.PublishAsync(expected,sent,ct);
            var acknowledged=sent.Select(p=>p.CapturedAt).Intersect(ack.AcknowledgedCapturedAt??[]).ToArray();
            if(acknowledged.Length==0)throw new InvalidDataException("Location upload was not acknowledged. It will be retried.");
            await gate.WaitAsync(ct);
            try
            {
                if(outbox is not null&&scope is not null)await outbox.AcknowledgeAsync(scope,expected,acknowledged);
                if(tripId==expected){pending.RemoveAll(p=>acknowledged.Contains(p.CapturedAt));await PersistLegacyAsync();}
                if(ack.HistoryIntervalMs>0)HistoryIntervalMs=ack.HistoryIntervalMs;
            }
            finally{gate.Release();}
        }
    }
    private async Task TryFlushAsync()
    {
        if(!await uploadGate.WaitAsync(0))return;
        try{await FlushCoreAsync(lifetime?.Token??default);uploadBackoff.Succeeded();SetHealth("Location sharing active");}
        catch{uploadBackoff.Failed(DateTimeOffset.UtcNow);SignalRetry();throw;}
        finally{uploadGate.Release();}
    }
    public async Task FlushAsync(){await uploadGate.WaitAsync();try{await FlushCoreAsync(lifetime?.Token??default);}finally{uploadGate.Release();}}
    public async Task EndLocallyAsync(Trip trip, GpsPoint? stageLocation = null)
    {
        if(offline is null)throw new InvalidOperationException("Offline completion is unavailable.");
        await gate.WaitAsync();
        try
        {
            if(tripId is not null&&tripId!=trip.Id)throw new InvalidOperationException("Finish the current journey before starting another.");
            // Atomic durable completion precedes stopping the sensor or canceling uploads.
            await offline.QueueCompletionAsync(trip, stageLocation);lifetime?.Cancel();platform.Stop();platform.PointCaptured-=OnPoint;tripId=null;
        }
        finally{gate.Release();}
        await FinishStopAsync();SetHealth("Journey ended · awaiting synchronization");
    }
    public async Task StopAsync(bool discard=false)
    {
        if(!discard)await FlushAsync();
        await gate.WaitAsync();try{lifetime?.Cancel();platform.Stop();platform.PointCaptured-=OnPoint;tripId=null;}finally{gate.Release();}
        await FinishStopAsync();SetHealth("Location sharing is off");
    }
    private async Task FinishStopAsync()
    {
        await uploadGate.WaitAsync();
        try
        {
            await publisher.StopAsync();await gate.WaitAsync();
            try
            {
                // Never delete durable unacknowledged GPS on logout, account switch or local Finish.
                if(outbox is null&&storageKey is not null)SecureStorage.Default.Remove(storageKey);
                storageKey=null;trackingScope=null;pending.Clear();pointFilter.Reset();uploadBackoff.Succeeded();
                lifetime?.Dispose();lifetime=null;preparedAt=default;preparedPoint=null;LastLocation="";
                MainThread.BeginInvokeOnMainThread(()=>DeviceDisplay.Current.KeepScreenOn=false);
            }
            finally{gate.Release();}
        }
        finally{uploadGate.Release();}
    }
    private void SetHealth(string text){Health=text;MainThread.BeginInvokeOnMainThread(()=>HealthChanged?.Invoke());}
}
