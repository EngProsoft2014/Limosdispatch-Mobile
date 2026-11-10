using LimosDispatch.Core.Helpers;
namespace LimosDispatch.Mobile.Services;
// Server inbox and unread count are authoritative across devices.
public sealed class RideNotifications(AuthService auth, PushApi api, RealtimeService realtime)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private bool started;
    private IDispatcherTimer? timer;
    private UserProfile? owner;
    public List<PushNotice> Notices { get; private set; } = [];
    public string? NextCursor { get; private set; }
    public int UnreadCount { get; private set; }
    public bool IsDispatch => auth.User?.CanDispatch == true;
    public event Action? Changed;
    public bool IsUnread(PushNotice notice) => !notice.IsRead;
    public void Clear() { owner = null; Notices = []; NextCursor = null; UnreadCount = 0; Notify(); }
    private void Notify() => MainThread.BeginInvokeOnMainThread(() => Changed?.Invoke());
    public void Start()
    {
        if (!started)
        {
            started = true; auth.SignedOut += Clear; realtime.TripsChanged += RefreshInBackground;
            timer = Application.Current!.Dispatcher.CreateTimer(); timer.Interval = TimeSpan.FromSeconds(30);
            timer.Tick += (_, _) => { if (auth.User is not null && realtime.IsForeground) RefreshInBackground(); }; timer.Start();
        }
        if (owner != auth.User) Clear();
        RefreshInBackground();
    }
    public void RefreshInBackground() => _ = UserFeedback.RunAsync(RefreshAsync);
    public Task RefreshAsync() => Load(false);
    public Task LoadMoreAsync() => Load(true);
    private async Task Load(bool more)
    {
        if (!await gate.WaitAsync(0)) return;
        try
        {
            var user = auth.User; if (user is null) { Clear(); return; }
            if (owner != user) { Clear(); owner = user; }
            var cursor = more ? NextCursor : null; if (more && cursor is null) return;
            var result = await api.InboxAsync(cursor); if (auth.User != user) return;
            var items = result.Items.Where(n => n.Company.Equals(user.Company, StringComparison.OrdinalIgnoreCase));
            Notices = (more ? Notices.Concat(items) : items).DistinctBy(n => n.Id).ToList();
            NextCursor = result.NextCursor; UnreadCount = Math.Max(0, result.UnreadCount); Notify();
        }
        finally { gate.Release(); }
    }
    public async Task MarkReadAsync(string id)
    {
        await gate.WaitAsync();
        try
        {
            var user = auth.User; if (user is null) return;
            var result = await api.ReadAsync(id); if (auth.User != user) return;
            foreach (var n in Notices.Where(n => n.Id == id)) n.IsRead = true;
            UnreadCount = Math.Max(0, result.UnreadCount); Notify();
        }
        finally { gate.Release(); }
    }
    public async Task MarkAllReadAsync()
    {
        await gate.WaitAsync();
        try
        {
            var user = auth.User; if (user is null) return;
            var result = await api.ReadAllAsync(); if (auth.User != user) return;
            foreach (var n in Notices) n.IsRead = true;
            UnreadCount = Math.Max(0, result.UnreadCount); Notify();
        }
        finally { gate.Release(); }
    }
}
