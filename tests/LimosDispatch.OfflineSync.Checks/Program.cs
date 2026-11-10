global using LimosDispatch.Core.Models;
global using LimosDispatch.Core.Services;
using LimosDispatch.Core.GenericRepository;
using LimosDispatch.Mobile.Services;
using System.Net;
using System.Text;
using System.Text.Json;

var count=0;void Check(bool yes,string name){if(!yes)throw new Exception(name);count++;Console.WriteLine("PASS "+name);}
var directory=Path.Combine(Path.GetTempPath(),"LimosDispatch-offline-checks-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
var path=Path.Combine(directory,"queue.db3");var key=System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
var store=new SqliteOfflineSyncStore(path,()=>Task.FromResult(key));
using var http=new HttpClient{BaseAddress=new("https://qa.invalid/")};var auth=new AuthService(http,new Session());await auth.GetTokenAsync();
UserProfile User(string company="lux",string id="qa-user")=>new(){Company=company,Id=id,DriverId="qa-driver",Role="Driver",Name="MOBILE QA"};
auth.SetUser(User());var scope=OfflineScope.From(auth.User)!;var api=new FakeApi();
OfflineSyncService Engine(IOfflineSyncStore? source=null)=>new(auth,source??store,s=>{if(s!=OfflineScope.From(auth.User))throw new Exception("Wrong account");return api;}){RequestSpacing=TimeSpan.Zero};
var sync=Engine();Check((await sync.CapabilitiesAsync()) is null,"Unknown server capability never enables offline completion");
await sync.CapabilitiesAsync(refresh:true);Check((await sync.CapabilitiesAsync())!.Supported,"Supported capabilities persist per authenticated scope");
Trip Trip(string id)=>new(){Id=id,Name="Private Passenger QA",DriverId="qa-driver",Status="Onboard",IsTest=true};
var t=Trip("qa-one");await sync.RememberAsync(t);
var now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();var p1=new GpsPoint(now-6000,29.98,-95.34,5);var p2=p1 with{CapturedAt=now-3000,Latitude=29.981};
await store.CaptureAsync(scope,t.Id,p1);await store.CaptureAsync(scope,t.Id,p2);await store.CaptureAsync(scope,t.Id,p2);
Check((await store.GetAsync(scope,t.Id))!.PendingPoints==2,"Repeated sensor timestamp does not create a second local point");
var first=await sync.QueueCompletionAsync(t);var repeated=await sync.QueueCompletionAsync(t);
Check(first.Completion==repeated.Completion&&first.Completion!.LastLocationCapturedAt==p2.CapturedAt,"Local Finish is atomic and retains its original identifier, time and last GPS timestamp");
try{await store.CaptureAsync(scope,t.Id,p2 with{CapturedAt=now});throw new Exception("Capture should fail");}catch(InvalidOperationException){Check(true,"Locally ended journey cannot capture new locations");}
Check((await sync.JourneyAsync(t.Id))!.PendingPoints==2,"Stopping after local Finish does not discard unacknowledged GPS");
var reopened=new SqliteOfflineSyncStore(path,()=>Task.FromResult(key));sync=Engine(reopened);
Check((await sync.JourneyAsync(t.Id))!.Completion==first.Completion,"A new store and engine recover the exact saved completion after restart");
Check((await reopened.PointsAsync(scope,t.Id,60)).Count==2,"GPS survives a new database connection");
byte[] ReadShared(string file){using var input=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);using var output=new MemoryStream();input.CopyTo(output);return output.ToArray();}
var bytes=ReadShared(path).Concat(File.Exists(path+"-wal")?ReadShared(path+"-wal"):[]).ToArray();
Check(!Encoding.UTF8.GetString(bytes).Contains("Private Passenger QA")&&!Encoding.UTF8.GetString(bytes).Contains("qa-driver"),"SQLite protects passenger and identity payloads rather than storing their JSON in plaintext");
Check((await reopened.ListAsync(new("another-company","qa-user","qa-driver"))).Count==0,"Other-company scope cannot enumerate this account queue");
Check((await reopened.ListAsync(new("lux","other-user","qa-driver"))).Count==0,"Other-user scope cannot enumerate this account queue");
api.TimeoutAfterCompletion=true;await sync.SynchronizeAsync();
var timed=await sync.JourneyAsync(t.Id);Check(timed!.Receipt is null&&timed.Completion==first.Completion&&timed.PendingPoints==2,"Ambiguous completion response retains the exact request and points");
await sync.SynchronizeAsync(manual:true);var done=await sync.JourneyAsync(t.Id);
Check(done!.Synced&&done.PendingPoints==0&&done.Receipt!.AlreadyApplied,"Replay recovers one server completion and clears acknowledged GPS only");
Check(api.CompletionEffects==1&&api.CompletionBodies.Distinct().Count()==1,"Retry never changes the completion body or repeats its server side effects");
Check(api.HistoryCalls>0&&api.HistoryPaths.All(p=>p.EndsWith("/location-history-sync")),"Historical synchronization never produces a live position event");
var fresh=Trip("qa-two");await sync.RememberAsync(fresh);await store.CaptureAsync(scope,fresh.Id,p1);await store.CaptureAsync(scope,fresh.Id,p2);await sync.QueueCompletionAsync(fresh);
api.TimeoutAfterBatch=true;await sync.SynchronizeAsync();var pending=await sync.JourneyAsync(fresh.Id);var batchId=pending!.Batch!.BatchId;
Check(pending.PendingPoints==2&&pending.Batch is not null,"Ambiguous batch response keeps its UUID and frozen point order");
sync=Engine(new SqliteOfflineSyncStore(path,()=>Task.FromResult(key)));await sync.SynchronizeAsync(manual:true);
Check((await sync.JourneyAsync(fresh.Id))!.Synced&&api.BatchBodies.Where(s=>s.Contains(batchId)).Distinct().Count()==1,"A restarted engine replays the identical GPS batch and recognizes its acknowledgement");
var partial=Trip("qa-partial");await sync.RememberAsync(partial);await store.CaptureAsync(scope,partial.Id,p1);await store.CaptureAsync(scope,partial.Id,p2);await sync.QueueCompletionAsync(partial);api.RejectTimestamp=p2.CapturedAt;
await sync.SynchronizeAsync(manual:true);var review=await sync.JourneyAsync(partial.Id);
Check(review!.Receipt is not null&&review.PendingPoints==1&&review.RejectedPoints==1&&!review.Synced,"Partial response removes only ACKed data and retains rejected GPS for review");
Check((await store.PointsAsync(scope,partial.Id,60)).Count==0&&review.Status=="Review required","Permanently rejected GPS is excluded from automatic retransmission without deletion");
api.RejectTimestamp=null;
var conflict=Trip("qa-conflict");await sync.RememberAsync(conflict);await sync.QueueCompletionAsync(conflict);api.ConflictTrip=conflict.Id;await sync.SynchronizeAsync(manual:true);
var blocked=await sync.JourneyAsync(conflict.Id);Check(blocked!.Blocked&&blocked.ErrorCode=="TRIP_CANCELLED"&&blocked.Receipt is null,"A cancelled-trip conflict is retained as review rather than fake success");
var calls=api.CompletionBodies.Count;await sync.SynchronizeAsync(manual:true);Check(api.CompletionBodies.Count==calls,"Automatic/manual bulk synchronization does not hammer terminal conflicts");api.ConflictTrip=null;
var unauthorized=Trip("qa-401");await sync.RememberAsync(unauthorized);await sync.QueueCompletionAsync(unauthorized);api.Unauthorized=true;await sync.SynchronizeAsync(manual:true);
Check((await sync.JourneyAsync(unauthorized.Id))!.SignInRequired&&(await sync.JourneyAsync(unauthorized.Id))!.Completion is not null,"401 retains the completion for the original account to sign in again");api.Unauthorized=false;
await sync.SynchronizeAsync(manual:true);Check((await sync.JourneyAsync(unauthorized.Id))!.Synced,"The original account can resume a job after authorization returns");
var rate=Trip("qa-rate");await sync.RememberAsync(rate);await sync.QueueCompletionAsync(rate);api.RateLimit=true;await sync.SynchronizeAsync(manual:true);
var limited=await sync.JourneyAsync(rate.Id);Check(!limited!.Blocked&&limited.NextAttemptAt>DateTimeOffset.UtcNow.AddSeconds(80),"429 persists Retry-After and retains work without a terminal rejection");api.RateLimit=false;
using(var cancellation=new CancellationTokenSource()){cancellation.Cancel();try{await sync.SynchronizeAsync(cancellation.Token);}catch(OperationCanceledException){}}
Check((await sync.JourneyAsync(rate.Id))!.Completion is not null,"Lifecycle cancellation does not erase saved work");
var denied=new OfflineScopedTokens(auth,new("different","qa-user","qa-driver"));try{await denied.GetTokenAsync();throw new Exception("Expected denial");}catch(UnauthorizedAccessException){Check(true,"Bound token provider refuses a different company or account");}
var current=await store.GetAsync(scope,rate.Id);var req=current!.Completion;await auth.ForgetSessionAsync();Check((await store.GetAsync(scope,rate.Id))!.Completion==req,"Sign-out clears the auth session without deleting encrypted synchronization work");
using(var response=new HttpResponseMessage((HttpStatusCode)429){Content=new StringContent("{\"code\":\"SYNC_RETRY\",\"message\":\"Try later\",\"retryable\":true,\"requestId\":\"qa-id\",\"currentTripStatus\":\"Onboard\"}")}){
 response.Headers.RetryAfter=new(TimeSpan.FromSeconds(90));try{await ApiRepository.Read<object>(response,default);throw new Exception("Expected structured API error");}catch(ApiException e){Check(e.Code=="SYNC_RETRY"&&e.Retryable==true&&e.RequestId=="qa-id"&&e.CurrentTripStatus=="Onboard"&&e.RetryAfter==TimeSpan.FromSeconds(90),"Repository preserves structured server conflicts and Retry-After for safe synchronization");}
}
var viewNow=DateTimeOffset.UtcNow;
Check(done!.SynchronizedAt>0,"The durable queue records when the final acknowledgement completes synchronization");
var display=new OfflineJourney{Receipt=new(){RecordedAt=viewNow.AddDays(-3).ToUnixTimeMilliseconds()},SynchronizedAt=viewNow.AddHours(-23).ToUnixTimeMilliseconds()};
Check(display.ShowInSynchronization(viewNow),"A completed job remains visible for 24 hours after final GPS acknowledgement, not its older receipt");
display.SynchronizedAt=viewNow.AddHours(-24).ToUnixTimeMilliseconds();Check(!display.ShowInSynchronization(viewNow),"A fully synchronized job is hidden at the 24-hour boundary");
display.PendingPoints=1;Check(display.ShowInSynchronization(viewNow),"Pending data remains visible regardless of age");
display.PendingPoints=0;display.Blocked=true;Check(display.ShowInSynchronization(viewNow),"Items requiring review remain visible regardless of age");
display.Blocked=false;display.SynchronizedAt=0;Check(!display.ShowInSynchronization(viewNow),"Older saved successful jobs use their server receipt time without database deletion");
Console.WriteLine(count+" offline synchronization checks passed (real encrypted SQLite, simulated API).");

sealed class Session:ISessionStore
{public Task<TokenPair?> LoadAsync()=>Task.FromResult<TokenPair?>(new(){Company="lux",AccessToken="mock",RefreshToken="mock",ExpiresAt=DateTimeOffset.UtcNow.AddHours(1)});public Task SaveAsync(TokenPair p)=>Task.CompletedTask;public Task ClearAsync()=>Task.CompletedTask;}
sealed class FakeApi:IGenericRepository
{
 public bool TimeoutAfterCompletion,TimeoutAfterBatch,Unauthorized,RateLimit;public string? ConflictTrip;public long? RejectTimestamp;
 public int CompletionEffects,HistoryCalls;public List<string> CompletionBodies=[],BatchBodies=[],HistoryPaths=[];
 private readonly Dictionary<string,OfflineCompletionReceipt> completions=[];private readonly Dictionary<string,OfflineHistoryReceipt> batches=[];
 public Task<T> GetAsync<T>(string path,CancellationToken ct=default)=>Task.FromResult((T)(object)new OfflineCapabilities{Version=1,CompletionSync=true,LocationHistorySync=true});
 public Task<T> PostAsync<T>(string path,object body,CancellationToken ct=default)
 {
  ct.ThrowIfCancellationRequested();var id=path.Split('/')[2];
  if(Unauthorized)throw new ApiException(HttpStatusCode.Unauthorized,"Sign in");
  if(RateLimit)throw new ApiException(HttpStatusCode.TooManyRequests,"Wait"){RetryAfter=TimeSpan.FromSeconds(90),Code="RATE_LIMITED"};
  if(id==ConflictTrip)throw new ApiException(HttpStatusCode.Conflict,"Cancelled"){Code="TRIP_CANCELLED"};
  if(body is OfflineCompletionRequest request)
  {
   CompletionBodies.Add(JsonSerializer.Serialize(request));var duplicate=completions.ContainsKey(request.RequestId);
   if(!duplicate){completions[request.RequestId]=new(){RequestId=request.RequestId,TripId=id,TripStatus="Completed",CompletedAt=request.CompletedAt,RecordedAt=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),CompletionReceiptId=Guid.NewGuid().ToString(),ClientSessionId=request.ClientSessionId};CompletionEffects++;}
   var receipt=JsonSerializer.Deserialize<OfflineCompletionReceipt>(JsonSerializer.Serialize(completions[request.RequestId]))!;receipt.AlreadyApplied=duplicate;
   if(TimeoutAfterCompletion){TimeoutAfterCompletion=false;throw new TimeoutException("Response lost after commit");}
   return Task.FromResult((T)(object)receipt);
  }
  var batch=(OfflineHistoryBatch)body;HistoryCalls++;HistoryPaths.Add(path);BatchBodies.Add(JsonSerializer.Serialize(batch));
  if(!batches.TryGetValue(batch.BatchId,out var result))
  {
   result=new(){BatchId=batch.BatchId,AcknowledgedCapturedAt=batch.Points.Where(p=>p.CapturedAt!=RejectTimestamp).Select(p=>p.CapturedAt).ToList(),RejectedPoints=batch.Points.Where(p=>p.CapturedAt==RejectTimestamp).Select(p=>new OfflinePointRejection{CapturedAt=p.CapturedAt,Code="POINT_CONFLICT",Retryable=false}).ToList()};batches[batch.BatchId]=result;
  }
  if(TimeoutAfterBatch){TimeoutAfterBatch=false;throw new TimeoutException("Batch response lost");}return Task.FromResult((T)(object)result);
 }
 public Task<T> PutAsync<T>(string p,object b,CancellationToken ct=default)=>throw new NotSupportedException();
 public Task<T> DeleteAsync<T>(string p,CancellationToken ct=default)=>throw new NotSupportedException();
 public Task<T> DeleteAsync<T>(string p,object b,CancellationToken ct=default)=>throw new NotSupportedException();
}
