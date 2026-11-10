using System.Collections.ObjectModel;
using LimosDispatch.Core.Helpers;
namespace LimosDispatch.Mobile.ViewModels;
public sealed class TrackingViewModel(DispatchService dispatch,DispatchTrackingStream stream,RealtimeService realtime) : BaseViewModel
{
    private CancellationTokenSource? lifetime;
    private LiveTrackingState live=new();
    private List<Trip> trips=[];
    private readonly Dictionary<string,string> renderedFreshness=[];
    private bool mapMode,fullScreen;
    private DateTime? updatedAt;
    private DateTimeOffset metadataAt,metadataAttemptAt,fallbackAt,refreshRequested,nextConnectAt;
    private int connectFailures;
    private readonly SemaphoreSlim metadataGate=new(1,1);
    public bool MapMode{get=>mapMode;set{if(Set(ref mapMode,value)){if(!value)FullScreen=false;Raise(nameof(ListMode));}}}
    public bool ListMode=>!MapMode;
    public bool FullScreen{get=>fullScreen;set{if(Set(ref fullScreen,value))Raise(nameof(ShowHeader));}}
    public bool ShowHeader=>!FullScreen;
    public string TripId{get;set;}="";
    public ObservableCollection<TrackedDriver> Drivers{get;}=[];
    public event Action? LocationsChanged;
    public event Action? StatusChanged;
    public string Updated=>updatedAt is {} time?LocalizationService.Format("Updated {0} · {1} on the road",time.ToString("HH:mm:ss"),Drivers.Count):LocalizationService.Text("Waiting for positions");
    public string UpdateMode=>LocalizationService.Text(stream.IsConnected?"Live updates connected":IsBusy?"Connecting live tracking…":"Live connection interrupted · periodic refresh");
    public void Start()
    {
        Stop();live=new();trips=[];renderedFreshness.Clear();
        metadataAt=metadataAttemptAt=fallbackAt=refreshRequested=nextConnectAt=default;connectFailures=0;lifetime=new();
        stream.PositionReceived+=OnPosition;stream.HeartbeatReceived+=OnHeartbeat;stream.SnapshotReceived+=OnSnapshot;stream.StateChanged+=OnState;
        realtime.ForegroundChanged+=OnForeground;
        RefreshCommand=new(LoadAsync);_=SessionAsync(lifetime.Token);
    }
    public void Stop()
    {
        lifetime?.Cancel();lifetime?.Dispose();lifetime=null;
        stream.PositionReceived-=OnPosition;stream.HeartbeatReceived-=OnHeartbeat;stream.SnapshotReceived-=OnSnapshot;stream.StateChanged-=OnState;
        realtime.ForegroundChanged-=OnForeground;_=stream.StopAsync();
    }
    private void Post(Action action)
    {
        var expected=lifetime;
        MainThread.BeginInvokeOnMainThread(()=>
        {
            if(expected is null||expected!=lifetime||expected.IsCancellationRequested)return;
            try{action();}catch(Exception ex){_=UserFeedback.ReportAsync(ex);}
        });
    }
    private void OnForeground(bool foreground)
    {if(!foreground)_=stream.StopAsync();else if(lifetime is {} current)_=ReconnectAsync(current.Token);}
    private async Task ReconnectAsync(CancellationToken ct)
    {try{await RefreshMetadataAsync(ct);await ConnectAsync(ct,force:true);}catch(OperationCanceledException)when(ct.IsCancellationRequested){}catch(Exception){Post(()=>Raise(nameof(UpdateMode)));}}
    private async Task ConnectAsync(CancellationToken ct,bool force=false)
    {
        if(ct.IsCancellationRequested||!realtime.IsForeground||stream.IsConnected||!force&&DateTimeOffset.UtcNow<nextConnectAt)return;
        nextConnectAt=DateTimeOffset.UtcNow.AddSeconds(15);
        await stream.StartAsync(ct);
        if(stream.IsConnected){connectFailures=0;nextConnectAt=default;}
        else nextConnectAt=DateTimeOffset.UtcNow.AddSeconds(Math.Min(60,5*Math.Pow(2,Math.Min(++connectFailures,4))));
    }
    private async Task SessionAsync(CancellationToken ct)
    {
        try
        {
            await Run(async()=>{await RefreshMetadataAsync(ct);if(realtime.IsForeground)await ConnectAsync(ct,force:true);});Raise(nameof(UpdateMode));
            while(!ct.IsCancellationRequested)
            {
                await Task.Delay(5000,ct);
                if(!realtime.IsForeground)continue;
                try
                {
                    if(DateTimeOffset.UtcNow-metadataAttemptAt>=TimeSpan.FromSeconds(60))await RefreshMetadataAsync(ct);
                    if(!stream.IsConnected&&DateTimeOffset.UtcNow-fallbackAt>=TimeSpan.FromSeconds(15))await LoadFallbackAsync(ct);
                    await ConnectAsync(ct);
                    Reconcile(false);Raise(nameof(UpdateMode));
                }
                catch(OperationCanceledException)when(ct.IsCancellationRequested){return;}
                catch(Exception){Raise(nameof(UpdateMode));}
            }
        }
        catch(OperationCanceledException)when(ct.IsCancellationRequested){}
    }
    private async Task RefreshMetadataAsync(CancellationToken ct)
    {
        await metadataGate.WaitAsync(ct);
        try
        {
            // Coalesce resume, unknown-driver and periodic requests; failures also respect the retry interval.
            if(DateTimeOffset.UtcNow-metadataAttemptAt<TimeSpan.FromSeconds(2))return;
            metadataAttemptAt=DateTimeOffset.UtcNow;
            var response=await dispatch.TripsAsync(ct:ct);ct.ThrowIfCancellationRequested();
            trips=response.Trips.ToList();metadataAt=DateTimeOffset.UtcNow;live.Prune(trips);Reconcile(true);
            System.Diagnostics.Debug.WriteLine("[PageRefresh] tracking metadata refresh completed");
        }
        finally{metadataGate.Release();}
    }
    private async Task LoadFallbackAsync(CancellationToken ct)
    {
        fallbackAt=DateTimeOffset.UtcNow;
        var response=await dispatch.TrackingAsync(ct);ct.ThrowIfCancellationRequested();
        // Legacy read-only fallback is explicitly labelled periodic; driver uploads never use HTTP.
        var snapshot=new TrackingSnapshot{ServerNow=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()};
        foreach(var driver in response.Drivers.Where(d=>d.Location?.Sharing==true))
        {
            var trip=trips.FirstOrDefault(t=>t.IsActive&&(t.DriverId==driver.Id||t.DriverId==driver.DriverId));
            if(trip is not null)snapshot.Locations.Add(new(){TripId=trip.Id,DriverId=trip.DriverId,Latitude=driver.Latitude,Longitude=driver.Longitude,Accuracy=driver.Accuracy,StoredAt=driver.UpdatedAt});
        }
        live.Snapshot(snapshot,trips);Reconcile(true);
    }
    public override Task LoadAsync()=>Run(async()=>
    {
        var ct=lifetime?.Token??default;await RefreshMetadataAsync(ct);
        if(!realtime.IsForeground)return;
        if(stream.IsConnected)await stream.RefreshAsync(ct);else{await ConnectAsync(ct,force:true);if(!stream.IsConnected)await LoadFallbackAsync(ct);}
        Raise(nameof(UpdateMode));
    });
    private bool Known(string driver,string trip)=>trips.Any(t=>t.IsActive&&t.Id==trip&&t.DriverId==driver);
    private void RefreshUnknown(string driver,string trip)
    {
        if(Known(driver,trip)||DateTimeOffset.UtcNow-refreshRequested<TimeSpan.FromSeconds(10))return;
        refreshRequested=DateTimeOffset.UtcNow;_=Run(async()=>
        {
            var ct=lifetime?.Token??default;
            await RefreshMetadataAsync(ct);
            if(realtime.IsForeground&&stream.IsConnected)await stream.RefreshAsync(ct);
        }, showBusy:false);
    }
    private void OnPosition(LocationFrame point)=>Post(()=>
    {
        if(live.Position(point,trips)){updatedAt=DateTime.Now;Reconcile(true);Raise(nameof(Updated));}
        else RefreshUnknown(point.DriverId,point.TripId);
    });
    private void OnHeartbeat(TrackingHeartbeat beat)=>Post(()=>
    {
        if(live.Presence(beat,trips)){updatedAt=DateTime.Now;Reconcile(false);Raise(nameof(Updated));}
        else RefreshUnknown(beat.DriverId,beat.TripId);
    });
    private void OnSnapshot(TrackingSnapshot snapshot)=>Post(()=>{live.Snapshot(snapshot,trips);updatedAt=DateTime.Now;Reconcile(true);Raise(nameof(Updated));});
    private void OnState()=>Post(()=>Raise(nameof(UpdateMode)));
    private void Reconcile(bool positionChange)
    {
        var rows=live.Build(trips,TripId);var ids=rows.Select(d=>d.Id).ToHashSet();
        for(var i=Drivers.Count-1;i>=0;i--)if(!ids.Contains(Drivers[i].Id)){renderedFreshness.Remove(Drivers[i].Id);Drivers.RemoveAt(i);}
        var changed=false;
        foreach(var row in rows)
        {
            var index=Drivers.ToList().FindIndex(d=>d.Id==row.Id);var freshness=row.Freshness;
            if(index<0){Drivers.Add(row);changed=true;}
            else
            {
                var previous=Drivers[index];
                if(previous.Latitude!=row.Latitude||previous.Longitude!=row.Longitude||previous.LastContactAt!=row.LastContactAt||previous.Motion!=row.Motion||previous.Name!=row.Name||renderedFreshness.GetValueOrDefault(row.Id)!=freshness){Drivers[index]=row;changed=true;}
            }
            renderedFreshness[row.Id]=freshness;
        }
        Raise(nameof(Updated));
        if(positionChange)LocationsChanged?.Invoke();else if(changed)StatusChanged?.Invoke();
    }
}
