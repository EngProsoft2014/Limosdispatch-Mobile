using System.Collections.ObjectModel;
using System.Globalization;
using LimosDispatch.Core.Helpers;
namespace LimosDispatch.Mobile.ViewModels;
public sealed class TripDetailViewModel : BaseViewModel
{
    private readonly FinanceService finance; private readonly DispatchService dispatch; private readonly AuthService auth; private readonly NavigationService nav; private readonly TrackingService tracking;
    private readonly OfflineSyncService offline;private readonly OfflineSyncCoordinator synchronization;
    private CancellationTokenSource? backgroundReload;
    private Trip trip = new(); private Driver? driver; private string vehicle = "";
    private DriverJourney? journey;
    private string extraType = "Distance", extraQuantity = "", extraDescription = "";
    private bool extraForm;
    private TripExtraRequest? pendingExtra;
    public Trip Trip { get => trip; private set { Set(ref trip, value); Notify(); } }
    public ObservableCollection<Driver> Drivers { get; } = [];
    public Driver? SelectedDriver { get => driver; set { Set(ref driver, value); if (value is not null) Vehicle = value.Vehicle; } }
    public string Vehicle { get => vehicle; set => Set(ref vehicle, value); }
    public bool IsAssignedDriver => auth.User?.IsDriver == true && auth.User.DriverId == Trip.DriverId;
    public bool CanReviewExtras=>auth.User?.Can("Reservations","Edit")==true;
    public bool CanEditTrip=>CanReviewExtras&&!Trip.IsActive&&!IsClosed;
    public bool CanSetPayment=>CanReviewExtras&&!Trip.PaymentMethod.Equals("Credit Card",StringComparison.OrdinalIgnoreCase);
    public System.Windows.Input.ICommand ReviewExtraCommand{get;}
    public AsyncCommand EditCommand{get;}public AsyncCommand CancelCommand{get;}public AsyncCommand PaymentCommand{get;}public AsyncCommand NoShowCommand{get;}public AsyncCommand UnassignCommand{get;}
    public IReadOnlyList<string> PaymentStates{get;}=["Pending","Paid","Refunded"];private string paymentState="Pending";public string PaymentState{get=>paymentState;set=>Set(ref paymentState,value);}
    public bool CanAdvance => IsAssignedDriver && TripWorkflow.Next(Trip.Status) is not null;
    public bool CanExecuteAdvance => CanAdvance && !IsBusy && (Trip.Status != "Confirmed" || journey?.CanStart == true);
    public bool CanAssign => auth.User?.CanAssign == true && !Trip.IsActive && Trip.Status is not ("Completed" or "Cancelled");
    public bool CanTrack => auth.User?.CanDispatch == true && Trip.IsActive;
    public bool CanRequestExtra => IsAssignedDriver && Trip.IsActive && journey?.TrackingAllowed == true;
    public bool ShowTracking => IsAssignedDriver && Trip.IsActive;
    public bool ShowDropoff => Trip.TripType != "Hourly";
    public bool HasPhone => !string.IsNullOrWhiteSpace(Trip.Phone);
    public bool HasNotes => !string.IsNullOrWhiteSpace(Trip.Notes);
    public bool HasEmail => !string.IsNullOrWhiteSpace(Trip.CustomerEmail);
    public bool HasFlight => !string.IsNullOrWhiteSpace(Trip.Flight);
    public string ChildSeatsText => Trip.ChildSeats is { Count: > 0 }
        ? string.Join(" · ", Trip.ChildSeats.Where(x => x.Quantity > 0).Select(x => $"{LocalizationService.Text(x.Type)} × {x.Quantity}"))
        : LocalizationService.Text(string.IsNullOrWhiteSpace(Trip.ChildSeatSummary) ? "Not required" : Trip.ChildSeatSummary);
    public bool HasExtras => Trip.Extras is { Count: > 0 };
    public bool HasHistory => Trip.History is { Count: > 0 };
    public bool IsClosed => Trip.Status is "Completed" or "Cancelled" or "No-show";
    public string NextAction => TripWorkflow.ActionLabel(Trip.Status);
    public string TrackingHealth => tracking.Health;
    private string mapAddress = "";
    private WebViewSource? mapSource;
    public WebViewSource PickupMap
    {
        get
        {
            if (mapSource is null || mapAddress != Trip.From)
            {
                mapAddress = Trip.From;
                var url = System.Net.WebUtility.HtmlEncode("https://maps.google.com/maps?q=" + Uri.EscapeDataString(Trip.From) + "&output=embed");
                mapSource = new HtmlWebViewSource { Html = $"<html><head><meta name='viewport' content='width=device-width,initial-scale=1'></head><body style='margin:0'><iframe title='Pickup location on Google Maps' src='{url}' style='width:100%;height:100%;border:0' referrerpolicy='no-referrer-when-downgrade'></iframe></body></html>", BaseUrl = $"https://{auth.User?.Company ?? "www"}.limosdispatch.com/" };
            }
            return mapSource;
        }
    }
    public string TripTypeSummary => Trip.TripType == "Hourly" ? LocalizationService.Format("Hourly · {0} hours", Trip.Hours.ToString("0.##",LocalizationService.NumericCulture)) : LocalizationService.Text(Trip.TripType);
    public string StartNotice => Trip.Status == "Confirmed" && journey?.CanStart != true
        ? LocalizationService.Format("Available {0} hours before pickup · {1}", (journey?.StartWindowMinutes ?? 120) / 60m,
            journey?.StartAvailableAt is > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(journey.StartAvailableAt.Value).ToLocalTime().ToString("g") : "—") : "";
    public bool HasStartNotice => !string.IsNullOrEmpty(StartNotice);
    public string LicenseNotice
    {
        get
        {
            var l = journey?.License;
            if (l is null || !DateTime.TryParse(l.LimoLicenseExpiry, CultureInfo.InvariantCulture, DateTimeStyles.None, out var expiry) || (expiry.Date-DateTime.Today).TotalDays > 30) return "";
            return LocalizationService.Format("Limo license: {0} · {1}. Please renew and send the updated document to dispatch.", l.LimoLicenseNumber, expiry.ToShortDateString());
        }
    }
    public bool HasLicenseNotice => !string.IsNullOrEmpty(LicenseNotice);
    public IReadOnlyList<string> ExtraTypes { get; } = ["Distance", "Time"];
    public string ExtraType { get => extraType; set { Set(ref extraType, value); Raise(nameof(ExtraQuantityLabel)); } }
    public string ExtraQuantity { get => extraQuantity; set => Set(ref extraQuantity, value); }
    public string ExtraDescription { get => extraDescription; set => Set(ref extraDescription, value); }
    public string ExtraQuantityLabel => LocalizationService.Text(ExtraType == "Time" ? "Total extra time (minutes)" : "Total extra miles");
    public bool ExtraFormVisible { get => extraForm && CanRequestExtra; set => Set(ref extraForm, value); }
    public AsyncCommand AdvanceCommand { get; }
    public AsyncCommand AssignCommand { get; }
    public AsyncCommand TrackCommand { get; }
    public AsyncCommand NavigateCommand { get; }
    public AsyncCommand PickupNavigateCommand { get; }
    public AsyncCommand DropoffNavigateCommand { get; }
    public AsyncCommand OpenMapCommand { get; }
    public AsyncCommand CallCommand { get; }
    public AsyncCommand PassengerSignCommand { get; }
    public AsyncCommand ToggleExtraCommand { get; }
    public AsyncCommand SendExtraCommand { get; }
    public AsyncCommand ResumeTrackingCommand { get; }
    public string LastLocation => tracking.LastLocation;
    public TripDetailViewModel(DispatchService dispatch, AuthService auth, NavigationService nav, TrackingService tracking,FinanceService finance,OfflineSyncService offline,OfflineSyncCoordinator synchronization)
    {
        this.offline=offline;this.synchronization=synchronization;this.finance=finance; this.dispatch = dispatch; this.auth = auth; this.nav = nav; this.tracking = tracking;
        EditCommand=new(()=>Run(()=>nav.EditTripAsync(Trip.Id)));
        CancelCommand=new(()=>Run(async()=>{if(!await nav.ConfirmAsync("Cancel journey",LocalizationService.Text("Cancel this journey? This does not issue a payment refund.")))return;await finance.CancelTripAsync(Trip.Id);await Reload();}));
        PaymentCommand=new(()=>Run(async()=>{if(!CanSetPayment)throw new UnauthorizedAccessException();var records=await finance.RecordsAsync("trips");var row=records.Rows.FirstOrDefault(x=>FinanceService.Id(x)==Trip.Id);var next=PaymentState;if(!PaymentStates.Contains(next))throw new InvalidOperationException("Choose a valid payment state.");if(!await nav.ConfirmAsync("Payment record",LocalizationService.Format("Set the payment record to {0}? No money is collected or refunded.",LocalizationService.Text(next))))return;await finance.UpdateAsync("trips",Trip.Id,row,new(){["paid"]=next},records.Version,"Reservations");await Reload();}));
        UnassignCommand=new(()=>Run(async()=>{if(!CanEditTrip)throw new InvalidOperationException("This journey is locked. Refresh its current status.");var records=await finance.RecordsAsync("trips");var row=records.Rows.FirstOrDefault(x=>FinanceService.Id(x)==Trip.Id);if(!await nav.ConfirmAsync("Unassign chauffeur",LocalizationService.Text("Remove the chauffeur and vehicle assignment from this unstarted journey?")))return;await finance.UpdateAsync("trips",Trip.Id,row,new(){["driverId"]="",["driverName"]="",["driver"]="Unassigned",["vehicle"]="Unassigned",["status"]="Unassigned"},records.Version,"Reservations");await Reload();}));
        NoShowCommand=new(()=>Run(async()=>{if(!CanEditTrip)throw new InvalidOperationException("This journey is locked. Refresh its current status.");var records=await finance.RecordsAsync("trips");var row=records.Rows.FirstOrDefault(x=>FinanceService.Id(x)==Trip.Id);if(!await nav.ConfirmAsync("No-show",LocalizationService.Text("Mark this journey as no-show?")))return;await finance.UpdateAsync("trips",Trip.Id,row,new(){["status"]="No-show"},records.Version,"Reservations");await Reload();}));
        ReviewExtraCommand=new Command<TripExtra>(e=>{if(e is not null&&e.Status=="Pending"&&CanReviewExtras)_=Run(()=>nav.ReviewExtraAsync(Trip.Id,e));});
        AdvanceCommand = new(() => { backgroundReload?.Cancel(); return Run(Advance); }); AssignCommand = new(() => Run(Assign));
        TrackCommand = new(() => Run(() => nav.OpenTrackingAsync(Trip.Id)));
        NavigateCommand = new(() => Run(() => OpenMap(Trip.Status == "Onboard" ? Trip.To : Trip.From, true)));
        PickupNavigateCommand = new(() => Run(() => OpenMap(Trip.From, true)));
        DropoffNavigateCommand = new(() => Run(() => OpenMap(Trip.To, true)));
        OpenMapCommand = new(() => Run(() => OpenMap(Trip.From, false)));
        CallCommand = new(() => Run(() => { PhoneDialer.Default.Open(Trip.Phone); return Task.CompletedTask; }));
        PassengerSignCommand = new(() => Run(async () => await Application.Current!.Windows[0].Page!.Navigation.PushModalAsync(new Pages.PassengerSignPage(Trip.Name))));
        ToggleExtraCommand = new(() => { ExtraFormVisible = !ExtraFormVisible; return Task.CompletedTask; });
        SendExtraCommand = new(() => Run(SendExtra));
        ResumeTrackingCommand = new(() => Run(async () => { await Reload(); if (ShowTracking && journey?.TrackingAllowed == true) await tracking.ResumeAsync(Trip.Id); }));
        RefreshCommand = new(LoadAsync);
        PropertyChanged += (_, e) => { if (e.PropertyName == nameof(IsBusy)) Raise(nameof(CanExecuteAdvance)); };
    }
    private static async Task OpenMap(string address, bool navigate) => await Launcher.Default.OpenAsync(new Uri("https://www.google.com/maps/" + (navigate ? "dir/?api=1&destination=" : "search/?api=1&query=") + Uri.EscapeDataString(address)));
    public void Subscribe() { tracking.HealthChanged += Notify;offline.Changed+=OnSynchronized; }
    public void Unsubscribe() { tracking.HealthChanged -= Notify;offline.Changed-=OnSynchronized; }
    private void OnSynchronized()=>MainThread.BeginInvokeOnMainThread(()=>{if(!string.IsNullOrEmpty(Trip.LocalSyncStatus))_=Run(Reload,showBusy:false);});
    public void SetTrip(Trip value) { journey = null; pendingExtra = null; Trip = value; Vehicle = value.Vehicle; }
    private void Notify() => Raise(string.Empty);
    private Task Reload() => Reload(default);
    private async Task Reload(CancellationToken ct)
    {
        if(await offline.JourneyAsync(Trip.Id) is {EndedLocally:true,Synced:false} local){Trip=OfflineSyncService.Project(local);Notify();return;}
        if (IsAssignedDriver) { var result = await dispatch.DriverJourneyAsync(Trip.Id, ct); ct.ThrowIfCancellationRequested(); journey = result; Trip = result.Trip; }
        else { var list = await dispatch.TripsAsync(ct: ct); ct.ThrowIfCancellationRequested(); Trip = list.Trips.FirstOrDefault(t => t.Id == Trip.Id) ?? throw new InvalidOperationException("Journey no longer exists."); }
        await offline.RememberAsync(Trip);
        if (tracking.ActiveTripId == Trip.Id && (!Trip.IsActive || journey?.TrackingAllowed == false)) await tracking.StopAsync(discard: true);
        if (IsAssignedDriver && Trip.IsActive && journey?.TrackingAllowed == true && !tracking.IsRunning) await tracking.StartAsync(Trip.Id);
        PaymentState=Trip.Paid;Notify();
    }
    public Task RefreshInBackgroundAsync(CancellationToken ct) => Run(async () =>
    {
        if(Connectivity.Current.NetworkAccess!=NetworkAccess.Internet)return;
        using var request=CancellationTokenSource.CreateLinkedTokenSource(ct);backgroundReload=request;
        try{await Reload(request.Token);System.Diagnostics.Debug.WriteLine("[PageRefresh] journey automatic refresh completed");}
        catch(OperationCanceledException) when(request.IsCancellationRequested){}
        finally{if(backgroundReload==request)backgroundReload=null;}
    }, showBusy: false);
    public override Task LoadAsync() => Run(async () =>
    {
        await Reload();
        if (CanAssign) { var list = await dispatch.DriversAsync(); Drivers.Clear(); foreach (var d in list.Where(d => !string.IsNullOrEmpty(d.TrackingId))) Drivers.Add(d); }
    });
    private async Task Advance()
    {
        var expected = Trip.Status;
        if(expected=="Onboard"&&(await offline.CapabilitiesAsync())?.Supported==true)
        {
            if(!await nav.ConfirmAsync("Finish journey",LocalizationService.Text("End this journey on this device? It will synchronize when connected.")))return;
            await tracking.EndLocallyAsync(Trip);await Reload();synchronization.Wake();return;
        }
        await Reload();
        if (Trip.Status != expected) throw new InvalidOperationException("The journey changed. Review its current status before continuing.");
        var next = TripWorkflow.Next(Trip.Status) ?? throw new InvalidOperationException("Refresh this journey.");
        if (next == "Enroute" && journey?.CanStart != true) throw new InvalidOperationException(StartNotice);
        if (!await nav.ConfirmAsync(NextAction, LocalizationService.Format("Update this journey to {0}?", LocalizationService.Text(next)))) return;
        if (next == "Enroute") await tracking.PrepareAsync();
        // Pending GPS upload must not prevent Finish; the server owns the status transition.
        if (next == "Completed") { try { await tracking.FlushAsync(); } catch { } }
        await dispatch.ChangeStatusAsync(Trip, next);
        if (next == "Completed") await tracking.StopAsync(discard: true);
        await Reload();
    }
    private async Task SendExtra()
    {
        if (!InputValidation.TryDecimal(ExtraQuantity,LocalizationService.Culture,out var quantity))
            throw new InvalidOperationException("Enter an extra quantity between 0.01 and 100000.");
        var description = ExtraDescription.Trim();
        // Keep the same request ID after an ambiguous response to prevent duplicate charges/requests.
        if (pendingExtra is null || pendingExtra.Type != ExtraType || pendingExtra.Quantity != quantity || pendingExtra.Description != description)
            pendingExtra = new(Trip.Id, Guid.NewGuid().ToString(), ExtraType, quantity, description);
        await Reload();
        await dispatch.RequestExtraAsync(Trip, pendingExtra);
        pendingExtra = null; ExtraQuantity = ""; ExtraDescription = ""; ExtraFormVisible = false;
        await Reload();
        await FeedbackService.ShowErrorAsync("Extra request sent to dispatch · awaiting approval");
    }
    private async Task Assign()
    {
        if (SelectedDriver is null || string.IsNullOrWhiteSpace(Vehicle)) throw new InvalidOperationException("Select a chauffeur and vehicle.");
        if (!await nav.ConfirmAsync("Assign journey", $"Assign this journey to {SelectedDriver.Name}?")) return;
        await dispatch.AssignAsync(Trip, SelectedDriver, Vehicle); await Reload();
    }
}
