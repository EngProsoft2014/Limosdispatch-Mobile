using System.Text.Json.Serialization;
namespace LimosDispatch.Core.Models;
public sealed record OfflineScope(string Company,string UserId,string DriverId)
{
    public static OfflineScope? From(UserProfile? u)=>u is {IsDriver:true,MustChangePassword:false}&&!string.IsNullOrEmpty(u.DriverId)?new(u.Company.ToLowerInvariant(),u.Id,u.DriverId):null;
    public string Key=>Company+"|"+UserId+"|"+DriverId;
}
public sealed class OfflineCapabilities
{
    public int Version{get;set;} public bool CompletionSync{get;set;} public bool LocationHistorySync{get;set;}
    public int WindowDays{get;set;}=7; public int ClockSkewSeconds{get;set;}=30;
    public int MaxBatchPoints{get;set;}=60;public int MaxRequestBytes{get;set;}=16384;
    public int MaxSamplesPerTrip{get;set;}=150000;public int HistoryIntervalMs{get;set;}=5000;
    [JsonIgnore]public bool Supported=>Version>=1&&CompletionSync&&LocationHistorySync&&WindowDays is >=1 and <=30&&MaxBatchPoints>0&&MaxRequestBytes>=512;
}
public sealed record OfflineCompletionRequest(string RequestId,long CompletedAt,string ClientSessionId,long? LastLocationCapturedAt,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    GpsPoint? StageLocation = null);
public sealed record MileageReminder(string? Action, string? Message);

public sealed class OfflineCompletionReceipt
{
    public MileageReminder? MileageReminder { get; set; }
    public string OperationStatus{get;set;}="";public string RequestId{get;set;}="";public string TripId{get;set;}="";
    public string TripStatus{get;set;}="";public long CompletedAt{get;set;}public long RecordedAt{get;set;}
    public string CompletionReceiptId{get;set;}="";public bool AlreadyApplied{get;set;}public string? ClientSessionId{get;set;}
}
public sealed record OfflineHistoryBatch(string BatchId,string CompletionRequestId,string CompletionReceiptId,List<GpsPoint> Points);
public sealed class OfflinePointRejection
{
    public long CapturedAt{get;set;}public string Code{get;set;}="";public string Message{get;set;}="";public bool Retryable{get;set;}
}
public sealed class OfflineHistoryReceipt
{
    public string BatchId{get;set;}="";public int Inserted{get;set;}
    public List<long> AcknowledgedCapturedAt{get;set;}=[];public List<long> DuplicateCapturedAt{get;set;}=[];
    public List<OfflinePointRejection> RejectedPoints{get;set;}=[];public int HistoryIntervalMs{get;set;}
}
public sealed class OfflineJourney
{
    public OfflineScope Scope{get;set;}=new("","","");public string TripId{get;set;}="";public Trip Snapshot{get;set;}=new();
    public GpsPoint? LastAccepted{get;set;}public OfflineCapabilities? Capabilities{get;set;}
    public OfflineCompletionRequest? Completion{get;set;}public OfflineCompletionReceipt? Receipt{get;set;}
    public OfflineHistoryBatch? Batch{get;set;}public int PendingPoints{get;set;}public int RejectedPoints{get;set;}
    public string ErrorCode{get;set;}="";public bool Blocked{get;set;}public bool SignInRequired{get;set;}
    public int Failures{get;set;}public DateTimeOffset NextAttemptAt{get;set;}
    public long SynchronizedAt{get;set;}
    public bool ShowInSynchronization(DateTimeOffset now)
    {
        if(!Synced)return true;
        // Older saved jobs have no local completion-of-sync time; use their server receipt.
        var finished=SynchronizedAt>0?SynchronizedAt:Receipt?.RecordedAt??0;
        return finished<=0||now.ToUnixTimeMilliseconds()-finished<TimeSpan.FromHours(24).TotalMilliseconds;
    }
    [JsonIgnore]public bool EndedLocally=>Completion is not null;
    [JsonIgnore]public bool Synced=>Receipt is not null&&PendingPoints==0&&!Blocked;
    [JsonIgnore]public string Status=>Blocked||RejectedPoints>0?"Review required":SignInRequired?"Sign in to synchronize":Completion is null?"Location uploads pending":Receipt is null?"Journey ended · awaiting synchronization":PendingPoints>0?"Journey completed · location uploads pending":"Synchronization complete";
}
