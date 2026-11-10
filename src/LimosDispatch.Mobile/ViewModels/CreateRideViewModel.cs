using System.Collections.ObjectModel;
using System.Text.Json;
using LimosDispatch.Core.GenericRepository;
using LimosDispatch.Core.Helpers;
namespace LimosDispatch.Mobile.ViewModels;
public sealed class CreateRideViewModel : BaseViewModel
{
    private readonly FinanceService finance;private readonly DispatchService dispatch; private readonly NavigationService nav; private readonly IGenericRepository api;
    private Trip? editing; private JsonElement original; private string editId="", pricingIdentity=""; private bool initializing;
    public string PageTitle=>LocalizationService.Text(editing is null?"New journey":"Edit journey");
    public void ConfigureEdit(string id)=>editId=id;
    private string PricingIdentity()=>$"{passenger?.CustomerId}|{Pickup}|{Dropoff}|{TripType}|{VehicleType}|{Hours}|{CouponCode}|{PaymentMethod}";
    private bool KeepPrice=>editing is not null&&PricingIdentity()==pricingIdentity;
    private readonly string draftId = "LD-" + Guid.NewGuid();
    private long version; private string name = "", phone = "", email = "", pickup = "", dropoff = "", notes = "", flight = "", fare = "", miles = "", hours = "", people = "1", timezone = "", type = "Distance", vehicleType = "", service = "";
    private DateTime date = DateTime.Today; private TimeSpan time = DateTime.Now.TimeOfDay; private bool saved;
    private readonly GoogleAddresses google;
    private readonly AuthService auth;
    private TierPricing pricing = new();
    private TierFare? calculatedFare;
    private Trip? created;
    private Driver? selectedDriver;
    private FleetVehicle? selectedVehicle;
    private Passenger? passenger;
    private readonly BookingQuoteService quotes;
    private string couponCode = "", boosterSeats = "0", infantSeats = "0", paymentMethod = "Cash", paymentStatus = "Pending";
    private string phoneError = "", emailError = "";
    private bool validatingContact;
    public string PhoneError { get => phoneError; private set => Set(ref phoneError, value); }
    public string EmailError { get => emailError; private set => Set(ref emailError, value); }
    public string CouponCode { get => couponCode; set { if (Set(ref couponCode, value)) _ = PreviewQuote(); } }
    public string BoosterSeats { get => boosterSeats; set => Set(ref boosterSeats, value); }
    public string InfantSeats { get => infantSeats; set => Set(ref infantSeats, value); }
    public IReadOnlyList<string> PaymentMethods { get; } = ["Zelle", "Cash", "Check", "Credit Card"];
    public IReadOnlyList<string> PaymentStates { get; } = ["Pending", "Paid"];
    public bool CanChangePaymentStatus=>editing is null&&!IsCreditCard;
    public bool IsCreditCard => PaymentMethod == "Credit Card";
    public string PaymentMethod { get => paymentMethod; set { if (Set(ref paymentMethod, value)) { if (IsCreditCard) PaymentStatus = "Pending"; Raise(nameof(IsCreditCard));Raise(nameof(CanChangePaymentStatus)); _ = PreviewQuote(); } } }
    public string PaymentStatus { get => paymentStatus; set => Set(ref paymentStatus, IsCreditCard ? "Pending" : value); }
    public AsyncCommand CheckCouponCommand { get; }
    private string Money(decimal value) => $"{value:0.00} {CompanySettingsService.Currency}".Trim();
    public string CustomerDiscountDisplay => quote is null ? "—" : Money(quote.CustomerDiscount);
    public string CouponDiscountDisplay => quote is null ? "—" : Money(quote.CouponDiscount);
    public string AfterDiscountDisplay => quote is null ? "—" : Money(quote.TotalAfterDiscount);
    public string CardFeesDisplay => quote is null ? "—" : Money(quote.CreditCardFee);
    private void QuoteChanged() { foreach (var property in new[] { nameof(TotalDisplay), nameof(CustomerDiscountDisplay), nameof(CouponDiscountDisplay), nameof(AfterDiscountDisplay), nameof(CardFeesDisplay) }) Raise(property); }
    private ReservationQuote? quote;
    private CancellationTokenSource? quoteCancellation;
    private string quoteStatus = "Select a passenger and complete the route or hours to see the final total.";
    public string QuoteStatus { get => quoteStatus; private set => Set(ref quoteStatus, value); }
    public string TotalDisplay => quote is null ? "—" : $"{quote.Price:0.00} {CompanySettingsService.Currency}".Trim();
    public string PassengerDisplay => passenger?.Name ?? LocalizationService.Text("Search passenger by name or phone");
    public AsyncCommand PassengerCommand { get; }
    private CancellationTokenSource? routeCancellation;
    private string routeIdentity = "", routeState = "Choose pickup and drop-off to calculate the road distance.";
    private decimal routeMeters;
    private bool routeBusy, loaded;
    public bool IsDistance => TripType == "Distance";
    public bool IsHourly => TripType == "Hourly";
    public bool CanAssign => auth.User?.CanAssign == true;
    public bool CanEdit => created is null;
    public bool IsRouteBusy { get => routeBusy; private set => Set(ref routeBusy, value); }
    public string RouteState { get => routeState; private set => Set(ref routeState, value); }
    public string SaveLabel => created is not null ? "Retry assignment" : editing is not null ? "Save changes" : "Create journey";
    public ObservableCollection<Driver> Drivers { get; } = [];
    public ObservableCollection<FleetVehicle> Vehicles { get; } = [];
    public ObservableCollection<string> VehicleTypes { get; } = [];
    public Driver? SelectedDriver { get => selectedDriver; set { Set(ref selectedDriver, value); if (value is not null) SelectedVehicle = Vehicles.FirstOrDefault(v => v.DisplayName == value.Vehicle || v.Name == value.Vehicle || !string.IsNullOrWhiteSpace(v.Plate) && value.Vehicle.Contains(v.Plate)); } }
    public FleetVehicle? SelectedVehicle { get => selectedVehicle; set { Set(ref selectedVehicle, value); if (value is not null && created is null) VehicleType = value.Category; } }
    public AsyncCommand PickupCommand { get; }
    public AsyncCommand DropoffCommand { get; }
    public AsyncCommand RouteCommand { get; }
    public string Name { get => name; set => Set(ref name, value); }
    public string Phone { get => phone; set { if (Set(ref phone, value) && validatingContact) PhoneError = InputValidation.PhoneError(Phone); } }
    public string Email { get => email; set { if (Set(ref email, value) && validatingContact) EmailError = InputValidation.EmailError(Email); } }
    public string PickupDisplay => string.IsNullOrWhiteSpace(Pickup) ? "Search pickup address" : Pickup;
    public string DropoffDisplay => string.IsNullOrWhiteSpace(Dropoff) ? "Search drop-off address" : Dropoff;
    public string Pickup { get => pickup; set { if (Set(ref pickup, value)) { Raise(nameof(PickupDisplay)); _ = UpdateRoute(); } } }
    public string Dropoff { get => dropoff; set { if (Set(ref dropoff, value)) { Raise(nameof(DropoffDisplay)); _ = UpdateRoute(); } } }
    public string Notes { get => notes; set => Set(ref notes, value); }
    public string Flight { get => flight; set => Set(ref flight, value); }
    public string FareDisplay => string.IsNullOrWhiteSpace(Fare) ? "—" : $"{Fare} {CompanySettingsService.Currency}".Trim();
    public string Fare { get => fare; set { if (Set(ref fare, value)) Raise(nameof(FareDisplay)); } }
    public string Miles { get => miles; set => Set(ref miles, value); }
    public string Hours { get => hours; set { if (Set(ref hours, value)) UpdateFare(); } }
    public string People { get => people; set => Set(ref people, value); }
    public string Timezone { get => timezone; set => Set(ref timezone, value); }
    public string TripType { get => type; set { if (Set(ref type, value)) { Raise(nameof(IsDistance)); Raise(nameof(IsHourly)); _ = UpdateRoute(); UpdateFare(); } } }
    public string VehicleType { get => vehicleType; set { if (Set(ref vehicleType, value)) UpdateFare(); } }
    public string Service { get => service; set => Set(ref service, value); }
    public DateTime Date { get => date; set => Set(ref date, value); }
    public TimeSpan Time { get => time; set => Set(ref time, value); }
    public IReadOnlyList<string> Types { get; } = ["Distance", "Hourly"];
    public AsyncCommand SaveCommand { get; }
    public CreateRideViewModel(DispatchService dispatch, NavigationService nav, IGenericRepository api, GoogleAddresses google, AuthService auth, BookingQuoteService quotes,FinanceService finance)
    {
        this.finance=finance;this.dispatch = dispatch; this.nav = nav; this.api = api; this.google = google; this.auth = auth; this.quotes = quotes;
        CheckCouponCommand = new(() => Run(async () => { if (passenger is null || calculatedFare is null) throw new InvalidOperationException("Select a passenger and complete pricing before checking a coupon."); await PreviewQuote(); }));
        RefreshCommand = new(LoadAsync); SaveCommand = new(() => Run(Save));
        PassengerCommand = new(() => Run(async () =>
        {
            var selected = await nav.SelectPassengerAsync();
            if (selected is null) return;
            passenger = selected; Name = selected.Name; Phone = selected.Phone; Email = selected.Email; Raise(nameof(PassengerDisplay));
            // Adding a passenger advances the shared workspace version.
            version = (await dispatch.TripRecordsAsync()).Version;
            _ = PreviewQuote();
        }));
        PickupCommand = new(async () => { var result = await nav.SelectAddressAsync("Pickup address"); if (result is not null) Pickup = result.Address; });
        DropoffCommand = new(async () => { var result = await nav.SelectAddressAsync("Drop-off address"); if (result is not null) Dropoff = result.Address; });
        RouteCommand = new(UpdateRoute);
    }
    public override Task LoadAsync() => Run(async () =>
    {
        if (loaded) return;
        if(editId.Length>0){var records=await api.GetAsync<Records<JsonElement>>("api/records/trips");original=records.Rows.FirstOrDefault(x=>FinanceService.Id(x)==editId);if(original.ValueKind!=JsonValueKind.Object)throw new InvalidOperationException("Journey no longer exists.");editing=original.Deserialize<Trip>(ApiRepository.Json)!;if(editing.IsActive||editing.Status is "Completed" or "Cancelled" or "No-show")throw new InvalidOperationException("This journey is locked. Refresh its current status.");version=records.Version;}
        if(editing is null)version = (await dispatch.TripRecordsAsync()).Version;
        var settings = await api.GetAsync<JsonElement>("api/data/settings");
        if (settings.TryGetProperty("timezone", out var zone)) Timezone = zone.GetString() ?? "";
        pricing = await api.GetAsync<TierPricing>("api/data/tierPricing");
        VehicleTypes.Clear(); foreach (var item in pricing.Types) VehicleTypes.Add(item);
        if (CanAssign)
        {
            var fleet = await api.GetAsync<List<FleetVehicle>>("api/data/fleet");
            Vehicles.Clear(); foreach (var v in fleet.Where(v => v.Status == "Active")) Vehicles.Add(v);
            Drivers.Clear(); foreach (var d in (await dispatch.DriversAsync()).Where(d => d.Status == "Active" && !string.IsNullOrWhiteSpace(d.TrackingId))) Drivers.Add(d);
        }
        if(editing is not null){initializing=true;var t=editing;passenger=new Passenger{CustomerId=t.CustomerId,Name=t.Name,Phone=t.Phone,Email=t.CustomerEmail};name=t.Name;phone=t.Phone;email=t.CustomerEmail;pickup=t.From;dropoff=t.HasDropoff?t.To:"";notes=t.Notes;flight=t.Flight;date=DateTime.Parse(t.Date,System.Globalization.CultureInfo.InvariantCulture);time=TimeSpan.Parse(t.Time,System.Globalization.CultureInfo.InvariantCulture);timezone=t.Timezone;type=t.TripType;vehicleType=t.VehicleType;hours=t.Hours.ToString(System.Globalization.CultureInfo.InvariantCulture);people=t.People.ToString(System.Globalization.CultureInfo.InvariantCulture);couponCode=t.CouponCode;paymentMethod=t.PaymentMethod;paymentStatus=t.Paid;boosterSeats=t.ChildSeats.FirstOrDefault(x=>x.Type=="Booster seat")?.Quantity.ToString()??"0";infantSeats=t.ChildSeats.FirstOrDefault(x=>x.Type=="Infant seat")?.Quantity.ToString()??"0";selectedDriver=Drivers.FirstOrDefault(d=>d.TrackingId==t.DriverId||original.TryGetProperty("driver",out var dn)&&dn.GetString()==d.Name);selectedVehicle=Vehicles.FirstOrDefault(v=>v.DisplayName==t.Vehicle);routeMeters=t.GoogleDistanceMeters>0?t.GoogleDistanceMeters:t.DistanceMiles*1609.344m;routeIdentity=Pickup+"|"+Dropoff;miles=t.DistanceMiles.ToString(System.Globalization.CultureInfo.InvariantCulture);fare=t.FareBeforeDiscount.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture);pricingIdentity=PricingIdentity();quote=new ReservationQuote{Price=t.Price,FareBeforeDiscount=t.FareBeforeDiscount,CustomerDiscount=t.CustomerDiscount,CouponDiscount=t.CouponDiscount,TotalAfterDiscount=t.TotalAfterDiscount,CreditCardFee=t.CreditCardFee,CreditCardFeeRate=t.CreditCardFeeRate,CreditCardFeeFixed=t.CreditCardFeeFixed,CouponCode=t.CouponCode,CouponId=t.CouponId};quoteStatus="Existing price is retained unless the passenger, route, hours, vehicle type, coupon or payment method changes.";initializing=false;QuoteChanged();Raise(string.Empty);}
        loaded = true; if(editing is null)UpdateFare();
    });
    private void UpdateFare()
    {
        if(initializing)return;
        calculatedFare = null; Fare = "";
        try
        {
            var quantity = IsHourly ? Number(Hours, "booked hours") : routeMeters / 1609.344m;
            calculatedFare = pricing.Quote(TripType, VehicleType, quantity);
            Fare = calculatedFare.Total.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (InvalidOperationException) { }
        _ = PreviewQuote();
    }
    private async Task PreviewQuote()
    {
        if(initializing)return;
        quoteCancellation?.Cancel(); quoteCancellation?.Dispose(); quoteCancellation = new(); var ct = quoteCancellation.Token;
        quote = null; QuoteChanged();
        if (passenger is null || calculatedFare is null) { QuoteStatus = "Select a passenger and complete the route or hours to see the final total."; return; }
        var customer = passenger.CustomerId; var fare = calculatedFare.Total; QuoteStatus = "Checking the customer’s price…";
        try
        {
            await Task.Delay(300, ct);
            var result = await quotes.QuoteAsync(fare, customer, ct, CouponCode, PaymentMethod, editing?.Id);
            if (ct.IsCancellationRequested) return;
            quote = result; QuoteChanged();
            QuoteStatus = string.IsNullOrWhiteSpace(result.Notice) ? result.CouponDiscount > 0 ? "Coupon applied. Review the final total below." : "Price checked with the company server." : result.Notice;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex) { if (!ct.IsCancellationRequested) { QuoteStatus = UserFeedback.Message(ex); await UserFeedback.ReportAsync(ex); } }
    }
    private async Task UpdateRoute()
    {
        if(initializing)return;
        routeCancellation?.Cancel(); routeCancellation?.Dispose(); routeCancellation = new();
        var ct = routeCancellation.Token;
        routeIdentity = ""; routeMeters = 0; Miles = ""; IsRouteBusy = false; UpdateFare();
        if (!IsDistance) { RouteState = "Hourly journey · as directed. No drop-off or road distance required."; return; }
        if (string.IsNullOrWhiteSpace(Pickup) || string.IsNullOrWhiteSpace(Dropoff)) { RouteState = "Choose pickup and drop-off to calculate the road distance."; return; }
        var identity = Pickup + "|" + Dropoff;
        IsRouteBusy = true; RouteState = "Calculating Google driving distance…";
        try
        {
            var route = await google.RouteAsync(Pickup, Dropoff, ct);
            if (ct.IsCancellationRequested || !IsDistance || identity != Pickup + "|" + Dropoff) return;
            routeIdentity = identity; routeMeters = (decimal)route.Meters;
            Miles = route.Miles.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            RouteState = LocalizationService.Format("{0} road miles · calculated by Google Maps", Miles); UpdateFare();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex) { if (!ct.IsCancellationRequested) { RouteState = UserFeedback.Message(ex); await UserFeedback.ReportAsync(ex); } }
        finally { if (!ct.IsCancellationRequested) IsRouteBusy = false; }
    }
    private static decimal Number(string text, string label, bool optional = false)
    {
        if (optional && string.IsNullOrWhiteSpace(text)) return 0;
        if (!InputValidation.TryDecimal(text,LocalizationService.Culture,out var n) || n < 0) throw new InvalidOperationException($"Enter a valid {label}.");
        return n;
    }
    private async Task Save()
    {
        if (saved) return;
        if (created is not null) { await FinishAssignment(); return; }
        // Display-only card price preview: no card intent, collection or confirmation is initiated.
        if ((IsCreditCard || editing?.PaymentMethod == "Credit Card") && !KeepPrice) throw new InvalidOperationException("Card collection is not enabled in this mobile build. Complete card checkout on the web or select another payment method.");
        validatingContact = true; PhoneError = InputValidation.PhoneError(Phone); EmailError = InputValidation.EmailError(Email);
        if (PhoneError.Length + EmailError.Length > 0) return;
        if (passenger is null || string.IsNullOrWhiteSpace(passenger.CustomerId)) throw new InvalidOperationException("Select or save a passenger before creating the journey.");
        if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Pickup) || IsDistance && string.IsNullOrWhiteSpace(Dropoff) || string.IsNullOrWhiteSpace(Phone)) throw new InvalidOperationException("Enter passenger, phone and the required addresses.");
        if (!KeepPrice && IsDistance && (IsRouteBusy || routeIdentity != Pickup + "|" + Dropoff || routeMeters <= 0)) throw new InvalidOperationException("Calculate a valid Google driving route before saving.");
        if(!KeepPrice)UpdateFare();
        if (!KeepPrice && calculatedFare is null) throw new InvalidOperationException("Choose a vehicle type and a distance or duration covered by company pricing.");
        if (SelectedDriver is not null && SelectedVehicle is null) throw new InvalidOperationException("Select an active vehicle for the assigned chauffeur.");
        if (SelectedDriver is not null && SelectedVehicle is not null)
        {
            var others = (await dispatch.TripsAsync()).Trips.Where(t => t.Id != editing?.Id && t.Status is not ("Cancelled" or "Completed" or "No-show") && t.Date == Date.ToString("yyyy-MM-dd") && t.Time == Time.ToString(@"hh\:mm"));
            if (others.Any(t => t.DriverId == SelectedDriver.TrackingId || t.Vehicle == SelectedVehicle.DisplayName))
                throw new InvalidOperationException("Scheduling conflict: this chauffeur or vehicle is already booked at that time.");
        }
        var passengers = Number(People, "passenger count");
        if (passengers < 1 || passengers != decimal.Truncate(passengers)) throw new InvalidOperationException("Passenger count must be a positive whole number.");
        var bookedHours = Number(Hours, "hours", TripType != "Hourly");
        if (TripType == "Hourly" && bookedHours <= 0) throw new InvalidOperationException("Hourly journeys require positive booked hours.");
        var seats = CardPaymentService.Seats(BoosterSeats, InfantSeats);
        var trip = new Trip { Id = draftId, Name = Name, Phone = Phone, CustomerEmail = Email, From = Pickup, To = IsDistance ? Dropoff : "As directed", Notes = Notes, Flight = Flight, Price = calculatedFare?.Total ?? editing?.Price ?? 0, DistanceMiles = IsDistance ? routeMeters / 1609.344m : 0, GoogleDistanceMeters = IsDistance ? routeMeters : 0, RouteFrom = IsDistance ? Pickup : "", RouteTo = IsDistance ? Dropoff : "", RouteDistanceSource = IsDistance ? "Google Maps" : "", PricingRate = calculatedFare?.Rate ?? editing?.PricingRate ?? 0, PricingBandFrom = calculatedFare?.From ?? editing?.PricingBandFrom ?? 0, PricingBandTo = calculatedFare?.To ?? editing?.PricingBandTo ?? 0, Hours = bookedHours, People = passengers, Date = Date.ToString("yyyy-MM-dd"), Time = Time.ToString(@"hh\:mm"), Timezone = Timezone, TripType = TripType, VehicleType = VehicleType, Service = editing is not null&&editing.TripType==TripType?editing.Service:IsHourly ? "Hourly charter" : "Point to point", Status = "Unassigned", PickupAt = TripWorkflow.PickupUnix(Date, Time, Timezone) };
        trip.PassengerId = passenger.CustomerId; trip.CustomerId = passenger.CustomerId; trip.BookingSource = "Customer account";
        trip.ChildSeats = seats; trip.ChildSeatSummary = seats.Count == 0 ? "Not required" : string.Join(" · ", seats.Select(s => $"{s.Quantity} × {s.Type}"));
        trip.Paid = PaymentStatus; trip.Vehicle = SelectedVehicle?.DisplayName ?? "Unassigned";
        if(!KeepPrice){var confirmedQuote = await quotes.QuoteAsync(calculatedFare!.Total, passenger.CustomerId, couponCode: CouponCode, paymentMethod: PaymentMethod,tripId:editing?.Id);BookingQuoteService.Apply(trip, confirmedQuote, PaymentMethod);}
        if(editing is not null){
          if(auth.User?.Can("Reservations","Edit")!=true)throw new UnauthorizedAccessException();
          var fresh=(await dispatch.TripsAsync()).Trips.FirstOrDefault(t=>t.Id==editing.Id)??throw new InvalidOperationException("Journey no longer exists.");
          if(fresh.IsActive||fresh.Status is "Completed" or "Cancelled" or "No-show")throw new InvalidOperationException("This journey is locked. Refresh its current status.");
          if(!await nav.ConfirmAsync("Save changes",LocalizationService.Format("Save changes to the journey for {0}?",Name)))return;
          var patch=JsonSerializer.SerializeToElement(trip,ApiRepository.Json).EnumerateObject().ToDictionary(p=>p.Name,p=>(object?)p.Value.Clone());
          foreach(var key in new[]{"id","clientRequestId","status","driverId","driverName","vehicle","extras","history","isTest","bookingSource","paid","schedule","route","isActive"})patch.Remove(key);
          if(KeepPrice)foreach(var key in new[]{"price","fareBeforeDiscount","customerDiscount","couponDiscount","totalAfterDiscount","creditCardFeeRate","creditCardFeeFixed","creditCardFee","couponCode","couponId","customerDiscountSnapshot","paymentMethod","pricingRate","pricingBandFrom","pricingBandTo","routeFrom","routeTo","googleDistanceMeters","routeDistanceSource","distanceMiles"})patch.Remove(key);
          if(SelectedDriver is null)patch["vehicle"]=SelectedVehicle?.DisplayName??editing.Vehicle;
          // Existing ledger payment state is intentionally preserved; changing it uses a separate explicit action.
          
          await finance.UpdateAsync("trips",editing.Id,original,patch,version,"Reservations");
          created=(await dispatch.TripsAsync()).Trips.First(t=>t.Id==editing.Id);await FinishAssignment();return;
        }
        if (!await nav.ConfirmAsync("Create journey", LocalizationService.Format("Create the journey for {0} on {1}? Total: {2} {3}.", Name, trip.Schedule, trip.Price.ToString("0.00"), CompanySettingsService.Currency))) return;
        Trip savedTrip;
        try { savedTrip = await dispatch.CreateTripAsync(trip, version); }
        catch (ApiException ex) when ((int)ex.Status == 409) { version = (await dispatch.TripRecordsAsync()).Version; throw; }
        created = savedTrip; Raise(nameof(CanEdit)); Raise(nameof(SaveLabel));
        await FinishAssignment();
    }
    private async Task FinishAssignment()
    {
        if (created is null) return;
        if (SelectedDriver is not null)
        {
            if (SelectedVehicle is null) throw new InvalidOperationException("Ride saved. Select a vehicle and retry assignment.");
            var current = (await dispatch.TripsAsync()).Trips.FirstOrDefault(t => t.Id == created.Id) ?? throw new InvalidOperationException("Ride saved but could not be refreshed. Open Rides before retrying.");
            if (current.DriverId != SelectedDriver.TrackingId || current.Vehicle != SelectedVehicle.DisplayName)
            {
                if (current.IsActive || current.Status is "Completed" or "Cancelled" or "Delegated") throw new InvalidOperationException("The saved ride is now locked. Refresh it from Rides.");
                await dispatch.AssignAsync(current, SelectedDriver, SelectedVehicle.DisplayName);
            }
            created = (await dispatch.TripsAsync()).Trips.First(t => t.Id == created.Id);
        }
        saved = true; await nav.PopAsync(); await nav.OpenTripAsync(created);
    }
}
