using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LimosDispatch.Core.Models;
using LimosDispatch.Core.Services;
using LimosDispatch.Core.GenericRepository;
using SQLite;
namespace LimosDispatch.Mobile.Services;
// Only opaque scope/trip hashes and timestamps are indexed. Trip names, identities,
// GPS coordinates, requests, receipts and rejected points are AES-GCM protected.
public sealed class SqliteOfflineSyncStore(string path,Func<Task<byte[]>> keyProvider):IOfflineSyncStore
{
    private static readonly Lazy<bool> native=new(()=>{SQLitePCL.Batteries_V2.Init();return true;});
    private readonly SemaphoreSlim gate=new(1,1);private SQLiteConnection? db;private byte[]? key;
    [Table("SyncHeaders")]
    public sealed class HeaderRow{[PrimaryKey]public string Id{get;set;}="";[Indexed]public string Scope{get;set;}="";public byte[] Payload{get;set;}=[];}
    [Table("SyncPoints")]
    public sealed class PointRow{[PrimaryKey]public string Id{get;set;}="";[Indexed]public string Journey{get;set;}="";public long CapturedAt{get;set;}public bool Rejected{get;set;}public byte[] Payload{get;set;}=[];}
    private static string Hash(string s)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)));
    private static string Id(OfflineScope s,string trip)=>Hash(s.Key+"|"+trip);
    private byte[] Encrypt<T>(string id,T value)
    {
        var clear=JsonSerializer.SerializeToUtf8Bytes(value,ApiRepository.Json);var result=new byte[28+clear.Length];RandomNumberGenerator.Fill(result.AsSpan(0,12));
        using var aes=new AesGcm(key!,16);aes.Encrypt(result.AsSpan(0,12),clear,result.AsSpan(28),result.AsSpan(12,16),Encoding.UTF8.GetBytes(id));return result;
    }
    private T Decrypt<T>(string id,byte[] payload)
    {
        if(payload.Length<28)throw new InvalidDataException("Saved synchronization data could not be read.");
        var clear=new byte[payload.Length-28];using var aes=new AesGcm(key!,16);aes.Decrypt(payload.AsSpan(0,12),payload.AsSpan(28),payload.AsSpan(12,16),clear,Encoding.UTF8.GetBytes(id));
        return JsonSerializer.Deserialize<T>(clear,ApiRepository.Json)??throw new InvalidDataException("Saved synchronization data is empty.");
    }
    private async Task<T> Execute<T>(Func<T> action)
    {
        await gate.WaitAsync();
        try
        {
            key??=await keyProvider();
            return await Task.Run(()=>
            {
                if(db is null){_=native.Value;db=new SQLiteConnection(path,SQLiteOpenFlags.ReadWrite|SQLiteOpenFlags.Create|SQLiteOpenFlags.FullMutex);db.ExecuteScalar<string>("PRAGMA journal_mode=WAL");db.Execute("PRAGMA synchronous=FULL");db.CreateTable<HeaderRow>();db.CreateTable<PointRow>();db.Execute("CREATE INDEX IF NOT EXISTS IX_SyncPointPending ON SyncPoints(Journey,Rejected,CapturedAt)");}
                return action();
            });
        }
        finally{gate.Release();}
    }
    private OfflineJourney Load(OfflineScope scope,string trip)
    {
        var id=Id(scope,trip);var row=db!.Find<HeaderRow>(id);
        return row is null?new(){Scope=scope,TripId=trip,Snapshot=new(){Id=trip,DriverId=scope.DriverId}}:Decrypt<OfflineJourney>(id,row.Payload);
    }
    private void Save(OfflineJourney j){if(j.Synced&&j.SynchronizedAt==0)j.SynchronizedAt=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();var id=Id(j.Scope,j.TripId);db!.InsertOrReplace(new HeaderRow{Id=id,Scope=Hash(j.Scope.Key),Payload=Encrypt(id,j)});}
    public Task<OfflineJourney?> GetAsync(OfflineScope s,string trip)=>Execute(()=>db!.Find<HeaderRow>(Id(s,trip)) is {} row?Decrypt<OfflineJourney>(row.Id,row.Payload):null);
    public Task<List<OfflineJourney>> ListAsync(OfflineScope s)=>Execute(()=>db!.Query<HeaderRow>("SELECT * FROM SyncHeaders WHERE Scope=?",Hash(s.Key)).Where(r=>!r.Id.StartsWith("caps:")).Select(r=>Decrypt<OfflineJourney>(r.Id,r.Payload)).ToList());
    public async Task UpdateAsync(OfflineScope s,string trip,Action<OfflineJourney> change)=>await Execute(()=>{db!.RunInTransaction(()=>{var j=Load(s,trip);change(j);Save(j);});return true;});
    public async Task CaptureAsync(OfflineScope s,string trip,GpsPoint point)=>await Execute(()=>
    {
        db!.RunInTransaction(()=>
        {
            var j=Load(s,trip);if(j.EndedLocally)throw new InvalidOperationException("This journey has already ended on this device.");
            var id=Id(s,trip)+":"+point.CapturedAt;if(db.Find<PointRow>(id) is not null)return;
            if(j.PendingPoints>=150000||db.ExecuteScalar<int>("SELECT COUNT(*) FROM SyncPoints")>=300000)throw new InvalidOperationException("Synchronization storage is full. Connect and synchronize before continuing.");
            db.Insert(new PointRow{Id=id,Journey=Id(s,trip),CapturedAt=point.CapturedAt,Payload=Encrypt(id,point)});j.PendingPoints++;if(j.LastAccepted is null||point.CapturedAt>j.LastAccepted.CapturedAt)j.LastAccepted=point;Save(j);
        });return true;
    });
    public Task<List<GpsPoint>> PointsAsync(OfflineScope s,string trip,int limit,long capturedAfter=0)=>Execute(()=>db!.Query<PointRow>("SELECT * FROM SyncPoints WHERE Journey=? AND Rejected=0 AND CapturedAt>=? ORDER BY CapturedAt LIMIT ?",Id(s,trip),capturedAfter,limit).Select(p=>Decrypt<GpsPoint>(p.Id,p.Payload)).ToList());
    public async Task AcknowledgeAsync(OfflineScope s,string trip,IReadOnlyCollection<long> times)=>await Execute(()=>
    {
        db!.RunInTransaction(()=>{var j=Load(s,trip);foreach(var time in times){var id=Id(s,trip)+":"+time;var row=db.Find<PointRow>(id);if(row is null)continue;db.Delete<PointRow>(id);j.PendingPoints--;if(row.Rejected)j.RejectedPoints--;}Save(j);});return true;
    });
    public async Task ApplyBatchAsync(OfflineScope s,string trip,OfflineHistoryBatch batch,OfflineHistoryReceipt receipt)=>await Execute(()=>
    {
        db!.RunInTransaction(()=>
        {
            var j=Load(s,trip);if(j.Batch?.BatchId!=batch.BatchId)throw new InvalidDataException("The saved batch changed before its acknowledgement.");
            foreach(var time in receipt.AcknowledgedCapturedAt){var id=Id(s,trip)+":"+time;var row=db.Find<PointRow>(id);if(row is null)continue;db.Delete<PointRow>(id);j.PendingPoints--;if(row.Rejected)j.RejectedPoints--;}
            foreach(var reject in receipt.RejectedPoints.Where(p=>!p.Retryable))
            {
                var id=Id(s,trip)+":"+reject.CapturedAt;var row=db.Find<PointRow>(id);if(row is null||row.Rejected)continue;
                var original=Decrypt<GpsPoint>(id,row.Payload);row.Payload=Encrypt(id,new{point=original,rejection=reject});row.Rejected=true;db.Update(row);j.RejectedPoints++;
            }
            j.Batch=null;j.SignInRequired=false;j.Failures=0;j.NextAttemptAt=receipt.RejectedPoints.Any(p=>p.Retryable)?DateTimeOffset.UtcNow.AddSeconds(30):default;
            j.ErrorCode=j.RejectedPoints>0?"POINTS_REVIEW_REQUIRED":"";Save(j);
        });return true;
    });
    public Task<OfflineCapabilities?> CapabilitiesAsync(OfflineScope s)=>Execute(()=>db!.Find<HeaderRow>("caps:"+Hash(s.Key)) is {} row?Decrypt<OfflineCapabilities>(row.Id,row.Payload):null);
    public async Task SaveCapabilitiesAsync(OfflineScope s,OfflineCapabilities value)=>await Execute(()=>{var id="caps:"+Hash(s.Key);db!.InsertOrReplace(new HeaderRow{Id=id,Scope=Hash(s.Key),Payload=Encrypt(id,value)});return true;});
}
