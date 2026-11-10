using System.Collections.ObjectModel;
using System.Windows.Input;
using LimosDispatch.Mobile.Pages;
namespace LimosDispatch.Mobile.ViewModels;
public sealed class RidesViewModel : BaseViewModel
{
    private readonly DispatchService dispatch;
    private readonly AuthService auth;
    private readonly NavigationService nav;
    private readonly TrackingService tracking;private readonly OfflineSyncService offline;
    private readonly RealtimeService realtime;
    private List<Trip> all = [];
    private string search = "", filter = "Upcoming";
    private bool personal;
    public ObservableCollection<Trip> Trips { get; } = [];
    public bool IsUpcoming => Filter == "Upcoming";
    public bool IsPast => Filter == "Past";
    public ICommand UpcomingCommand { get; }
    public ICommand PastCommand { get; }
    public string Search { get => search; set { Set(ref search, value); Apply(); } }
    public string Filter { get => filter; set { if (Set(ref filter, value)) { Raise(nameof(IsUpcoming)); Raise(nameof(IsPast)); Apply(); } } }
    public bool IsDispatcher => auth.User?.CanDispatch == true;
    public bool CanCreate => auth.User?.CanCreateTrip == true;
    public bool CanSwitchMode => IsDispatcher && auth.User?.IsDriver == true;
    public bool Personal { get => personal; set { if (Set(ref personal, value)) _ = LoadAsync(); } }
    public string Heading => IsDispatcher ? "Dispatch overview" : "My rides";
    public string Name => auth.User?.Name ?? "";
    public string Summary => $"{all.Count(t => t.IsActive)} active · {all.Count(t => t.Status == "Unassigned")} unassigned";
    public ICommand OpenTripCommand { get; }
    public AsyncCommand NewTripCommand { get; }
    public AsyncCommand AccountCommand { get; }
    public RidesViewModel(DispatchService dispatch, AuthService auth, NavigationService nav, TrackingService tracking, RealtimeService realtime,OfflineSyncService offline)
    {
        this.offline=offline;this.dispatch = dispatch; this.auth = auth; this.nav = nav; this.tracking = tracking; this.realtime = realtime;
        UpcomingCommand = new Command(() => Filter = "Upcoming"); PastCommand = new Command(() => Filter = "Past");
        RefreshCommand = new(LoadAsync); NewTripCommand = new(() => Run(() => nav.PushAsync<CreateRidePage>()));
        AccountCommand = new(() => Run(() => nav.PushAsync<MorePage>()));
        OpenTripCommand = new Command<Trip>(t => { if (t is not null) _ = Run(() => nav.OpenTripAsync(t)); });
    }
    public void Subscribe() => realtime.TripsChanged += OnChanged;
    public void Unsubscribe() => realtime.TripsChanged -= OnChanged;
    private void OnChanged() => MainThread.BeginInvokeOnMainThread(async () => await LoadAsync());
    public override Task LoadAsync() => Run(async () =>
    {
        await offline.OverlayAsync(all);Apply();
        var response = await dispatch.TripsAsync(Personal); all = response.Trips;await offline.OverlayAsync(all);
        Apply(); Raise(nameof(Summary));
        // Resume location only for this user's active assigned trip after app restart.
        var active = all.FirstOrDefault(t => t.IsActive && t.DriverId == auth.User?.DriverId);
        if (active is not null && !tracking.IsRunning && !await offline.EndedAsync(active.Id)) await tracking.StartAsync(active.Id);
    });
    private void Apply()
    {
        var now = DateTimeOffset.UtcNow;
        var list = all.Where(t => LimosDispatch.Core.Helpers.ScreenRules.IsPast(t, now) == IsPast)
            .Where(t => string.IsNullOrWhiteSpace(Search) || $"{t.Name} {t.Id} {t.From} {t.To} {t.DriverName}".Contains(Search, StringComparison.OrdinalIgnoreCase))
            .OrderBy(t => IsPast ? -t.PickupAt : t.PickupAt == 0 ? long.MaxValue : t.PickupAt);
        var desired = list.ToList();
        var ids = desired.Select(t => t.Id).ToHashSet();
        for (var i = Trips.Count - 1; i >= 0; i--) if (!ids.Contains(Trips[i].Id)) Trips.RemoveAt(i);
        for (var i = 0; i < desired.Count; i++)
        {
            var value = desired[i];
            var existing = Trips.Select((t, index) => (t, index)).FirstOrDefault(x => x.t.Id == value.Id);
            if (existing.t is null) Trips.Insert(i, value);
            else
            {
                if (existing.index != i) Trips.Move(existing.index, i);
                var old = Trips[i];
                if ((old.Status, old.Name, old.Schedule, old.Route, old.DriverName, old.Vehicle, old.People, old.Service, old.TripType) !=
                    (value.Status, value.Name, value.Schedule, value.Route, value.DriverName, value.Vehicle, value.People, value.Service, value.TripType)) Trips[i] = value;
            }
        }
    }
}
