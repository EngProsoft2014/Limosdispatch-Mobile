using System.Security.Cryptography;
using LimosDispatch.Core.Helpers;
namespace LimosDispatch.Mobile.Services;
public sealed class OfflineSyncCoordinator(OfflineSyncService sync,AuthService auth,RealtimeService realtime)
{
    private CancellationTokenSource? session;private IDispatcherTimer? timer;private bool started;private bool refreshing;
    public void Start()
    {
        if(!started)
        {
            started=true;auth.SignedOut+=Stop;realtime.ForegroundChanged+=OnForeground;Connectivity.Current.ConnectivityChanged+=(_,_)=>{if(realtime.IsForeground)Wake();};
            timer=Application.Current!.Dispatcher.CreateTimer();timer.Interval=TimeSpan.FromSeconds(20);timer.Tick+=(_,_)=>{if(realtime.IsForeground)Wake();};timer.Start();
        }
        session?.Cancel();session?.Dispose();session=new();_=RefreshAndWake(session.Token);
    }
    public void Stop(){session?.Cancel();session?.Dispose();session=null;}
    private void OnForeground(bool foreground)
    {
        if(!foreground){session?.Cancel();return;}
        if(sync.Scope is not null){session?.Dispose();session=new();_=RefreshAndWake(session.Token);}
    }
    private async Task RefreshAndWake(CancellationToken ct)
    {
        if(refreshing)return;refreshing=true;
        try{if(Connectivity.Current.NetworkAccess==NetworkAccess.Internet)await sync.CapabilitiesAsync(ct,refresh:true);Wake();}
        catch(OperationCanceledException)when(ct.IsCancellationRequested){}
        catch(Exception ex){await UserFeedback.ReportAsync(ex);}
        finally{refreshing=false;}
    }
    public void Wake()
    {
        var current=session;if(sync.Scope is null||current is null||current.IsCancellationRequested||!realtime.IsForeground||Connectivity.Current.NetworkAccess!=NetworkAccess.Internet)return;
        _=UserFeedback.RunAsync(()=>sync.SynchronizeAsync(current.Token));
    }
    public Task SyncNowAsync()=>sync.SynchronizeAsync(session?.Token??default,manual:true);
}
public static class OfflineStoreKey
{
    public static async Task<byte[]> LoadAsync(string databasePath)
    {
        const string key="offline-sync-aes-v1";var saved=await SecureStorage.Default.GetAsync(key);
        if(saved is not null)return Convert.FromBase64String(saved);
        if(File.Exists(databasePath)&&new FileInfo(databasePath).Length>0)throw new InvalidOperationException("Saved synchronization data needs recovery. Its encryption key is unavailable.");
        var bytes=RandomNumberGenerator.GetBytes(32);await SecureStorage.Default.SetAsync(key,Convert.ToBase64String(bytes));return bytes;
    }
}
