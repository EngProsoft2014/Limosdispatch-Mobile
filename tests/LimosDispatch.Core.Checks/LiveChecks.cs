using LimosDispatch.Core.Constants;
using LimosDispatch.Core.GenericRepository;
using LimosDispatch.Core.Models;
using LimosDispatch.Core.Services;
public static class LiveChecks
{
    // Optional read-only integration run. Credentials are supplied only in process environment.
    public static async Task RunAsync()
    {
        var company = Environment.GetEnvironmentVariable("LD_COMPANY");
        var email = Environment.GetEnvironmentVariable("LD_EMAIL");
        var password = Environment.GetEnvironmentVariable("LD_PASSWORD");
        if (string.IsNullOrEmpty(company) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password)) return;
        var store = new EphemeralStore();
        var client = new HttpClient { BaseAddress = new(ApiRoutes.BaseUrl) };
        var auth = new AuthService(client, store);
        var api = new ApiRepository(client, auth);
        try
        {
            await auth.LoginAsync(company, email, password);
            var user = await api.GetAsync<UserProfile>(ApiRoutes.Me); auth.SetUser(user);
            var service = new DispatchService(api, auth);
            var trips = await service.TripsAsync();
            Console.WriteLine($"LIVE {user.Role}: authenticated; {trips.Trips.Count} trips parsed");
            var expenses = await service.ExpensesAsync(); Console.WriteLine($"LIVE expense envelope parsed; version {expenses.Version}");
            if (user.IsDriver)
            {
                if (trips.Trips.Count > 0)
                {
                    var trip = await service.TripAsync(trips.Trips[0].Id);
                    if (string.IsNullOrEmpty(trip.Id)) throw new Exception("Detail envelope mismatch");
                    Console.WriteLine("LIVE assigned trip detail parsed");
                }
                var statement = await service.StatementAsync("2026-11");
                Console.WriteLine($"LIVE statement: {statement.Statement.Rows.Count} rows; total {statement.Statement.Total}");
            }
            else
            {
                var records = await service.TripRecordsAsync(); Console.WriteLine($"LIVE trip records: {records.Rows.Count} rows");
                var drivers = await service.DriversAsync();
                var settings = await api.GetAsync<System.Text.Json.JsonElement>("api/data/settings");
                if (!settings.TryGetProperty("currency", out _)) throw new Exception("Company currency metadata missing");
                foreach (var d in drivers.Where(d => !string.IsNullOrEmpty(d.PayrollId))) await service.StatementAsync("2026-11", d.PayrollId);
                await service.TrackingAsync(); Console.WriteLine("LIVE driver directory, payroll and tracking parsed");
            }
        }
        // The deployed logout endpoint revokes every device/session for the account.
        finally { await auth.ForgetSessionAsync(); client.Dispose(); }
    }
    private sealed class EphemeralStore : ISessionStore
    {
        private TokenPair? pair;
        public Task<TokenPair?> LoadAsync() => Task.FromResult(pair);
        public Task SaveAsync(TokenPair p) { pair = p; return Task.CompletedTask; }
        public Task ClearAsync() { pair = null; return Task.CompletedTask; }
    }
}
