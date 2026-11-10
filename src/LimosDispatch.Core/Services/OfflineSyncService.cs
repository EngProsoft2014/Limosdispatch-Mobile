using System.Net;
using System.Text.Json;
using LimosDispatch.Core.Models;
using LimosDispatch.Core.GenericRepository;
namespace LimosDispatch.Core.Services;
public sealed class OfflineSyncService(AuthService auth,IOfflineSyncStore store,Func<OfflineScope,IGenericRepository> repository)
{
    private readonly SemaphoreSlim gate=new(1,1);
    private DateTimeOffset nextRequest;
    public event Action? Changed;
    public TimeSpan RequestSpacing{get;init;}=TimeSpan.FromMilliseconds(2200);
    public OfflineScope? Scope=>OfflineScope.From(auth.User);
    private bool Current(OfflineScope scope)=>Scope==scope;
    private void Notify(){try{Changed?.Invoke();}catch(Exception){}}
    private static Trip Clone(Trip trip)=>JsonSerializer.Deserialize<Trip>(JsonSerializer.Serialize(trip,ApiRepository.Json),ApiRepository.Json)!;
    public async Task<OfflineCapabilities?> CapabilitiesAsync(CancellationToken ct=default,bool refresh=false)
    {
        var scope=Scope;if(scope is null)return null;
        var cached=await store.CapabilitiesAsync(scope);
        if(!refresh)return cached;
        try
        {
            var value=await repository(scope).GetAsync<OfflineCapabilities>("api/driver/offline-sync/capabilities",ct);
            if(!Current(scope))return null;
            await store.SaveCapabilitiesAsync(scope,value);return value;
        }
        catch(ApiException e)when((int)e.Status is 404 or 405){var unsupported=new OfflineCapabilities();await store.SaveCapabilitiesAsync(scope,unsupported);return unsupported;}
        catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
        catch(HttpRequestException){return cached;}
        catch(TimeoutException){return cached;}
        catch(ApiException e)when((int)e.Status>=500){return cached;}
    }
    public async Task RememberAsync(Trip trip)
    {
        var scope=Scope;if(scope is null||trip.DriverId!=scope.DriverId)return;
        var snapshot=Clone(trip);
        await store.UpdateAsync(scope,trip.Id,j=>{if(!j.EndedLocally)j.Snapshot=snapshot;});
    }
    public async Task<OfflineJourney?> JourneyAsync(string id)=>Scope is {} s?await store.GetAsync(s,id):null;
    public async Task<List<OfflineJourney>> JourneysAsync()=>Scope is {} s?await store.ListAsync(s):[];
    public async Task<bool> EndedAsync(string id)=>(await JourneyAsync(id))?.EndedLocally==true;
    public static Trip Project(OfflineJourney j)
    {
        var trip=Clone(j.Snapshot);
        if(j.EndedLocally){trip.Status="Completed";trip.LocalSyncStatus=j.Synced?"":j.Status;}
        return trip;
    }
    public async Task OverlayAsync(List<Trip> trips)
    {
        var records=(await JourneysAsync()).Where(j=>j.EndedLocally&&!j.Synced).ToList();
        foreach(var j in records){var index=trips.FindIndex(t=>t.Id==j.TripId);if(index>=0)trips[index]=Project(j);else if(!j.Synced)trips.Add(Project(j));}
    }
    public async Task<OfflineJourney> QueueCompletionAsync(Trip trip)
    {
        var scope=Scope??throw new UnauthorizedAccessException();
        if(trip.DriverId!=scope.DriverId||trip.Status!="Onboard")throw new InvalidOperationException("Only the assigned chauffeur can finish an onboard journey.");
        var caps=await CapabilitiesAsync();
        if(caps?.Supported!=true)throw new InvalidOperationException("Offline completion is unavailable. Connect to check server support first.");
        var completedAt=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();var snapshot=Clone(trip);
        await store.UpdateAsync(scope,trip.Id,j=>
        {
            if(j.EndedLocally)return;
            j.Snapshot=snapshot;j.Capabilities=caps;
            j.Completion=new(Guid.NewGuid().ToString(),completedAt,Guid.NewGuid().ToString(),j.LastAccepted?.CapturedAt);
            j.ErrorCode="";j.Blocked=false;j.SignInRequired=false;j.NextAttemptAt=default;
        });
        Notify();return (await store.GetAsync(scope,trip.Id))!;
    }
    private async Task BeforeRequest(OfflineScope scope,CancellationToken ct)
    {
        if(!Current(scope))throw new OperationCanceledException(ct);
        var delay=nextRequest-DateTimeOffset.UtcNow;if(delay>TimeSpan.Zero)await Task.Delay(delay,ct);
        if(!Current(scope))throw new OperationCanceledException(ct);
        nextRequest=DateTimeOffset.UtcNow+RequestSpacing;
    }
    public async Task SynchronizeAsync(CancellationToken ct=default,bool manual=false)
    {
        if(!await gate.WaitAsync(0,ct))return;
        try
        {
            var scope=Scope;if(scope is null)return;
            var api=repository(scope);var jobs=(await store.ListAsync(scope)).Where(j=>j.EndedLocally&&!j.Synced&&!j.Blocked).ToList();
            // Complete every eligible journey before spending time on route backlog.
            foreach(var job in jobs.Where(j=>j.Receipt is null))
            {
                if(!Current(scope)||ct.IsCancellationRequested)return;
                if(!manual&&job.NextAttemptAt>DateTimeOffset.UtcNow)continue;
                try
                {
                    await BeforeRequest(scope,ct);
                    var receipt=await api.PostAsync<OfflineCompletionReceipt>($"api/trips/{Uri.EscapeDataString(job.TripId)}/completion-sync",job.Completion!,ct);
                    if(receipt.RequestId!=job.Completion!.RequestId||receipt.TripId!=job.TripId||receipt.TripStatus!="Completed"||string.IsNullOrWhiteSpace(receipt.CompletionReceiptId)||receipt.CompletedAt!=job.Completion.CompletedAt||(receipt.ClientSessionId is {} session&&session!=job.Completion.ClientSessionId))throw new InvalidDataException("The completion acknowledgement did not match the saved request.");
                    await store.UpdateAsync(scope,job.TripId,j=>{j.Receipt=receipt;j.ErrorCode="";j.SignInRequired=false;j.Failures=0;j.NextAttemptAt=default;});Notify();
                }
                catch(OperationCanceledException)when(ct.IsCancellationRequested||!Current(scope)){return;}
                catch(Exception error){await Failed(scope,job.TripId,error);if(error is ApiException e&&(int)e.Status is 401 or 429)return;}
            }
            foreach(var original in jobs)
            {
                for(int batchNumber=0;batchNumber<5;batchNumber++)
                {
                    if(!Current(scope)||ct.IsCancellationRequested)return;
                    var job=await store.GetAsync(scope,original.TripId);
                    if(job is null||job.Receipt is null||job.Blocked||job.Synced||!manual&&job.NextAttemptAt>DateTimeOffset.UtcNow)break;
                    try
                    {
                        if(job.Completion!.CompletedAt<DateTimeOffset.UtcNow.AddDays(-(job.Capabilities?.WindowDays??7)).ToUnixTimeMilliseconds())throw new ApiException((HttpStatusCode)422,"The synchronization window has expired."){Code="SYNC_WINDOW_EXPIRED"};
                        var batch=job.Batch;
                        if(batch is null)
                        {
                            var points=await store.PointsAsync(scope,job.TripId,Math.Clamp(job.Capabilities?.MaxBatchPoints??60,1,60));
                            if(points.Count==0)break;
                            batch=new(Guid.NewGuid().ToString(),job.Completion.RequestId,job.Receipt.CompletionReceiptId,points);
                            while(JsonSerializer.SerializeToUtf8Bytes(batch,ApiRepository.Json).Length>(job.Capabilities?.MaxRequestBytes??16384)&&batch.Points.Count>1)batch.Points.RemoveAt(batch.Points.Count-1);
                            if(JsonSerializer.SerializeToUtf8Bytes(batch,ApiRepository.Json).Length>(job.Capabilities?.MaxRequestBytes??16384))throw new InvalidDataException("The saved GPS batch exceeds the server limit.");
                            var frozen=batch;await store.UpdateAsync(scope,job.TripId,j=>j.Batch=frozen);
                        }
                        await BeforeRequest(scope,ct);
                        var response=await api.PostAsync<OfflineHistoryReceipt>($"api/trips/{Uri.EscapeDataString(job.TripId)}/location-history-sync",batch,ct);
                        Validate(batch,response);await store.ApplyBatchAsync(scope,job.TripId,batch,response);Notify();
                    }
                    catch(OperationCanceledException)when(ct.IsCancellationRequested||!Current(scope)){return;}
                    catch(Exception error){await Failed(scope,job.TripId,error);if(error is ApiException e&&(int)e.Status is 401 or 429)return;break;}
                }
            }
        }
        catch(OperationCanceledException)when(ct.IsCancellationRequested){}
        finally{gate.Release();}
    }
    private static void Validate(OfflineHistoryBatch batch,OfflineHistoryReceipt response)
    {
        var sent=batch.Points.Select(p=>p.CapturedAt).ToHashSet();
        var ack=(response.AcknowledgedCapturedAt??[]).ToHashSet();var rejected=response.RejectedPoints??[];
        if(response.BatchId!=batch.BatchId||ack.Any(t=>!sent.Contains(t))||rejected.Any(p=>!sent.Contains(p.CapturedAt)||ack.Contains(p.CapturedAt))||sent.Any(t=>!ack.Contains(t)&&!rejected.Any(p=>p.CapturedAt==t)))throw new InvalidDataException("The GPS acknowledgement did not account for the saved batch.");
    }
    private async Task Failed(OfflineScope scope,string tripId,Exception error)
    {
        if(!Current(scope))return;
        await store.UpdateAsync(scope,tripId,j=>
        {
            j.Failures=Math.Min(8,j.Failures+1);
            var api=error as ApiException;var status=(int?)(api?.Status);
            j.ErrorCode=api?.Code is {Length:>0}?api.Code:error is UnauthorizedAccessException?"SIGN_IN_REQUIRED":"SYNC_RETRY";
            j.SignInRequired=status==401||error is UnauthorizedAccessException;
            j.Blocked=status is 400 or 403 or 404 or 405 or 409 or 413 or 422;
            var seconds=Math.Min(300,15*Math.Pow(2,j.Failures-1));
            j.NextAttemptAt=DateTimeOffset.UtcNow+TimeSpan.FromSeconds(Math.Max(seconds,api?.RetryAfter?.TotalSeconds??0));
            if(status==429)nextRequest=j.NextAttemptAt;
        });Notify();
    }
}
public sealed class OfflineScopedTokens(AuthService auth,OfflineScope scope):IAccessTokenProvider
{
    private void Check(){if(OfflineScope.From(auth.User)!=scope)throw new UnauthorizedAccessException("Sign in to the original account to synchronize.");}
    public async Task<string> GetTokenAsync(CancellationToken ct=default){Check();var token=await auth.GetTokenAsync(ct);Check();return token;}
    public async Task<string> RefreshAfterRejectionAsync(string rejected,CancellationToken ct=default){Check();var token=await auth.RefreshAfterRejectionAsync(rejected,ct);Check();return token;}
}
