using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LimosDispatch.Core.Constants;
using LimosDispatch.Core.GenericRepository;
using LimosDispatch.Core.Models;
using LimosDispatch.Core.Services;

// Explicit MOBILE QA integration test. Never signs out all of the user's account sessions.
public static class PushQaChecks
{
    public static async Task RunAsync()
    {
        var mode = Environment.GetEnvironmentVariable("LD_PUSH_QA");
        if (string.IsNullOrEmpty(mode)) return;
        var statePath = Environment.GetEnvironmentVariable("LD_PUSH_QA_STATE") ?? throw new Exception("Missing QA state path");
        var company = Need("LD_COMPANY");
        if (company != "lux") throw new Exception("This QA flow is scoped to the authorized lux tenant.");
        using var adminHttp = new HttpClient { BaseAddress = new(ApiRoutes.BaseUrl) };
        using var driverHttp = new HttpClient { BaseAddress = new(ApiRoutes.BaseUrl) };
        var adminAuth = new AuthService(adminHttp, new Store()); var adminApi = new ApiRepository(adminHttp, adminAuth);
        await adminAuth.LoginAsync(company, Need("LD_ADMIN_EMAIL"), Need("LD_ADMIN_PASSWORD"));
        adminAuth.SetUser(await adminApi.GetAsync<UserProfile>(ApiRoutes.Me));
        var admin = new DispatchService(adminApi, adminAuth);
        if (mode == "prepare")
        {
            if (File.Exists(statePath)) throw new Exception("Finish cleanup of the previous push QA record first.");
            var request = "MOBILE-QA-PUSH-" + Guid.NewGuid();
            var state = new State { RequestId = request };
            await File.WriteAllTextAsync(statePath, JsonSerializer.Serialize(state));
            var settings = await adminApi.GetAsync<JsonElement>("api/data/settings");
            var zone = settings.GetProperty("timezone").GetString()!;
            var pickup = DateTimeOffset.UtcNow.AddMinutes(10);
            var local = TimeZoneInfo.ConvertTime(pickup, TimeZoneInfo.FindSystemTimeZoneById(zone));
            var trip = new Trip { Id = request, ClientRequestId = request, Name = "MOBILE QA Push notifications", Phone = "2025550100", From = "MOBILE QA test pickup", To = "MOBILE QA test destination", Date = local.ToString("yyyy-MM-dd"), Time = local.ToString("HH:mm"), PickupAt = pickup.ToUnixTimeMilliseconds(), Timezone = zone, Price = 0.01m, People = 1, DistanceMiles = 1, TripType = "Distance", VehicleType = "SUV", Service = "MOBILE QA", Notes = "Synthetic push test. No customer payment. Delete this exact QA record after testing.", Status = "Unassigned" };
            var saved = await admin.CreateTripAsync(trip, (await admin.TripRecordsAsync()).Version);
            state.TripId = saved.Id; await File.WriteAllTextAsync(statePath, JsonSerializer.Serialize(state));
            var driverAuth = new AuthService(driverHttp, new Store()); var driverApi = new ApiRepository(driverHttp, driverAuth);
            await driverAuth.LoginAsync(company, Need("LD_DRIVER_EMAIL"), Need("LD_DRIVER_PASSWORD"));
            driverAuth.SetUser(await driverApi.GetAsync<UserProfile>(ApiRoutes.Me));
            var chauffeur = (await admin.DriversAsync()).Single(d => d.TrackingId == driverAuth.User!.DriverId);
            await admin.AssignAsync(saved, chauffeur, chauffeur.Vehicle);
            Console.WriteLine("PUSH QA: created and assigned exact MOBILE QA trip " + saved.Id);
            var inbox = await new PushApi(driverApi).InboxAsync();
            Console.WriteLine("PUSH QA: assignment inbox event=" + inbox.Items.Any(n => n.TripId == saved.Id && n.Type == "ride_assigned"));
            return;
        }
        var existingState = JsonSerializer.Deserialize<State>(await File.ReadAllTextAsync(statePath))!;
        var records = await admin.TripRecordsAsync();
        var own = records.Rows.SingleOrDefault(t => t.ClientRequestId == existingState.RequestId);
        if (own is null) { if (mode == "cleanup") { File.Delete(statePath); Console.WriteLine("PUSH QA: own record already absent"); return; } throw new Exception("QA trip missing"); }
        if (own.Name != "MOBILE QA Push notifications" || (!string.IsNullOrEmpty(existingState.TripId) && own.Id != existingState.TripId)) throw new Exception("Refusing to modify a non-QA record");
        if (mode is "cleanup" or "archive")
        {
            foreach (var n in (await new PushApi(adminApi).InboxAsync()).Items.Where(n => n.TripId == own.Id && !n.IsRead)) await new PushApi(adminApi).ReadAsync(n.Id);
            var cleanupDriverAuth = new AuthService(driverHttp, new Store()); var cleanupDriverApi = new ApiRepository(driverHttp, cleanupDriverAuth);
            await cleanupDriverAuth.LoginAsync(company, Need("LD_DRIVER_EMAIL"), Need("LD_DRIVER_PASSWORD")); cleanupDriverAuth.SetUser(await cleanupDriverApi.GetAsync<UserProfile>(ApiRoutes.Me));
            foreach (var n in (await new PushApi(cleanupDriverApi).InboxAsync()).Items.Where(n => n.TripId == own.Id && !n.IsRead)) await new PushApi(cleanupDriverApi).ReadAsync(n.Id);
            var cleanupTrip = await new PushApi(adminApi).TripAsync(own.Id);
            if (cleanupTrip.Extras.Count > 0)
            {
                foreach (var extra in cleanupTrip.Extras.Where(e => e.Status == "Pending" && e.Description.StartsWith("MOBILE QA push delivery test only", StringComparison.Ordinal)))
                    await adminApi.PostAsync<JsonElement>("api/trips/extras/review", new { tripId = own.Id, extraId = extra.Id, decision = "Rejected", amount = 0, customerConfirmed = false, note = "MOBILE QA cleanup: synthetic extra, no customer charge." });
                var verified = await new PushApi(adminApi).TripAsync(own.Id);
                Console.WriteLine("PUSH QA RETAINED: server audit protection prevents delete/cancel of started ride " + own.Id + "; status=" + verified.Status + "; extra=" + string.Join(",", verified.Extras.Select(e => e.Status)));
                Console.WriteLine("PUSH QA: no charge approved; do not complete a synthetic ride to bypass cleanup restrictions. Web developer must archive this exact test record.");
                return;
            }
            using var request = new HttpRequestMessage(HttpMethod.Delete, ApiRoutes.Record("trips") + "/" + Uri.EscapeDataString(own.Id)) { Content = JsonContent.Create(new { version = records.Version, record = new { } }) };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await adminAuth.GetTokenAsync());
            using var response = await adminHttp.SendAsync(request); await ApiRepository.Read<JsonElement>(response, default);
            if ((await admin.TripRecordsAsync()).Rows.Any(t => t.Id == own.Id)) throw new Exception("QA trip was not removed");
            File.Delete(statePath); Console.WriteLine("PUSH QA CLEANUP: exact synthetic trip removed"); return;
        }
        if (mode != "extra") throw new Exception("Unsupported QA mode");
        var chauffeurAuth = new AuthService(driverHttp, new Store()); var chauffeurApi = new ApiRepository(driverHttp, chauffeurAuth);
        await chauffeurAuth.LoginAsync(company, Need("LD_DRIVER_EMAIL"), Need("LD_DRIVER_PASSWORD")); chauffeurAuth.SetUser(await chauffeurApi.GetAsync<UserProfile>(ApiRoutes.Me));
        var driverService = new DispatchService(chauffeurApi, chauffeurAuth);
        var current = await driverService.TripAsync(own.Id);
        if (current.Status == "Confirmed") await driverService.ChangeStatusAsync(current, "Enroute");
        await File.WriteAllTextAsync(statePath, JsonSerializer.Serialize(existingState));
        await chauffeurApi.PostAsync<JsonElement>("api/driver/trip/extras", new { tripId = own.Id, requestId = existingState.ExtraRequestId, type = "Time", quantity = 1m, description = "MOBILE QA push delivery test only; no approval or charge requested." });
        var adminInbox = await new PushApi(adminApi).InboxAsync();
        Console.WriteLine("PUSH QA: admin extra inbox event=" + adminInbox.Items.Any(n => n.TripId == own.Id && n.Type == "trip_extra_requested"));
    }
    private static string Need(string key) => Environment.GetEnvironmentVariable(key) ?? throw new Exception("Missing " + key);
    private sealed class State { public string RequestId { get; set; } = ""; public string TripId { get; set; } = ""; public string ExtraRequestId { get; set; } = Guid.NewGuid().ToString(); }
    private sealed class Store : ISessionStore { private TokenPair? value; public Task<TokenPair?> LoadAsync() => Task.FromResult(value); public Task SaveAsync(TokenPair pair) { value = pair; return Task.CompletedTask; } public Task ClearAsync() { value = null; return Task.CompletedTask; } }
}
