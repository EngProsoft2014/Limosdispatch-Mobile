using System.Net;
using System.Text.Json;
using LimosDispatch.Core.Models;
using LimosDispatch.Core.Services;
using LimosDispatch.Core.Helpers;
using LimosDispatch.Core.GenericRepository;

var passed = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception("FAILED: " + name); Console.WriteLine("PASS " + name); passed++; }
var gpsFilter = new TrackingPointFilter();
var gpsStart = new GpsPoint(1794260000000,29.98,-95.34,5);
Check(gpsFilter.Accept(gpsStart), "Live tracking accepts initial map position");
Check(!gpsFilter.Accept(gpsStart)&&!gpsFilter.Accept(gpsStart with{CapturedAt=gpsStart.CapturedAt-1000}), "Live tracking rejects duplicate and out-of-order provider fixes");
Check(!gpsFilter.Accept(gpsStart with{CapturedAt=gpsStart.CapturedAt+1000,Latitude=29.981}), "Providers share one two-second moving clock across queue drains");
Check(gpsFilter.Accept(gpsStart with{CapturedAt=gpsStart.CapturedAt+2000,Latitude=29.981}), "Live moving sample accepted at two-second cadence");
Check(!gpsFilter.Accept(gpsStart with{CapturedAt=gpsStart.CapturedAt+60000,Latitude=29.981}), "Stopped driver does not resend coordinates after a minute");
Check(!gpsFilter.Accept(gpsStart with{CapturedAt=gpsStart.CapturedAt+62000,Latitude=29.981,Accuracy=2}), "Improved accuracy alone does not create stationary route points");
Check(!gpsFilter.Accept(gpsStart with{CapturedAt=gpsStart.CapturedAt+64000,Latitude=29.98105,SpeedMetersPerSecond=0}), "Stationary speed suppresses small GPS drift");
Check(gpsFilter.Accept(gpsStart with{CapturedAt=gpsStart.CapturedAt+66000,Latitude=29.982,SpeedMetersPerSecond=0}), "Measured displacement resumes movement even without useful sensor speed");
Check(gpsFilter.IsMoving(gpsStart.CapturedAt+68000)&&!gpsFilter.IsMoving(gpsStart.CapturedAt+80000), "Motion expires to stationary without extra coordinate uploads");
Check(!gpsFilter.Accept(gpsStart with{CapturedAt=gpsStart.CapturedAt+82000,Latitude=double.NaN}), "Invalid sensor coordinates rejected");
gpsFilter.Reset();Check(gpsFilter.Accept(gpsStart), "New journey resets motion sampling scope");
var retry = new TrackingUploadBackoff();var retryClock=DateTimeOffset.UnixEpoch;
Check(retry.CanAttempt(retryClock), "Tracking upload initially allowed");retry.Failed(retryClock);
Check(!retry.CanAttempt(retryClock.AddSeconds(14))&&retry.CanAttempt(retryClock.AddSeconds(15)), "Failed upload waits before retry");retry.Failed(retryClock.AddSeconds(15));
Check(!retry.CanAttempt(retryClock.AddSeconds(44))&&retry.CanAttempt(retryClock.AddSeconds(45)), "Repeated failure backs off thirty seconds");retry.Failed(retryClock.AddSeconds(45));
Check(!retry.CanAttempt(retryClock.AddSeconds(104))&&retry.CanAttempt(retryClock.AddSeconds(105)), "Offline backoff capped at sixty seconds");retry.Succeeded();Check(retry.CanAttempt(retryClock), "Successful upload resets retry backoff");
var movingFilter=new TrackingPointFilter();var stationaryFilter=new TrackingPointFilter();var movingAccepted=0;var stationaryAccepted=0;
for(var second=0;second<180;second++){if(movingFilter.Accept(gpsStart with{CapturedAt=gpsStart.CapturedAt+second*1000,Latitude=29.98+second*.0001}))movingAccepted++;if(stationaryFilter.Accept(gpsStart with{CapturedAt=gpsStart.CapturedAt+second*1000}))stationaryAccepted++;}
Check(movingAccepted==90&&stationaryAccepted==1, "One-second callbacks produce moving samples only, with no coordinate heartbeat");
Check(!JsonSerializer.Serialize(gpsStart with{SpeedMetersPerSecond=20},ApiRepository.Json).Contains("speed",StringComparison.OrdinalIgnoreCase), "Sensor speed remains local and does not alter location payload");
var queued=new List<GpsPoint>{gpsStart,gpsStart with{CapturedAt=gpsStart.CapturedAt+2000}};var sent=queued.ToList();
Check(TrackingAcknowledgements.Apply(queued,sent,new(){Inserted=0,AcknowledgedCapturedAt=[gpsStart.CapturedAt]})==1&&queued.Count==1,"Zero archive inserts still acknowledge live points, leaving unacknowledged points queued");
try{TrackingAcknowledgements.Apply(queued,queued.ToList(),new(){AcknowledgedCapturedAt=[123]});Check(false,"Unexpected ack");}catch(InvalidDataException){Check(queued.Count==1,"Unknown acknowledgement cannot discard queued points");}
var liveTrips=new List<Trip>{new(){Id="LIVE1",DriverId="D1",DriverName="QA driver",Status="Enroute"}};
var liveState=new LiveTrackingState();var time=gpsStart.CapturedAt;
Check(liveState.Presence(new(){TripId="LIVE1",DriverId="D1",Motion="stationary",ReceivedAt=time},liveTrips)&&liveState.Build(liveTrips).Count==0,"Presence alone cannot invent a vehicle position");
Check(liveState.Position(new(){TripId="LIVE1",DriverId="D1",CapturedAt=time+10,ReceivedAt=time+20,Latitude=29.98,Longitude=-95.34,Accuracy=5},liveTrips),"Live location accepted for active assigned journey");
liveState.Presence(new(){TripId="LIVE1",DriverId="D1",Motion="stationary",ReceivedAt=time+30000},liveTrips);var parked=liveState.Build(liveTrips).Single();
Check(parked.Latitude==29.98&&parked.Location!.ReceivedAt==time+20&&parked.Location.CapturedAt==time+10&&parked.LastContactAt==time+30000,"Heartbeat updates contact clock without changing GPS coordinate or capture clocks");
Check(parked.FreshnessAt(time+31000)=="Stationary"&&parked.FreshnessAt(time+121000)=="Connection delayed or unconfirmed","Presence remains stationary while fresh and becomes unconfirmed after ninety seconds");
Check(!liveState.Presence(new(){TripId="LIVE1",DriverId="D1",Motion="moving",ReceivedAt=time+100},liveTrips),"Old heartbeat cannot roll back connection state");
Check(!liveState.Position(new(){TripId="OTHER",DriverId="D1",CapturedAt=time+31000,ReceivedAt=time+31000,Latitude=1,Longitude=1},liveTrips),"Unassigned or foreign trip event rejected");
Check(!liveState.Position(new(){TripId="LIVE1",DriverId="D2",CapturedAt=time+31000,ReceivedAt=time+31000,Latitude=1,Longitude=1},liveTrips),"Wrong driver event rejected");
Check(liveState.Position(new(){TripId="LIVE1",DriverId="D1",CapturedAt=time+40000,ReceivedAt=time+40010,Latitude=29.99,Longitude=-95.33,Accuracy=5},liveTrips)&&liveState.Build(liveTrips).Single().Motion=="moving","Fresh measured position resumes moving status");
Check(!liveState.Position(new(){TripId="LIVE1",DriverId="D1",CapturedAt=time+30000,ReceivedAt=time+50000,Latitude=29.97,Longitude=-95.35,Accuracy=5},liveTrips),"Old sensor frame arriving late cannot move vehicle backwards");
liveState.Snapshot(new(){ServerNow=time+35000,Locations=[]},liveTrips);Check(liveState.Build(liveTrips).Count==1,"Older reconnect snapshot cannot erase a newer in-flight point");
liveState.Snapshot(new(){ServerNow=time+60000,Locations=[new(){TripId="LIVE1",DriverId="D1",StoredAt=time+55000,Latitude=30,Longitude=-95,Accuracy=4}]},liveTrips);var restored=liveState.Build(liveTrips).Single();
Check(restored.Latitude==30&&restored.Location!.CapturedAt==0&&restored.UpdatedAt==time+55000,"Snapshot storedAt restores position without fabricating capturedAt");
Check(!liveState.Position(new(){TripId="LIVE1",DriverId="D1",CapturedAt=time+45000,ReceivedAt=time+50000,Latitude=29.9,Longitude=-95.2},liveTrips),"Buffered event cannot overwrite a newer snapshot");
liveTrips[0].Status="Completed";Check(liveState.Build(liveTrips).Count==0,"Completed journey removes vehicle from live tracking");
Check(InputValidation.EmailError("alice+qa@example.com") == "" && InputValidation.EmailError("", false) == "", "Valid and optional email formats accepted");
Check(InputValidation.EmailError("alice@") != "" && InputValidation.EmailError("alice@example") != "" && InputValidation.EmailError("Alice <alice@example.com>") != "", "Malformed and display-name email inputs rejected");
Check(InputValidation.EmailError("", true) != "", "Required login email cannot be empty");
Check(InputValidation.PhoneError("+1 (202) 555-0179") == "" && InputValidation.PhoneError("+34 612 345 678") == "", "US and Spanish phone formats accepted without assuming one country");
Check(InputValidation.PhoneError("12") != "" && InputValidation.PhoneError("call2025550179") != "" && InputValidation.PhoneError("202+5550179") != "" && InputValidation.PhoneError("1234567890123456") != "", "Invalid phone length, letters and misplaced plus rejected");
Check(InputValidation.PhoneError("٠١٠١٢٣٤٥٦٧٨") == "" && InputValidation.NormalizeDigits("٠١٠١٢٣٤٥٦٧٨") == "01012345678", "Arabic phone digits normalize for API and search");
Check(CardPaymentService.Seats("0", "2").Single() == new ChildSeat("Infant seat", 2), "Child seats preserve web types and omit zero quantities");
try { CardPaymentService.Seats("21", "0"); Check(false, "Seat limit"); } catch (InvalidOperationException) { Check(true, "Child seat quantity obeys web maximum of 20"); }
try { CardPaymentService.Seats("1.5", "0"); Check(false, "Fractional seats"); } catch (InvalidOperationException) { Check(true, "Child seat quantities must be whole numbers"); }
Check(UserFeedback.SafeMessage("<html>server failure</html>") == UserFeedback.Message(new Exception()), "Unsafe server content uses safe fallback");
Check(UserFeedback.Message(new HttpRequestException()).Contains("internet"), "Network errors show English recovery message");
Check(UserFeedback.Message(new ApiException(HttpStatusCode.Conflict, "raw")).Contains("Refresh"), "Write conflicts request refresh");
var feedbackMessages = new List<string>();
UserFeedback.Presenter = text => { feedbackMessages.Add(text); return Task.CompletedTask; };
await UserFeedback.RunAsync(() => throw new InvalidOperationException("Choose a date."));
Check(feedbackMessages.SequenceEqual(["Choose a date."]), "UI boundary reports validation without escaping");
using (var cancelled = new CancellationTokenSource()) { cancelled.Cancel(); await UserFeedback.RunAsync(() => throw new OperationCanceledException(cancelled.Token)); }
Check(feedbackMessages.Count == 1, "Intentional cancellation is silent");
UserFeedback.Presenter = _ => throw new Exception("presentation failed");
await UserFeedback.ReportAsync(new HttpRequestException());
Check(true, "Feedback failure cannot crash an error handler");
UserFeedback.Presenter = _ => Task.CompletedTask;
var tiers = new TierPricing { Types = ["SUV"], Distance = [new() { From = 0, To = 50, Prices = [150] }, new() { From = 50, To = 80, Prices = [200] }], Hourly = [new() { From = 0, To = 2, Prices = [150] }, new() { From = 2, To = 4, Prices = [200] }] };
Check(tiers.Quote("Distance", "SUV", 49.99m).Total == 150 && tiers.Quote("Distance", "SUV", 50).Total == 200, "Distance bands use fixed fare and upper-exclusive boundaries like web");
Check(tiers.Quote("Hourly", "SUV", 2).Total == 400 && tiers.Quote("Hourly", "SUV", 4).Total == 800, "Hourly fare multiplies rate, including last upper boundary");
try { tiers.Quote("Distance", "SUV", 81); Check(false, "Unpriced route rejected"); } catch (InvalidOperationException) { Check(true, "Unpriced route cannot invent a fare"); }
var noticeTrip = new Trip { Id = "WEB1", BookingSource = "Website", PickupAt = 1, Extras = [new() { Id = "E1", Status = "Pending", Quantity = 10, Type = "Time", Unit = "minutes" }, new() { Id = "E2", Status = "Approved" }] };
var notices = RideInbox.Build([noticeTrip, new Trip { Id = "MANUAL" }], true, DateTimeOffset.UtcNow);
Check(notices.Count == 2 && notices.Select(n => n.Key).Distinct().Count() == 2, "Dispatch inbox separates website booking and pending extra without manual rides or approved extras");
Check(RideInbox.Build([noticeTrip], false, DateTimeOffset.UtcNow).Single().Title == "Upcoming ride", "Driver inbox retains overdue unfinished rides without dispatch-only Extra notices");
var googleRequests = new List<string>();
using var fakeGoogleHttp = new HttpClient(new FakeHandler(async request => {
    Check(request.Headers.Authorization is null && request.Headers.Contains("X-Goog-Api-Key"), "Google requests never receive tenant bearer tokens");
    googleRequests.Add(request.RequestUri!.AbsoluteUri);
    if (request.RequestUri.AbsoluteUri.Contains("places:autocomplete")) {
        var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
        Check(payload.RootElement.GetProperty("sessionToken").GetString() == "search-session", "Autocomplete passes per-search session token");
        return Response(HttpStatusCode.OK, """{"suggestions":[{"placePrediction":{"placeId":"P1","text":{"text":"Houston airport"}}}]}""");
    }
    if (request.Method == HttpMethod.Get) return Response(HttpStatusCode.OK, """{"id":"P1","formattedAddress":"Houston, TX, USA"}""");
    return Response(HttpStatusCode.OK, """{"routes":[{"distanceMeters":1609.344}]}""");
}));
var google = new GoogleAddresses(fakeGoogleHttp, "fake-key");
var suggestions = await google.SearchAsync("Houston", "search-session");
var selectedAddress = await google.SelectAsync(suggestions.Single(), "search-session");
Check(selectedAddress.Address == "Houston, TX, USA" && googleRequests[1].Contains("sessionToken=search-session"), "Selection resolves formatted address and completes same Google session");
Check(Math.Abs((await google.RouteAsync("A", "B")).Miles - 1) < 0.00001, "Driving meters convert to miles using 1609.344");
using var emptyGoogle = new HttpClient(new FakeHandler(_ => Task.FromResult(Response(HttpStatusCode.OK, """{"routes":[]}"""))));
try { await new GoogleAddresses(emptyGoogle, "fake-key").RouteAsync("A", "B"); Check(false, "Missing road route"); } catch (InvalidOperationException) { Check(true, "Missing road route is an error, never a straight-line estimate"); }
var trip = JsonSerializer.Deserialize<Trip>("""{"id":"T1","people":"1","hours":"2","distanceMiles":"","price":415,"status":"Confirmed"}""", ApiRepository.Json)!;
Check(trip.People == 1 && trip.Hours == 2 && trip.DistanceMiles == 0 && trip.Price == 415, "Deployed mixed numeric strings");
var tracking = JsonSerializer.Deserialize<TrackingEnvelope>("""{"drivers":[{"id":"D1","name":"Test driver","location":{"latitude":29.76,"longitude":-95.37,"sharing":true,"receivedAt":1794000000000,"accuracy":10}}]}""", ApiRepository.Json)!;
Check(tracking.Drivers[0].Latitude == 29.76 && tracking.Drivers[0].Location!.Sharing && tracking.Drivers[0].UpdatedAt == 1794000000000, "Nested tracking location contract used by deployed web client");
Check(TripWorkflow.Next("Confirmed") == "Enroute" && TripWorkflow.Next("Enroute") == "Arrived" && TripWorkflow.Next("Arrived") == "Onboard" && TripWorkflow.Next("Onboard") == "Completed", "All ordered journey stages");
Check(TripWorkflow.Next("Completed") is null && TripWorkflow.Next("Cancelled") is null && TripWorkflow.Next("Unassigned") is null, "Terminal/unassigned journeys cannot advance");
try { TripWorkflow.ValidateTransition(trip, "Completed"); Check(false, "Skip rejected"); } catch (InvalidOperationException) { Check(true, "Skipping stages rejected"); }
Check(TripWorkflow.PickupUnix(new DateTime(2026, 11, 5), new TimeSpan(18, 0, 0), "America/Chicago") == 1793923200000, "Pickup uses company timezone, matches live API");
try { TripWorkflow.PickupUnix(new DateTime(2026, 3, 8), new TimeSpan(2, 30, 0), "America/Chicago"); Check(false, "DST gap"); } catch (InvalidOperationException) { Check(true, "Invalid DST time rejected"); }
try { TripWorkflow.PickupUnix(new DateTime(2026, 11, 1), new TimeSpan(1, 30, 0), "America/Chicago"); Check(false, "DST ambiguity"); } catch (InvalidOperationException) { Check(true, "Ambiguous DST time rejected"); }
var user = new UserProfile { Role = "Driver", Company = "test" };
Check(!user.CanDispatch && !user.CanAssign && !user.CanCreateTrip, "Driver does not inherit dispatch access");
user.Permissions["Dispatch"] = new() { View = true };
Check(user.CanDispatch && !user.CanAssign, "Read dispatch permission does not grant assignment");
user.Permissions["Reservations"] = new() { View = true, Edit = true };
Check(user.CanAssign && !user.CanCreateTrip, "Assignment and creation permissions separated");
var refreshCount = 0;
var store = new MemoryStore { Pair = new() { AccessToken = "old", RefreshToken = "rotating", Company = "test", ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1) } };
var handler = new FakeHandler(async request => {
    if (request.RequestUri!.AbsolutePath.EndsWith("refresh"))
    { Interlocked.Increment(ref refreshCount); await Task.Delay(30); return Response(HttpStatusCode.OK, """{"accessToken":"new","refreshToken":"new-refresh","expiresAt":"2099-01-01T00:00:00Z"}"""); }
    return Response(HttpStatusCode.OK, "{}");
});
var auth = new AuthService(new HttpClient(handler) { BaseAddress = new("https://example.test/") }, store);
var values = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => auth.GetTokenAsync()));
Check(refreshCount == 1 && values.All(t => t == "new") && store.Pair!.RefreshToken == "new-refresh", "20 concurrent consumers rotate refresh token once");
await auth.RefreshAfterRejectionAsync("old"); Check(refreshCount == 1, "Stale 401 does not reuse old refresh token");
try { auth.SetUser(new() { Company = "other" }); Check(false, "Tenant mismatch"); } catch (InvalidOperationException) { Check(true, "Tenant mismatch rejected"); }
foreach (var role in new[] { "Driver", "Admin" })
{
    var sessionStore = new MemoryStore { Pair = new() { AccessToken = "revoked", RefreshToken = "revoked-refresh", Company = "test", ExpiresAt = DateTimeOffset.UtcNow.AddHours(1) } };
    var requests = 0; var signouts = 0;
    var sessionAuth = new AuthService(new HttpClient(new FakeHandler(_ =>
    {
        requests++;
        return Task.FromResult(Response(HttpStatusCode.Unauthorized, "{}"));
    })) { BaseAddress = new("https://example.test/") }, sessionStore);
    await sessionAuth.GetTokenAsync(); sessionAuth.SetUser(new() { Company = "test", Role = role });
    sessionAuth.SignedOut += () => signouts++;
    try { await sessionAuth.ValidateCurrentSessionAsync(); } catch (UnauthorizedAccessException) { }
    Check(requests == 2 && signouts == 1 && sessionAuth.User is null && sessionStore.Pair is null,
        $"{role} foreground validation signs out revoked unexpired session without push");
}
foreach (var status in new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.ServiceUnavailable })
{
    var sessionStore = new MemoryStore { Pair = new() { AccessToken = "old", RefreshToken = "refresh", Company = "test", ExpiresAt = DateTimeOffset.UtcNow.AddHours(1) } };
    var sessionAuth = new AuthService(new HttpClient(new FakeHandler(request => Task.FromResult(
        request.RequestUri!.AbsolutePath.EndsWith("refresh")
            ? Response(HttpStatusCode.OK, """{"accessToken":"renewed","refreshToken":"next","expiresAt":"2099-01-01T00:00:00Z"}""")
            : Response(status, "{}")))) { BaseAddress = new("https://example.test/") }, sessionStore);
    await sessionAuth.GetTokenAsync(); sessionAuth.SetUser(new() { Company = "test", Role = "Admin" });
    try { await sessionAuth.ValidateCurrentSessionAsync(); } catch (ApiException) { }
    Check((sessionAuth.User is null) == (status == HttpStatusCode.Unauthorized) &&
        (sessionStore.Pair is null) == (status == HttpStatusCode.Unauthorized),
        $"Session validation clears repeated 401 but retains session for {status}");
}
{
    var sessionStore = new MemoryStore { Pair = new() { AccessToken = "current", RefreshToken = "refresh", Company = "test", ExpiresAt = DateTimeOffset.UtcNow.AddHours(1) } };
    var sessionAuth = new AuthService(new HttpClient(new FakeHandler(_ => throw new HttpRequestException("Offline"))) { BaseAddress = new("https://example.test/") }, sessionStore);
    await sessionAuth.GetTokenAsync(); sessionAuth.SetUser(new() { Company = "test", Role = "Driver" });
    await sessionAuth.RejectTokenAsync("previous-login");
    Check(sessionAuth.User is not null && sessionStore.Pair is not null, "Delayed rejection from previous login preserves current session");
    try { await sessionAuth.ValidateCurrentSessionAsync(); } catch (HttpRequestException) { }
    Check(sessionAuth.User is not null && sessionStore.Pair is not null, "Offline session check preserves login and secure session");
}
var count = 0;
var conflict = new ApiRepository(new HttpClient(new FakeHandler(_ => { count++; return Task.FromResult(Response(HttpStatusCode.Conflict, "{}")); })) { BaseAddress = new("https://example.test/") }, new StaticTokens());
try { await conflict.PostAsync<JsonElement>("api/records/trips", new { }); Check(false, "Conflict"); } catch (ApiException e) { Check((int)e.Status == 409 && count == 1, "Conflicted write is never automatically repeated"); }
var calls = 0; var tokens = new StaticTokens();
var unauthorized = new ApiRepository(new HttpClient(new FakeHandler(r => { calls++; return Task.FromResult(Response(calls == 1 ? HttpStatusCode.Unauthorized : HttpStatusCode.OK, "{}")); })) { BaseAddress = new("https://example.test/") }, tokens);
await unauthorized.GetAsync<JsonElement>("api/trips"); Check(calls == 2 && tokens.Refreshes == 1, "401 renewed and replayed once");
try { await unauthorized.GetAsync<JsonElement>("https://another.test/"); Check(false, "Absolute URI"); } catch (ArgumentException) { Check(true, "Repository rejects credential forwarding to absolute URLs"); }
auth.SetUser(new UserProfile { Company = "test", Role = "Admin" });
var passengerRows = new List<Passenger>(); var passengerWrites = 0; var losePassengerResponse = false;
using var passengerHttp = new HttpClient(new FakeHandler(async request =>
{
    if (request.Method == HttpMethod.Post)
    {
        passengerWrites++;
        var input = JsonSerializer.Deserialize<RecordInput<Passenger>>(await request.Content!.ReadAsStringAsync(), ApiRepository.Json)!;
        input.Record.Id = "server-passenger-id"; passengerRows.Add(input.Record);
        if (losePassengerResponse) throw new HttpRequestException("Response lost after save");
    }
    return Response(HttpStatusCode.OK, JsonSerializer.Serialize(new Records<Passenger> { Version = passengerWrites + 10, Rows = passengerRows }, ApiRepository.Json));
})) { BaseAddress = new("https://example.test/") };
var passengerService = new PassengerService(new ApiRepository(passengerHttp, new StaticTokens()), auth);
var passengerDraft = new Passenger { CustomerId = "CUS-test", Name = "  Mobile   Passenger  ", Phone = "+1 (202) 555-0179", Email = "qa@example.test" };
var savedPassenger = await passengerService.SaveAsync(passengerDraft);
Check(savedPassenger.Id == "server-passenger-id" && savedPassenger.CustomerId == "CUS-test" && savedPassenger.Name == "Mobile Passenger", "Passenger save returns server record ID and stable customer ID with normalized name");
await passengerService.SaveAsync(new Passenger { CustomerId = "CUS-other", Name = "mobile passenger", Phone = "12025550179" });
Check(passengerWrites == 1, "Passenger name and normalized phone reuse an existing contact like web");
Check(PassengerService.Search(passengerRows, "(202) 555").Count() == 1 && PassengerService.Search(passengerRows, "mobile").Count() == 1 && PassengerService.Search(passengerRows, "example.test").Count() == 1, "Passenger directory searches name, formatted phone and email");
try { await passengerService.SaveAsync(new Passenger { CustomerId = "bad", Name = "QA", Phone = "12" }); Check(false, "Invalid phone"); } catch (InvalidOperationException) { Check(passengerWrites == 1, "Passenger phone validation prevents an API write"); }
try { await passengerService.SaveAsync(new Passenger { CustomerId = "bad", Name = "QA", Phone = "1234567890", Email = "bad email" }); Check(false, "Invalid email"); } catch (InvalidOperationException) { Check(passengerWrites == 1, "Passenger email validation prevents an API write"); }
losePassengerResponse = true;
var recoverPassenger = new Passenger { CustomerId = "CUS-recover", Name = "Recovery Test", Phone = "2025550198" };
try { await passengerService.SaveAsync(recoverPassenger); Check(false, "Lost response"); } catch (HttpRequestException) { }
var recoveredPassenger = await passengerService.SaveAsync(recoverPassenger);
Check(passengerWrites == 2 && recoveredPassenger.CustomerId == "CUS-recover", "Retry after an ambiguous passenger save recovers the stable customer ID without another write");
auth.SetUser(new UserProfile { Company = "test", Role = "Driver" });
try { await passengerService.SaveAsync(passengerDraft); Check(false, "Driver passenger creation"); } catch (UnauthorizedAccessException) { Check(passengerWrites == 2, "Driver without Customers Add cannot save passengers"); }
auth.SetUser(new UserProfile { Company = "test", Role = "Admin" });
using var quoteHttp = new HttpClient(new FakeHandler(_ => Task.FromResult(Response(HttpStatusCode.OK, """{"price":80,"fareBeforeDiscount":100,"customerDiscount":20,"totalAfterDiscount":80}""")))) { BaseAddress = new("https://example.test/") };
var checkedQuote = await new BookingQuoteService(new ApiRepository(quoteHttp, new StaticTokens())).QuoteAsync(100, "CUS-test");
var pricedTrip = new Trip { CustomerId = "CUS-test" }; BookingQuoteService.Apply(pricedTrip, checkedQuote);
Check(pricedTrip.Price == 80 && pricedTrip.FareBeforeDiscount == 100 && pricedTrip.CustomerDiscount == 20 && pricedTrip.PaymentMethod == "Cash", "Selected customer's server quote and discount are retained on the booking");
Check(new AddressSuggestion("P1", "Airport, Houston, TX").PrimaryText == "Airport" && new AddressSuggestion("P1", "Airport, Houston, TX").SecondaryText == "Houston, TX", "Address results separate place title from detailed location");
var savedOnServer = false; var postCount = 0; var createOmitsExtras = false;
const string serverRecord = """{"version":5,"rows":[{"id":"server-generated-id","clientRequestId":"draft-client-id","name":"QA"}]}""";
var recordApi = new ApiRepository(new HttpClient(new FakeHandler(async r =>
{
    if (r.Method == HttpMethod.Post) { savedOnServer = true; postCount++; using var body = JsonDocument.Parse(await r.Content!.ReadAsStringAsync()); createOmitsExtras = !body.RootElement.GetProperty("record").TryGetProperty("extras", out _); }
    return Response(HttpStatusCode.OK, savedOnServer ? serverRecord : """{"version":4,"rows":[]}""");
})) { BaseAddress = new("https://example.test/") }, new StaticTokens());
var recordsService = new DispatchService(recordApi, auth);
var draft = new Trip { Id = "draft-client-id", Name = "QA" };
var serverTrip = await recordsService.CreateTripAsync(draft, 4);
Check(serverTrip.Id == "server-generated-id", "Create journey uses server-generated identifier from records envelope");
Check(createOmitsExtras, "New trip payload excludes server-owned extras, including an empty list");
var recoveredTrip = await recordsService.CreateTripAsync(draft, 4);
Check(recoveredTrip.Id == serverTrip.Id && postCount == 1, "Retry recovers already-saved draft without creating a duplicate");
var loginStorage = new MemoryLoginStorage();
var loginMemory = new LoginMemory(loginStorage);
await loginMemory.SaveAsync(" LUX ", " driver@example.test ", "test-password", true);
var remembered = await loginMemory.LoadAsync();
Check(remembered is { Company: "lux", Email: "driver@example.test", Password: "test-password" }, "Remember me restores credentials for the normalized company");
await loginMemory.SaveAsync("lux", "driver@example.test", "test-password", false);
Check(await loginMemory.LoadAsync() is null, "Unchecked remember me removes previously saved credentials");
await loginMemory.SaveAsync("lux", "driver@example.test", "test-password", true);
await loginMemory.ClearAsync();
Check(await loginMemory.LoadAsync() is null, "Company change can clear the remembered login");
var savingLogin = loginMemory.SaveAsync("lux", "driver@example.test", "test-password", true);
var clearingLogin = loginMemory.ClearAsync();
await Task.WhenAll(savingLogin, clearingLogin);
Check(await loginMemory.LoadAsync() is null, "Company-change clear wins over an earlier in-flight credential save");
loginStorage.Value = "invalid-json";
Check(await loginMemory.LoadAsync() is null && loginStorage.Value is null, "Corrupt saved login is removed safely");
var screenNow = new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);
Check(!ScreenRules.IsPast(new Trip { Status = "Enroute", PickupAt = screenNow.AddDays(-1).ToUnixTimeMilliseconds() }, screenNow), "Active rides remain accessible in Upcoming even after pickup time");
Check(!ScreenRules.IsPast(new Trip { Status = "Confirmed", PickupAt = screenNow.AddDays(-1).ToUnixTimeMilliseconds() }, screenNow), "Overdue Confirmed rides remain in Upcoming for admin and driver");
Check(ScreenRules.IsPast(new Trip { Status = "Completed", PickupAt = screenNow.AddDays(1).ToUnixTimeMilliseconds() }, screenNow), "Completed rides always appear in Past");
Check(!ScreenRules.IsPast(new Trip { Status = "Confirmed", PickupAt = screenNow.AddDays(1).ToUnixTimeMilliseconds() }, screenNow), "Future rides appear in Upcoming");
ScreenRules.ValidateExpenseDate(screenNow.Date, screenNow.Date);
ScreenRules.ValidateExpenseDate(screenNow.Date.AddDays(-1), screenNow.Date);
Check(true, "Expense dates accept today and earlier days");
try { ScreenRules.ValidateExpenseDate(screenNow.Date.AddDays(1), screenNow.Date); Check(false, "Future expense date"); }
catch (InvalidOperationException) { Check(true, "Future expense date is rejected"); }
try { await recordsService.SaveExpenseAsync(new Expense { Date = DateTime.Today.AddDays(1).ToString("yyyy-MM-dd") }, 4); Check(false, "Future expense API write"); }
catch (InvalidOperationException ex) { Check(ex.Message.Contains("future") && postCount == 1, "Future expense is blocked before any API write"); }
var pdfBytes = "%PDF-1.4\n1 0 obj\n<<>>\nendobj\n%%EOF"u8.ToArray();
var pdfReceipt = ReceiptFiles.Create("MOBILE_QA.pdf", pdfBytes);
Check(pdfReceipt.Data.StartsWith("data:application/pdf;base64,") && ReceiptFiles.Read(pdfReceipt).SequenceEqual(pdfBytes), "PDF receipts retain their content and document MIME type");
Check(ReceiptFiles.DetectMime(new byte[] {137,80,78,71,13,10,26,10}) == "image/png", "PNG receipts are recognized by their file signature");
Check(ReceiptFiles.DetectMime("RIFF1234WEBP"u8.ToArray()) == "image/webp", "WebP receipts match the web upload formats");
try { ReceiptFiles.Read(new Receipt { Data = "data:image/png;base64," + Convert.ToBase64String(pdfBytes) }); Check(false, "MIME mismatch"); }
catch (InvalidOperationException) { Check(true, "Receipt MIME spoofing is rejected"); }
try { ReceiptFiles.Create("unsupported.docx", "PK-invalid"u8.ToArray()); Check(false, "Unsupported file"); }
catch (InvalidOperationException) { Check(true, "Unsupported receipt file types are rejected"); }
try { ReceiptFiles.Create("large.pdf", new byte[ReceiptFiles.MaximumBytes + 1]); Check(false, "Oversized file"); }
catch (InvalidOperationException) { Check(true, "Oversized receipt files are rejected before upload"); }
var pdfPosted = false;
var pdfApi = new ApiRepository(new HttpClient(new FakeHandler(async request =>
{
    if (request.Method == HttpMethod.Post)
    {
        pdfPosted = (await request.Content!.ReadAsStringAsync()).Contains("data:application/pdf;base64,");
        return Response(HttpStatusCode.OK, """{"version":2,"rows":[{"id":"saved-pdf","clientRequestId":"pdf-draft"}]}""");
    }
    return Response(HttpStatusCode.OK, """{"version":1,"rows":[]}""");
})) { BaseAddress = new("https://example.test/") }, new StaticTokens());
var pdfSaved = await new DispatchService(pdfApi, auth).SaveExpenseAsync(new Expense { Id = "pdf-draft", Name = "QA", Category = "Fuel", Amount = 1, Date = DateTime.Today.ToString("yyyy-MM-dd"), Receipt = pdfReceipt }, 1);
Check(pdfPosted && pdfSaved.Id == "saved-pdf", "Expense API payload supports PDF receipts and reads the server record ID");
var optionalReceiptPosted=false;
var optionalReceiptApi=new ApiRepository(new HttpClient(new FakeHandler(async request=>{
    if(request.Method==HttpMethod.Post){using var payload=JsonDocument.Parse(await request.Content!.ReadAsStringAsync());optionalReceiptPosted=payload.RootElement.GetProperty("record").GetProperty("receipt").ValueKind==JsonValueKind.Null;
        return Response(HttpStatusCode.OK,"""{"version":2,"rows":[{"id":"saved-optional","clientRequestId":"optional-draft"}]}""");}
    return Response(HttpStatusCode.OK,"""{"version":1,"rows":[]}""");
})){BaseAddress=new Uri("https://example.test/")},new StaticTokens());
var optionalExpense=new Expense{Id="optional-draft",Name="QA",Category="Fuel",Amount=1,Date=DateTime.Today.ToString("yyyy-MM-dd")};
var optionalSaved=await new DispatchService(optionalReceiptApi,auth).SaveExpenseAsync(optionalExpense,1);
Check(optionalReceiptPosted&&optionalSaved.Id=="saved-optional","Expense can be saved without a receipt like the web");
optionalExpense.Receipt=new Receipt{Data="invalid"};
try{await new DispatchService(optionalReceiptApi,auth).SaveExpenseAsync(optionalExpense,1);Check(false,"Optional attachment validation");}catch(InvalidOperationException){Check(true,"An optional receipt is still validated when attached");}
TokenPair SavedSession(bool expired = false) => new() { Company = "test", AccessToken = "saved-access", RefreshToken = "saved-refresh", ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(expired ? -1 : 10) };
(AuthService Auth, SessionRestoreService Restorer, MemoryStore Store, ApiRepository Api) RestoreFixture(TokenPair? pair, Func<HttpRequestMessage, Task<HttpResponseMessage>> handle)
{
    var memory = new MemoryStore { Pair = pair };
    var client = new HttpClient(new FakeHandler(handle)) { BaseAddress = new("https://example.test/") };
    var service = new AuthService(client, memory);
    var repository = new ApiRepository(client, service);
    return (service, new SessionRestoreService(service, memory, repository), memory, repository);
}
var noSessionCalls = 0;
var noSession = RestoreFixture(null, _ => { noSessionCalls++; return Task.FromResult(Response(HttpStatusCode.OK, "{}")); });
Check(await noSession.Restorer.RestoreAsync() is null && noSessionCalls == 0, "First launch without a saved session needs login without API calls");
var loginCalls = 0;
var startup = RestoreFixture(SavedSession(), request => { if (request.RequestUri!.AbsolutePath.EndsWith("login")) loginCalls++; return Task.FromResult(Response(HttpStatusCode.OK, """{"company":"test","role":"Admin","name":"Admin"}""")); });
var startupUser = await startup.Restorer.RestoreAsync();
Check(startupUser?.Role == "Admin" && startup.Auth.User == startupUser && loginCalls == 0, "Saved session restores the authenticated workspace without password login");
await startup.Auth.LogoutAsync(startup.Api);
Check(await startup.Restorer.RestoreAsync() is null, "Explicit sign-out prevents automatic workspace restoration");
var startupRefreshes = 0;
var expiredStartup = RestoreFixture(SavedSession(true), request =>
{
    if (request.RequestUri!.AbsolutePath.EndsWith("refresh")) { startupRefreshes++; return Task.FromResult(Response(HttpStatusCode.OK, """{"accessToken":"renewed","refreshToken":"rotated","expiresAt":"2099-01-01T00:00:00Z"}""")); }
    return Task.FromResult(Response(HttpStatusCode.OK, """{"company":"test","role":"Driver"}"""));
});
Check((await expiredStartup.Restorer.RestoreAsync())?.Role == "Driver" && startupRefreshes == 1 && expiredStartup.Store.Pair?.RefreshToken == "rotated", "Expired access token renews before restoring the chauffeur workspace");
var rejectedStartup = RestoreFixture(SavedSession(true), _ => Task.FromResult(Response(HttpStatusCode.Unauthorized, "{}")));
Check(await rejectedStartup.Restorer.RestoreAsync() is null && rejectedStartup.Store.Pair is null, "Rejected refresh requires login and removes the invalid session");
var wrongTenantStartup = RestoreFixture(SavedSession(), _ => Task.FromResult(Response(HttpStatusCode.OK, """{"company":"other","role":"Admin"}""")));
Check(await wrongTenantStartup.Restorer.RestoreAsync() is null && wrongTenantStartup.Store.Pair is null, "Startup never restores a profile from another company");
var unavailableStartup = RestoreFixture(SavedSession(true), _ => Task.FromResult(Response(HttpStatusCode.ServiceUnavailable, "{}")));
try { await unavailableStartup.Restorer.RestoreAsync(); Check(false, "Temporary startup outage"); }
catch (ApiException ex) { Check((int)ex.Status == 503 && unavailableStartup.Store.Pair is not null, "Temporary renewal failure keeps the session for retry"); }
var offlineStartup = RestoreFixture(SavedSession(), _ => throw new HttpRequestException("Offline"));
try { await offlineStartup.Restorer.RestoreAsync(); Check(false, "Offline startup"); }
catch (HttpRequestException) { Check(offlineStartup.Store.Pair is not null, "Offline startup preserves the saved session"); }
var pushData = new Dictionary<string,string> { ["type"]="ride_assigned", ["company"]="lux", ["tripId"]="LD-QA-1", ["notificationId"]="0123456789abcdef0123456789abcdef" };
Check(PushTarget.FromData(pushData) == new PushTarget("lux", "LD-QA-1", pushData["notificationId"]), "FCM routing reads the documented payload without relying on title/body");
Check(PushTarget.FromData(new Dictionary<string,string>(pushData) { ["type"]="unknown" }) is null, "Unknown notification events cannot route to a ride");
Check(PushTarget.FromData(new Dictionary<string,string>(pushData) { ["tripId"]="../other" }) is null, "Notification route rejects path traversal");
Check(PushTarget.FromData(new Dictionary<string,string>(pushData) { ["company"]="lux/other" }) is null, "Notification route validates the company identifier");
Check(PushTarget.FromData(new Dictionary<string,string>(pushData) { ["notificationId"]="" }) is null, "FCM taps require a notification ID for deduplication and read state");
Check(PushTarget.Create("LUX", "LD-QA-1")?.Company == "lux", "External ride links normalize company codes without altering ride IDs");
var pushRequests = new List<(string Method,string Path,string Body)>();
var pushRepo = new ApiRepository(new HttpClient(new FakeHandler(async request =>
{
    var path = request.RequestUri!.PathAndQuery;
    pushRequests.Add((request.Method.Method, path, request.Content is null ? "" : await request.Content.ReadAsStringAsync()));
    if (request.Method == HttpMethod.Delete) return Response(HttpStatusCode.NoContent, "");
    if (path.StartsWith("/api/trips/")) return Response(HttpStatusCode.OK, """{"trip":{"id":"LD-QA-1","status":"Confirmed"}}""");
    if (path.StartsWith("/api/notifications?")) return Response(HttpStatusCode.OK, """{"items":[{"id":"notice-1","company":"lux","tripId":"LD-QA-1","createdAt":"2026-11-08T18:30:00+00:00","isRead":false}],"nextCursor":"opaque+/=","unreadCount":8}""");
    return Response(HttpStatusCode.OK, """{"registered":true,"unreadCount":7}""");
})) { BaseAddress = new("https://example.test/") }, new StaticTokens());
var pushApi = new PushApi(pushRepo);
var pushInbox = await pushApi.InboxAsync("opaque+/=");
Check(pushRequests[^1].Path.EndsWith("cursor=opaque%2B%2F%3D") && pushInbox.UnreadCount == 8 && pushInbox.Items.Count == 1, "Inbox cursor is opaque and URL-encoded; badge uses the server count, not page size");
Check((await pushApi.TripAsync("LD-QA-1")).Status == "Confirmed" && pushRequests[^1].Path == "/api/trips/LD-QA-1", "Every notification open fetches the currently authorized trip detail endpoint");
var install = Guid.NewGuid().ToString();
await pushApi.RegisterAsync(new(install, "android", "synthetic-token", "1.0"));
using(var deviceBody = JsonDocument.Parse(pushRequests[^1].Body))
{
    Check(deviceBody.RootElement.GetProperty("registrationToken").GetString() == "synthetic-token" && !deviceBody.RootElement.TryGetProperty("company", out _) && !deviceBody.RootElement.TryGetProperty("userId", out _), "Device registration lets JWT determine tenant and user ownership");
}
var pushCallCount = pushRequests.Count;
try { await pushApi.RegisterAsync(new(install,"ios","bad token","1.0")); Check(false,"Invalid FCM token"); } catch(InvalidOperationException) { Check(pushRequests.Count == pushCallCount,"Invalid token is rejected before registration"); }
Check((await pushApi.ReadAsync("notice-1")).UnreadCount == 7 && pushRequests[^1].Path == "/api/notifications/notice-1/read", "Reading a notification updates the authoritative unread count");
await pushApi.ReadAllAsync(); Check(pushRequests[^1].Path == "/api/notifications/read-all", "Mark-all-read uses the server inbox endpoint");
await pushApi.UnregisterAsync(install); Check(pushRequests[^1].Method == "DELETE" && pushRequests[^1].Path.EndsWith(install), "Unregister supports idempotent 204 before logout");
var registrationAttempts = 0;
var conflictPush = new PushApi(new ApiRepository(new HttpClient(new FakeHandler(_ => Task.FromResult(Response(++registrationAttempts == 1 ? HttpStatusCode.Conflict : HttpStatusCode.OK, "{}")))) { BaseAddress = new("https://example.test/") }, new StaticTokens()));
await conflictPush.RegisterAsync(new(install, "ios", "synthetic-token", "1.0"));
Check(registrationAttempts == 2, "Device registration retries a concurrent 409 once as specified by the handoff");

// Driver workflow parity: real HTTP serialization, without writing a live journey/payroll.
var journeyJson = """{"trip":{"id":"QA","driverId":"DRV-QA","status":"Confirmed","history":[{"status":"Confirmed","at":1794234000000}],"extras":[{"id":"E","status":"Approved","type":"Time","quantity":90,"unit":"minutes","requestedBy":"QA","requestedAt":1794234000000,"reviewedAt":1794234001000,"reviewedBy":"Dispatch","reviewNote":"Confirmed","amount":25}]},"canStart":false,"startWindowMinutes":180,"startAvailableAt":1794234000000,"trackingAllowed":false}""";
var envelope = JsonSerializer.Deserialize<DriverJourney>(journeyJson, ApiRepository.Json)!;
Check(!envelope.CanStart && envelope.StartWindowMinutes == 180 && envelope.StartAvailableAt == 1794234000000, "Driver detail preserves server-owned start window and permission");
Check(envelope.Trip.History.Single().At == 1794234000000 && envelope.Trip.Extras.Single().ReviewedBy == "Dispatch" && envelope.Trip.Extras.Single().Amount == 25, "Journey history and extra review details survive API deserialization");
var sentStatuses = new List<string>();
var extraBodies = new List<string>();
using var journeyHttp = new HttpClient(new FakeHandler(async r => {
    if (r.RequestUri!.AbsolutePath.EndsWith("/auth/login")) return Response(HttpStatusCode.OK, """{"accessToken":"qa","refreshToken":"qa-refresh","expiresAt":"2099-01-01T00:00:00Z"}""");
    if (r.Method == HttpMethod.Get) return Response(HttpStatusCode.OK, journeyJson);
    var body = await r.Content!.ReadAsStringAsync();
    if (r.RequestUri.AbsolutePath.EndsWith("/trip/status")) sentStatuses.Add(JsonDocument.Parse(body).RootElement.GetProperty("status").GetString()!);
    else extraBodies.Add(body);
    return Response(HttpStatusCode.OK, "{}");
})) { BaseAddress = new Uri("https://qa.invalid/") };
var journeyAuth = new AuthService(journeyHttp, new MemoryStore());
await journeyAuth.LoginAsync("lux", "qa@example.com", "qa");
journeyAuth.SetUser(new UserProfile { Role = "Driver", Company = "lux", DriverId = "DRV-QA" });
var journeyApi = new DispatchService(new ApiRepository(journeyHttp, journeyAuth), journeyAuth);
Check(!(await journeyApi.DriverJourneyAsync("QA")).CanStart, "Driver detail service retains envelope instead of dropping canStart");
var stageTrip = envelope.Trip;
foreach (var stage in new[] { "Enroute", "Arrived", "Onboard", "Completed" }) {
    await journeyApi.ChangeStatusAsync(stageTrip, stage);
    stageTrip.Status = stage;
}
Check(sentStatuses.SequenceEqual(new[] { "Enroute", "Arrived", "Onboard", "Completed" }), "All four driver stages use the web status endpoint in order through Finish");
try { await journeyApi.ChangeStatusAsync(stageTrip, "Enroute"); Check(false, "Closed journey cannot restart"); } catch (InvalidOperationException) { Check(true, "Closed journey cannot restart"); }
stageTrip.Status = "Onboard";
var extraRequest = new TripExtraRequest("QA", Guid.NewGuid().ToString(), "Time", 90, "Customer requested waiting time");
await journeyApi.RequestExtraAsync(stageTrip, extraRequest);
await journeyApi.RequestExtraAsync(stageTrip, extraRequest);
Check(extraBodies.Count == 2 && extraBodies[0] == extraBodies[1] && JsonDocument.Parse(extraBodies[0]).RootElement.GetProperty("quantity").GetDecimal() == 90, "Extra retry preserves request UUID and sends Time as minutes without converting to hours");
foreach (var invalid in new[] { extraRequest with { Quantity = 0 }, extraRequest with { Quantity = 100001 }, extraRequest with { Description = new string('x',1001) }, extraRequest with { TripId = "other" } }) {
    try { TripWorkflow.ValidateExtra(stageTrip, invalid); Check(false,"Invalid extra rejected"); } catch (InvalidOperationException) { Check(true,"Invalid extra rejected"); }
}
stageTrip.Status = "Confirmed";
try { await journeyApi.RequestExtraAsync(stageTrip, extraRequest); Check(false, "No extras before On the way"); } catch (InvalidOperationException) { Check(true, "No extras before On the way"); }
journeyAuth.SetUser(new UserProfile { Role = "Driver", Company = "lux", DriverId = "other" }); stageTrip.Status = "Onboard";
try { await journeyApi.RequestExtraAsync(stageTrip, extraRequest); Check(false, "Only assigned driver requests extras"); } catch (UnauthorizedAccessException) { Check(true, "Only assigned driver requests extras"); }

Check(!ScreenRules.IsDriverPast(new Trip { Status = "Confirmed", PickupAt = 1 }), "Driver Upcoming retains unstarted past-dated reservations like the web");
Check(ScreenRules.IsDriverPast(new Trip { Status = "No-show", PickupAt = long.MaxValue }), "Driver Past includes No-show regardless of pickup date like the web");
var routeSamples = new List<GpsPoint> { new(61000,11,21,5),new(1000,10,20,5),new(1000,10,20,5),new(2000,double.NaN,20,5),new(3000,91,20,5) };
var orderedRoute=RoutePlayback.Normalize(routeSamples);
Check(orderedRoute.Count==2&&orderedRoute[0].CapturedAt==1000,"Route history rejects invalid fixes, sorts and removes exact duplicates");
Check(RoutePlayback.At(orderedRoute,0)==orderedRoute[0]&&RoutePlayback.At(orderedRoute,999999)==orderedRoute[^1],"Playback clamps start and end");
var midpoint=RoutePlayback.At(orderedRoute,31000);
Check(Math.Abs(midpoint.Latitude-10.5)<.0001&&Math.Abs(midpoint.Longitude-20.5)<.0001,"Playback follows elapsed timestamp, not point index");
Check(RoutePlayback.At([new(1000,10,20,5),new(200000,11,21,5)],100000).Latitude==10,"GPS outages are not filled with invented movement");
Check(Math.Abs(RoutePlayback.At([new(1000,0,179,5),new(2000,0,-179,5)],1500).Longitude)==180,"Playback crosses dateline by shortest route");
Check(RoutePlayback.InDateRange(new Trip{Date="2026-11-09"},new(2026,11,9),new(2026,11,9))&&!RoutePlayback.InDateRange(new Trip{Date="invalid"},DateTime.MinValue,DateTime.MaxValue),"History date filter is inclusive and ignores invalid dates");
int routePageCount=0;
var pagedRouteApi=new ApiRepository(new HttpClient(new FakeHandler(r=>{
    routePageCount++;Check(routePageCount==1||r.RequestUri!.Query.Contains("after=1000"),"History cursor is preserved on next page");
    return Task.FromResult(Response(HttpStatusCode.OK,routePageCount==1?"{\"points\":[{\"capturedAt\":1000,\"latitude\":10,\"longitude\":20,\"accuracy\":5}],\"nextAfter\":1000}":"{\"points\":[{\"capturedAt\":2000,\"latitude\":11,\"longitude\":21,\"accuracy\":5}],\"nextAfter\":null}"));
})){BaseAddress=new Uri("https://example.test/")},new StaticTokens());
var fullRoute=await new TrackingArchiveService(pagedRouteApi).LocationsAsync("QA trip");
Check(routePageCount==2&&fullRoute.Count==2,"History loads every page rather than showing a truncated route");
if(Environment.GetEnvironmentVariable("LD_PRICING_FIXTURE") is {} pricingPath)
{
    var livePricing=JsonSerializer.Deserialize<TierPricing>(await File.ReadAllTextAsync(pricingPath),ApiRepository.Json)!;
    var three=livePricing.Quote("Hourly","SUV",3);
    Check(three.Total==decimal.Round(three.Rate*3,2,MidpointRounding.AwayFromZero),"Live company hourly fare matches web rate times 3 booked hours");
    Check(new Trip{TripType="Hourly",From="Airport",To="As directed"}.HasDropoff==false,"Hourly cards omit drop-off like the chauffeur web portal");
}
Check(!ScreenRules.IsPast(new Trip{Status="Unassigned",PickupAt=1},screenNow),"Overdue unassigned reservations remain available to dispatch");
Check(ScreenRules.IsPast(new Trip{Status="No-show",PickupAt=long.MaxValue},screenNow),"No-show belongs in Past for admin as well as driver");
Console.WriteLine($"{passed} checks passed.");
await FinanceChecks.RunAsync();
await LiveChecks.RunAsync();
await WriteQaChecks.RunAsync();
await PushQaChecks.RunAsync();
static HttpResponseMessage Response(HttpStatusCode code, string json) => new(code) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };
sealed class FakeHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handle) : HttpMessageHandler { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct) => handle(r); }
sealed class MemoryStore : ISessionStore
{
 public TokenPair? Pair; public Task<TokenPair?> LoadAsync() => Task.FromResult(Pair); public Task SaveAsync(TokenPair pair) { Pair = pair; return Task.CompletedTask; } public Task ClearAsync() { Pair = null; return Task.CompletedTask; }
}
sealed class MemoryLoginStorage : ILoginMemoryStorage
{
    public string? Value;
    public Task<string?> ReadAsync() => Task.FromResult(Value);
    public async Task WriteAsync(string value) { await Task.Delay(10); Value = value; }
    public Task RemoveAsync() { Value = null; return Task.CompletedTask; }
}
sealed class StaticTokens : IAccessTokenProvider
{
 public int Refreshes; public Task<string> GetTokenAsync(CancellationToken ct = default) => Task.FromResult("token"); public Task<string> RefreshAfterRejectionAsync(string r, CancellationToken ct = default) { Refreshes++; return Task.FromResult("fresh"); }
}
