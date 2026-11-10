using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LimosDispatch.Core.Constants;
using LimosDispatch.Core.GenericRepository;
using LimosDispatch.Core.Models;
using LimosDispatch.Core.Services;

// Explicit opt-in integration test. Only its own MOBILE QA record IDs are removed.
public static class WriteQaChecks
{
    public static async Task RunAsync()
    {
        if (Environment.GetEnvironmentVariable("LD_WRITE_QA") != "1") return;
        var company = Need("LD_COMPANY");
        using var adminHttp = new HttpClient { BaseAddress = new(ApiRoutes.BaseUrl) };
        using var driverHttp = new HttpClient { BaseAddress = new(ApiRoutes.BaseUrl) };
        var adminAuth = new AuthService(adminHttp, new MemoryStore());
        var driverAuth = new AuthService(driverHttp, new MemoryStore());
        var adminApi = new ApiRepository(adminHttp, adminAuth);
        var driverApi = new ApiRepository(driverHttp, driverAuth);
        var tripId = "LD-MOBILE-QA-" + Guid.NewGuid();
        var expenseId = Guid.NewGuid().ToString();
        var tripCreated = false; var expenseCreated = false;
        try
        {
            await adminAuth.LoginAsync(company, Need("LD_ADMIN_EMAIL"), Need("LD_ADMIN_PASSWORD"));
            adminAuth.SetUser(await adminApi.GetAsync<UserProfile>(ApiRoutes.Me));
            await driverAuth.LoginAsync(company, Need("LD_DRIVER_EMAIL"), Need("LD_DRIVER_PASSWORD"));
            driverAuth.SetUser(await driverApi.GetAsync<UserProfile>(ApiRoutes.Me));
            var admin = new DispatchService(adminApi, adminAuth);
            var driver = new DispatchService(driverApi, driverAuth);
            var settings = await adminApi.GetAsync<JsonElement>("api/data/settings");
            var zone = settings.GetProperty("timezone").GetString()!;
            var pickup = DateTimeOffset.UtcNow.AddMinutes(2);
            var local = TimeZoneInfo.ConvertTime(pickup, TimeZoneInfo.FindSystemTimeZoneById(zone));
            var statementBefore = (await driver.StatementAsync(local.ToString("yyyy-MM"))).Statement;
            if (statementBefore.Finalized) throw new Exception("QA cannot change a finalized payroll month.");
            var testTrip = new Trip { Id = tripId, Name = "MOBILE QA — automated workflow", Phone = "2025550100", From = "MOBILE QA · Houston test pickup", To = "MOBILE QA · Houston test drop-off", Date = local.ToString("yyyy-MM-dd"), Time = local.ToString("HH:mm"), PickupAt = pickup.ToUnixTimeMilliseconds(), Timezone = zone, Price = 0.01m, People = 1, DistanceMiles = 5, Hours = 1, Service = "MOBILE QA", TripType = "Distance", VehicleType = "SUV", Notes = "MOBILE QA synthetic test; not a real booking; removed after verification.", Status = "Unassigned" };
            var savedTrip = await admin.CreateTripAsync(testTrip, (await admin.TripRecordsAsync()).Version);
            tripId = savedTrip.Id;
            testTrip = savedTrip;
            tripCreated = true;
            if (!(await admin.TripRecordsAsync()).Rows.Any(t => t.Id == tripId)) throw new Exception("Trip create response did not persist the record.");
            Console.WriteLine("QA PASS: create and read unassigned trip " + tripId);
            var chauffeur = (await admin.DriversAsync()).Single(d => d.TrackingId == driverAuth.User!.DriverId);
            await admin.AssignAsync(testTrip, chauffeur, chauffeur.Vehicle);
            var assigned = await driver.TripAsync(tripId);
            if (assigned.Status != "Confirmed" || assigned.DriverId != chauffeur.TrackingId) throw new Exception("Assignment did not produce the expected driver/Confirmed state.");
            Console.WriteLine("QA PASS: assignment visible in chauffeur portal");
            foreach (var status in new[] { "Enroute", "Arrived", "Onboard", "Completed" })
            {
                if (status == "Completed")
                {
                    await driver.LocationsAsync(tripId, [new(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), 29.7604, -95.3698, 5)]);
                    var history = await adminApi.GetAsync<JsonElement>($"api/trips/{Uri.EscapeDataString(tripId)}/location-history");
                    if (!history.TryGetProperty("points", out var points) || points.GetArrayLength() == 0) throw new Exception("GPS history did not contain the synthetic test point.");
                    var tracking = await admin.TrackingAsync();
                    if (!tracking.Drivers.Any(d => d.Id == chauffeur.TrackingId && d.Location?.Sharing == true && Math.Abs(d.Latitude - 29.7604) < 0.00001)) throw new Exception("Live tracking did not contain the synthetic test location.");
                    Console.WriteLine("QA PASS: synthetic GPS batch, route history and live tracking");
                }
                await driver.ChangeStatusAsync(assigned, status);
                assigned = await driver.TripAsync(tripId);
                if (assigned.Status != status) throw new Exception("Trip status did not persist: " + status);
                Console.WriteLine("QA PASS: chauffeur stage " + status);
            }
            var categories = await adminApi.GetAsync<List<string>>("api/data/expenseCategories");
            var expense = new Expense { Id = expenseId, Name = "MOBILE QA — synthetic receipt", Category = categories.First(), Amount = 0.01m, Date = local.ToString("yyyy-MM-dd"), Driver = driverAuth.User!.Name, Notes = "MOBILE QA synthetic fixture, not a real expense; removed after verification.", Receipt = new Receipt { Name = "mobile-qa-fixture.png", Data = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aD1sAAAAASUVORK5CYII=" } };
            var savedExpense = await driver.SaveExpenseAsync(expense, (await driver.ExpensesAsync()).Version);
            expenseId = savedExpense.Id;
            expenseCreated = true;
            if (!(await admin.ExpensesAsync()).Rows.Any(e => e.Id == expenseId && e.Receipt is not null && e.Driver == driverAuth.User.Name)) throw new Exception("Driver expense/receipt was not visible to administrator.");
            Console.WriteLine("QA PASS: chauffeur expense + synthetic receipt visible to administrator");
            var statementAfter = (await driver.StatementAsync(local.ToString("yyyy-MM"))).Statement;
            if (!statementAfter.Rows.Any(r => r.Id == tripId)) throw new Exception("Completed QA trip was missing from the earnings statement.");
            Console.WriteLine("QA PASS: completed trip included in earnings statement");
        }
        finally
        {
            var cleanupErrors = new List<string>();
            if (expenseCreated) try { await RemoveOwnRecord(adminHttp, adminAuth, adminApi, "expenses", expenseId); } catch (Exception e) { cleanupErrors.Add("expense " + expenseId + ": " + e.Message); }
            if (tripCreated) try { await RemoveOwnRecord(adminHttp, adminAuth, adminApi, "trips", tripId); } catch (Exception e) { cleanupErrors.Add("trip " + tripId + ": " + e.Message); }
            await driverAuth.ForgetSessionAsync();
            await adminAuth.ForgetSessionAsync();
            if (cleanupErrors.Count > 0) throw new Exception("QA cleanup needs attention: " + string.Join("; ", cleanupErrors));
            Console.WriteLine("QA CLEANUP: own test records removed; test session memory cleared");
        }
    }
    private static async Task RemoveOwnRecord(HttpClient http, AuthService auth, IGenericRepository api, string module, string id)
    {
        var envelope = await api.GetAsync<Records<JsonElement>>(ApiRoutes.Record(module));
        var row = envelope.Rows.FirstOrDefault(r => r.GetProperty("id").GetString() == id);
        if (row.ValueKind == JsonValueKind.Undefined) return;
        if (!row.GetProperty("name").GetString()!.StartsWith("MOBILE QA", StringComparison.Ordinal)) throw new Exception("Refusing to remove a non-QA record.");
        using var request = new HttpRequestMessage(HttpMethod.Delete, ApiRoutes.Record(module) + "/" + Uri.EscapeDataString(id)) { Content = JsonContent.Create(new { version = envelope.Version, record = new { } }) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await auth.GetTokenAsync());
        using var response = await http.SendAsync(request);
        await ApiRepository.Read<JsonElement>(response, default);
        var check = await api.GetAsync<Records<JsonElement>>(ApiRoutes.Record(module));
        if (check.Rows.Any(r => r.GetProperty("id").GetString() == id)) throw new Exception("QA record still present after cleanup.");
    }
    private static string Need(string key) => Environment.GetEnvironmentVariable(key) ?? throw new Exception("Missing QA environment variable: " + key);
    private sealed class MemoryStore : ISessionStore
    {
        private TokenPair? pair;
        public Task<TokenPair?> LoadAsync() => Task.FromResult(pair);
        public Task SaveAsync(TokenPair value) { pair = value; return Task.CompletedTask; }
        public Task ClearAsync() { pair = null; return Task.CompletedTask; }
    }
}
