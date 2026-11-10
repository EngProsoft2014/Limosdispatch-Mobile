using System.Text.Json;
using LimosDispatch.Mobile.Services;
var checks=0;
void Check(bool test,string name){if(!test)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
using var http=new HttpClient{BaseAddress=new("https://qa.invalid/")};var auth=new AuthService(http,new Session());await auth.GetTokenAsync();auth.SetUser(new(){Id="qa-user",Company="lux",Role="Driver",Name="MOBILE QA"});
var publisher=new Publisher();var platform=new Platform();var service=new TrackingService(platform,publisher,auth,new(){HeartbeatInterval=TimeSpan.FromMilliseconds(120)});
LimosDispatch.Core.Helpers.UserFeedback.Presenter=_=>Task.CompletedTask;
await service.StartAsync("qa-trip");await service.FlushAsync();var fix=Geolocation.Default.Fix;var point=new GpsPoint(fix.Timestamp.ToUnixTimeMilliseconds(),fix.Latitude,fix.Longitude,5);
Check(publisher.Calls==1,"Initial fix publishes through Hub contract");
platform.Emit(point);platform.Emit(point with{CapturedAt=point.CapturedAt+1000,Latitude=29.981});await service.FlushAsync();Check(publisher.Calls==1,"Provider duplicate and early update stay suppressed after successful queue drain");
var moving=point with{CapturedAt=point.CapturedAt+2000,Latitude=29.981};platform.Emit(moving);await service.FlushAsync();Check(publisher.Calls==2,"Moving coordinates publish at two-second cadence");
platform.Emit(moving with{CapturedAt=point.CapturedAt+70000});await service.FlushAsync();Check(publisher.Calls==2,"Stopped driver never sends periodic coordinate heartbeat");
await Task.Delay(300);Check(publisher.Heartbeats>0&&publisher.Calls==2,"Presence heartbeat does not publish or persist GPS coordinates");
Check(publisher.Motions.All(m=>m is "moving" or "stationary"),"Heartbeat uses only declared motion values");
publisher.OmitAck=true;platform.Emit(moving with{CapturedAt=point.CapturedAt+4000,Latitude=29.982});await Task.Delay(80);
Check(service.Health.Contains("pending",StringComparison.OrdinalIgnoreCase)&&SecureStorage.Default.Values.Values.Any(v=>JsonDocument.Parse(v).RootElement.GetProperty("Pending").GetArrayLength()==1),"Missing acknowledgement retains pending location");
publisher.OmitAck=false;await service.FlushAsync();Check(publisher.Calls==4,"Explicit flush retries unacknowledged point without fabricating acknowledgement");
Check(SecureStorage.Default.Values.Values.All(v=>JsonDocument.Parse(v).RootElement.GetProperty("Pending").GetArrayLength()==0),"Archive inserted zero still clears explicitly acknowledged live points");
var before=publisher.Calls;await service.ResumeAsync("qa-trip");await service.FlushAsync();Check(publisher.Calls==before,"Resuming platform does not reset last accepted fix");
await service.StopAsync(true);var beats=publisher.Heartbeats;await Task.Delay(200);Check(!service.IsRunning&&publisher.Heartbeats==beats&&publisher.Stops==1,"Journey stop cancels heartbeat worker and closes Hub publisher");
publisher.FailNext=true;publisher.Recovered=new(TaskCreationOptions.RunContinuationsAsynchronously);await service.StartAsync("qa-retry");await Task.Delay(80);
Check(service.Health.Contains("pending",StringComparison.OrdinalIgnoreCase),"Offline initial publish is queued safely");
await publisher.Recovered.Task.WaitAsync(TimeSpan.FromSeconds(22));await service.FlushAsync();Check(publisher.Calls==6,"Pending point recovers without a new GPS callback");
await service.StopAsync(true);Check(!service.IsRunning&&publisher.Stops==2,"Second journey cleanup closes its transport");
Console.WriteLine(checks+" live tracking runtime checks passed (mock publisher/device adapters).");
sealed class Platform:ITrackingPlatform{public event Action<GpsPoint>? PointCaptured;public Task StartAsync()=>Task.CompletedTask;public void Stop(){}public void Emit(GpsPoint point)=>PointCaptured?.Invoke(point);}
sealed class Session:ISessionStore{public Task<TokenPair?> LoadAsync()=>Task.FromResult<TokenPair?>(new(){AccessToken="mock-access",RefreshToken="mock-refresh",Company="lux",ExpiresAt=DateTimeOffset.UtcNow.AddHours(1)});public Task SaveAsync(TokenPair value)=>Task.CompletedTask;public Task ClearAsync()=>Task.CompletedTask;}
sealed class Publisher:ITrackingPublisher
{
 public int Calls,Heartbeats,Stops;public bool FailNext,OmitAck,SawFailure;public System.Collections.Concurrent.ConcurrentQueue<string>Motions=new();public TaskCompletionSource Recovered=new(TaskCreationOptions.RunContinuationsAsynchronously);
 public Task<LocationAck> PublishAsync(string tripId,List<GpsPoint> points,CancellationToken ct=default)
 {
  Calls++;if(FailNext){FailNext=false;SawFailure=true;throw new HttpRequestException("Synthetic offline failure");}
  if(SawFailure)Recovered.TrySetResult();return Task.FromResult(new LocationAck{Inserted=0,HistoryIntervalMs=15000,AcknowledgedCapturedAt=OmitAck?[]:points.Select(p=>p.CapturedAt).ToList()});
 }
 public Task HeartbeatAsync(string tripId,string motion,CancellationToken ct=default){Heartbeats++;Motions.Enqueue(motion);return Task.CompletedTask;}
 public Task StopAsync(){Stops++;return Task.CompletedTask;}
}
