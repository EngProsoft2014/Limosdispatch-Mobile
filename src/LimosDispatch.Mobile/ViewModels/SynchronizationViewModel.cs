using System.Collections.ObjectModel;
namespace LimosDispatch.Mobile.ViewModels;
public sealed record SynchronizationRow(OfflineJourney Journey)
{
    public string Title=>string.IsNullOrWhiteSpace(Journey.Snapshot.Name)?Journey.TripId:Journey.Snapshot.Name;
    public string Status=>LocalizationService.Text(Journey.Status);
    public string Counts=>LocalizationService.Format("{0} locations pending · {1} need review",Journey.PendingPoints,Journey.RejectedPoints);
    public string EndedAt=>Journey.Completion is {} c?LocalizationService.Format("Ended on this device: {0}",DateTimeOffset.FromUnixTimeMilliseconds(c.CompletedAt).ToLocalTime().ToString("g")):"";
    public string Reason=>LocalizationService.Text(Journey.ErrorCode switch
    {
        "TRIP_CANCELLED"=>"The journey was cancelled by dispatch.",
        "ASSIGNMENT_CHANGED"=>"The chauffeur assignment changed. Contact dispatch.",
        "ASSIGNMENT_REVIEW_REQUIRED"=>"This journey needs assignment review. Contact dispatch.",
        "COMPLETION_TIME_REVIEW_REQUIRED" or "INVALID_COMPLETION_TIME"=>"The completion time needs review. Contact dispatch.",
        "SYNC_WINDOW_EXPIRED"=>"The synchronization window has expired. Saved data has been retained.",
        "SIGN_IN_REQUIRED"=>"Sign in to the original account to synchronize.",
        "TRIP_STORAGE_LIMIT" or "POINTS_REVIEW_REQUIRED"=>"Some saved locations need review. Contact dispatch.",
        "TRIP_CHANGED" or "TRIP_ALREADY_COMPLETED" or "INVALID_TRIP_STATE"=>"The journey changed on the server. Contact dispatch.",
        "INVALID_COMPLETION_RECEIPT" or "IDEMPOTENCY_KEY_REUSED"=>"The saved synchronization request needs review. Contact dispatch.",
        "SYNC_RETRY"=>"Waiting for a connection or the next synchronization attempt.",
        ""=>"",_=>"Saved data needs review. Contact dispatch."
    });
    public bool HasReason=>!string.IsNullOrEmpty(Reason);
}
public sealed class SynchronizationViewModel(OfflineSyncService sync,OfflineSyncCoordinator coordinator):BaseViewModel
{
    public ObservableCollection<SynchronizationRow> Rows{get;}=[];
    private AsyncCommand? synchronizeCommand;
    public string Summary=>LocalizationService.Format("{0} journeys · {1} locations pending",Rows.Count,Rows.Sum(r=>r.Journey.PendingPoints));
    public bool Empty=>Rows.Count==0;
    public AsyncCommand SynchronizeCommand=>synchronizeCommand??=new(()=>Run(async()=>
    {
        if(Connectivity.Current.NetworkAccess!=NetworkAccess.Internet)throw new InvalidOperationException("Connect to the internet to synchronize saved journeys.");
        await coordinator.SyncNowAsync();await RefreshRows();
    }));
    public void Subscribe()=>sync.Changed+=OnChanged;
    public void Unsubscribe()=>sync.Changed-=OnChanged;
    private void OnChanged()=>MainThread.BeginInvokeOnMainThread(()=>_=Run(RefreshRows,showBusy:false));
    private async Task RefreshRows()
    {
        var jobs=await sync.JourneysAsync();var now=DateTimeOffset.UtcNow;Rows.Clear();
        foreach(var job in jobs.Where(j=>(j.EndedLocally||j.PendingPoints>0)&&j.ShowInSynchronization(now)).OrderBy(j=>j.Synced).ThenByDescending(j=>j.Completion?.CompletedAt??0))Rows.Add(new(job));
        Raise(nameof(Summary));Raise(nameof(Empty));
    }
    public override Task LoadAsync()=>Run(RefreshRows);
    public Task RefreshInBackgroundAsync()=>Run(RefreshRows,showBusy:false);
}
