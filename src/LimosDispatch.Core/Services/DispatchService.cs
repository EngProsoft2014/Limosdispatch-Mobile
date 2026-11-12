using System.Text.Json;
using LimosDispatch.Core.Constants;
using LimosDispatch.Core.Models;
using LimosDispatch.Core.GenericRepository;
using LimosDispatch.Core.Helpers;
namespace LimosDispatch.Core.Services;
public sealed class DispatchService(IGenericRepository api, AuthService auth)
{
    private UserProfile User => auth.User ?? throw new UnauthorizedAccessException();
    public Task<TripList> TripsAsync(bool personal = false, CancellationToken ct = default) => api.GetAsync<TripList>(personal || !User.CanDispatch ? ApiRoutes.Portal : ApiRoutes.Trips, ct);
    public Task<Trip> TripAsync(string id, CancellationToken ct = default) => LoadTrip(id, ct);
    public Task<DriverJourney> DriverJourneyAsync(string id, CancellationToken ct = default) =>
        api.GetAsync<DriverJourney>($"api/driver/trip?id={Uri.EscapeDataString(id)}", ct);
    public async Task RequestExtraAsync(Trip trip, TripExtraRequest request, CancellationToken ct = default)
    {
        if (!User.IsDriver || User.DriverId != trip.DriverId) throw new UnauthorizedAccessException("Only the assigned chauffeur can request extras.");
        TripWorkflow.ValidateExtra(trip, request);
        await api.PostAsync<JsonElement>("api/driver/trip/extras", request, ct);
    }
    private async Task<Trip> LoadTrip(string id, CancellationToken ct)
    {
        var json = await api.GetAsync<JsonElement>($"api/driver/trip?id={Uri.EscapeDataString(id)}", ct);
        var value = json.TryGetProperty("trip", out var nested) ? nested : json;
        return value.Deserialize<Trip>(ApiRepository.Json) ?? throw new JsonException("Missing trip");
    }
    public async Task<MileageReminder?> ChangeStatusAsync(Trip trip, string next, CancellationToken ct = default, GpsPoint? stageLocation = null)
    {
        if (!User.IsDriver || User.DriverId != trip.DriverId) throw new InvalidOperationException("Only the assigned chauffeur can update this journey.");
        TripWorkflow.ValidateTransition(trip, next);
        var request = stageLocation is null
            ? (object)new { tripId = trip.Id, status = next }
            : new { tripId = trip.Id, status = next, stageLocation };
        var response = await api.PostAsync<JsonElement>(ApiRoutes.Status, request, ct);
        return next == "Completed" && response.ValueKind == JsonValueKind.Object
            && response.TryGetProperty("mileageReminder", out var reminder) && reminder.ValueKind == JsonValueKind.Object
            ? reminder.Deserialize<MileageReminder>(ApiRepository.Json)
            : null;
    }
    public async Task AssignAsync(Trip trip, Driver driver, string vehicle, CancellationToken ct = default)
    {
        if (!User.CanAssign) throw new UnauthorizedAccessException("Reservation edit permission is required.");
        if (trip.IsActive || trip.Status is "Completed" or "Cancelled") throw new InvalidOperationException("This journey is locked for assignment.");
        if (string.IsNullOrEmpty(driver.TrackingId)) throw new InvalidOperationException("This driver needs a configured login account before assignment.");
        await api.PostAsync<JsonElement>(ApiRoutes.Assign, new { id = trip.Id, driverId = driver.TrackingId, vehicle, trip.PickupAt, trip.Timezone }, ct);
    }
    public Task<Records<Trip>> TripRecordsAsync(CancellationToken ct = default) => api.GetAsync<Records<Trip>>(ApiRoutes.Record("trips"), ct);
    public async Task<Trip> CreateTripAsync(Trip trip, long version, CancellationToken ct = default)
    {
        if (!User.CanCreateTrip) throw new UnauthorizedAccessException("Reservation add permission is required.");
        if (string.IsNullOrEmpty(trip.ClientRequestId)) trip.ClientRequestId = trip.Id;
        if (string.IsNullOrEmpty(trip.ClientRequestId)) throw new InvalidOperationException("A draft request identifier is required.");
        var before = await TripRecordsAsync(ct);
        var existing = before.Rows.FirstOrDefault(t => t.ClientRequestId == trip.ClientRequestId);
        if (existing is not null) return existing;
        if (before.Version != version) throw new ApiException(System.Net.HttpStatusCode.Conflict, "Data changed on the server. Review and save again.");
        // Extras are server-owned and now rejected even as an empty array on create.
        var record = JsonSerializer.SerializeToElement(trip, ApiRepository.Json).EnumerateObject()
            .Where(p => p.Name is not ("extras" or "history")).ToDictionary(p => p.Name, p => p.Value.Clone());
        var result = await api.PostAsync<Records<Trip>>(ApiRoutes.Record("trips"), new RecordInput<Dictionary<string, JsonElement>>(version, record), ct);
        return result.Rows.SingleOrDefault(t => t.ClientRequestId == trip.ClientRequestId)
            ?? throw new InvalidOperationException("The journey was saved, but its server identifier could not be read. Refresh before trying again.");
    }
    public Task<List<Driver>> DriversAsync(CancellationToken ct = default) => api.GetAsync<List<Driver>>("api/data/drivers", ct);
    public Task<Records<Expense>> ExpensesAsync(CancellationToken ct = default) => api.GetAsync<Records<Expense>>(ApiRoutes.Record("expenses"), ct);
    public async Task<Expense> SaveExpenseAsync(Expense expense, long version, CancellationToken ct = default, Expense? original = null)
    {
        if (!User.Can("Expenses", original is null ? "Add" : "Edit")) throw new UnauthorizedAccessException("Your account does not have permission for this action.");
        if (!DateTime.TryParseExact(expense.Date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var expenseDate)) throw new InvalidOperationException("Select a valid expense date.");
        LimosDispatch.Core.Helpers.ScreenRules.ValidateExpenseDate(expenseDate, DateTime.Today);
        if (expense.Amount <= 0 || string.IsNullOrWhiteSpace(expense.Name) || string.IsNullOrWhiteSpace(expense.Category)) throw new InvalidOperationException("Enter a description, category and positive amount.");
        if(original is not null)ExpenseAccess.Demand(User,original.Driver);
        if (expense.Receipt is not null) LimosDispatch.Core.Helpers.ReceiptFiles.Read(expense.Receipt);
        if(expense.Scope is not("General" or "Vehicle")||expense.Scope=="Vehicle"&&string.IsNullOrWhiteSpace(expense.Vehicle))throw new InvalidOperationException("Select the linked vehicle for vehicle expenses.");
        if(expense.CostKind is not("Operating expense" or "Driver payroll payment" or "Other staff salary"))throw new InvalidOperationException("Choose a valid cost classification.");
        if(expense.CostKind=="Driver payroll payment"&&string.IsNullOrWhiteSpace(expense.Driver))throw new InvalidOperationException("Select the driver for a payroll payment.");
        if(expense.Scope=="General")expense.Vehicle="";
        if (User.IsDriver && !User.CanDispatch) expense.Driver = User.Name;
        if (string.IsNullOrEmpty(expense.ClientRequestId)) expense.ClientRequestId = expense.Id;
        if (string.IsNullOrEmpty(expense.ClientRequestId)) throw new InvalidOperationException("A draft request identifier is required.");
        var before = await ExpensesAsync(ct);
        if(original is not null)
        {
            var current=before.Rows.FirstOrDefault(e=>e.Id==original.Id)??throw new InvalidOperationException("This expense is no longer available.");
            if(before.Version!=version&&!FinanceService.Equal(JsonSerializer.SerializeToElement(current,ApiRepository.Json),JsonSerializer.SerializeToElement(original,ApiRepository.Json)))throw new ApiException(System.Net.HttpStatusCode.Conflict,"Data changed on the server. Refresh and review before saving again.");
            var updated=await api.PutAsync<Records<Expense>>(ApiRoutes.Record("expenses")+"/"+Uri.EscapeDataString(original.Id),new RecordInput<Expense>(before.Version,expense),ct);
            return updated.Rows.Single(e=>e.Id==original.Id);
        }
        var existing = before.Rows.FirstOrDefault(e => e.ClientRequestId == expense.ClientRequestId);
        if (existing is not null) return existing;
        if (before.Version != version) throw new ApiException(System.Net.HttpStatusCode.Conflict, "Data changed on the server. Review and save again.");
        var result = await api.PostAsync<Records<Expense>>(ApiRoutes.Record("expenses"), new RecordInput<Expense>(version, expense), ct);
        return result.Rows.SingleOrDefault(e => e.ClientRequestId == expense.ClientRequestId)
            ?? throw new InvalidOperationException("The expense was saved, but its server identifier could not be read. Refresh before trying again.");
    }
    public Task<EarningsEnvelope> StatementAsync(string month, string? payrollId = null, CancellationToken ct = default) =>
        api.GetAsync<EarningsEnvelope>($"api/payroll/statement?month={Uri.EscapeDataString(month)}" + (string.IsNullOrEmpty(payrollId) ? "" : $"&driverId={Uri.EscapeDataString(payrollId)}"), ct);
    public Task<TrackingEnvelope> TrackingAsync(CancellationToken ct = default) => api.GetAsync<TrackingEnvelope>(ApiRoutes.Tracking, ct);
    public Task<JsonElement> LocationsAsync(string tripId, List<GpsPoint> points, CancellationToken ct = default) =>
        api.PostAsync<JsonElement>($"api/driver/trips/{Uri.EscapeDataString(tripId)}/locations", new LocationBatch(points), ct);
}
