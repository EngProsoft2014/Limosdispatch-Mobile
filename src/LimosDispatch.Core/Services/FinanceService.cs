using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using LimosDispatch.Core.Models;
using LimosDispatch.Core.GenericRepository;
namespace LimosDispatch.Core.Services;
public sealed class FinanceService(IGenericRepository api,AuthService auth)
{
    private UserProfile User=>auth.User??throw new UnauthorizedAccessException();
    public void Demand(string module,string action="View"){if(!User.Can(module,action))throw new UnauthorizedAccessException("Your account does not have permission for this action.");}
    public Task<WorkspaceEnvelope> WorkspaceAsync(CancellationToken ct=default)=>api.GetAsync<WorkspaceEnvelope>("api/workspace",ct);
    public static T Read<T>(WorkspaceEnvelope w,string key,T fallback)=>w.Data.TryGetValue(key,out var v)&&v.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined)?v.Deserialize<T>(ApiRepository.Json)??fallback:fallback;
    public async Task<ExpenseLookups> ExpenseChoicesAsync(CancellationToken ct=default){Demand("Expenses");var w=await WorkspaceAsync(ct);var x=Read(w,"expenseLookups",new ExpenseLookups());if(User.IsAdmin){if(x.Categories.Count==0)x.Categories=Read(w,"expenseCategories",new List<string>());if(x.Vehicles.Count==0)x.Vehicles=Read(w,"fleet",new List<FleetVehicle>());if(x.Drivers.Count==0)x.Drivers=Read(w,"drivers",new List<Driver>());}return x;}
    public Task<Records<JsonElement>> RecordsAsync(string module,CancellationToken ct=default)=>api.GetAsync<Records<JsonElement>>("api/records/"+Uri.EscapeDataString(module),ct);
    public static bool Equal(JsonElement a,JsonElement b)=>JsonNode.DeepEquals(JsonNode.Parse(a.GetRawText()),JsonNode.Parse(b.GetRawText()));
    public static string Id(JsonElement row)=>row.TryGetProperty("id",out var id)?id.GetString()??"":"";
    public async Task UpdateAsync(string module,string id,JsonElement original,Dictionary<string,object?> patch,long version,string permission)
    {
        Demand(permission,"Edit");var current=await RecordsAsync(module);var row=current.Rows.FirstOrDefault(r=>Id(r)==id);
        if(row.ValueKind!=JsonValueKind.Object)throw new InvalidOperationException("This record is no longer available.");
        if(module=="expenses")LimosDispatch.Core.Helpers.ExpenseAccess.Demand(User,row.TryGetProperty("driver",out var expenseOwner)?expenseOwner.GetString():null);
        if(!Equal(row,original))throw new ApiException(HttpStatusCode.Conflict,"Data changed on the server. Refresh and review before saving again.");
        if(module=="trips"&&patch.ContainsKey("paid")&&row.TryGetProperty("paymentMethod",out var method)&&method.GetString()=="Credit Card")throw new InvalidOperationException("Card payment records must be managed through the web payment flow.");
        if(module=="trips"&&patch.Keys.Any(k=>k!="paid")){var trip=row.Deserialize<Trip>(ApiRepository.Json)!;if(trip.IsActive||trip.Status is "Completed" or "Cancelled" or "No-show")throw new InvalidOperationException("This journey is locked. Refresh its current status.");}
        var merged=row.EnumerateObject().ToDictionary(p=>p.Name,p=>(object?)p.Value.Clone());foreach(var p in patch)merged[p.Key]=p.Value;
        await api.PutAsync<JsonElement>($"api/records/{Uri.EscapeDataString(module)}/{Uri.EscapeDataString(id)}",new{version=current.Version,record=merged});
    }
    public async Task DeleteAsync(string module,string id,JsonElement original,long version,string permission)
    {
        Demand(permission,"Delete");var current=await RecordsAsync(module);var row=current.Rows.FirstOrDefault(r=>Id(r)==id);
        if(row.ValueKind!=JsonValueKind.Object)return;
        if(module=="expenses")LimosDispatch.Core.Helpers.ExpenseAccess.Demand(User,row.TryGetProperty("driver",out var expenseOwner)?expenseOwner.GetString():null);
        if(!Equal(row,original))throw new ApiException(HttpStatusCode.Conflict,"Data changed on the server. Refresh and review before saving again.");
        await api.DeleteAsync<JsonElement>($"api/records/{Uri.EscapeDataString(module)}/{Uri.EscapeDataString(id)}",new{version=current.Version,record=new{}});
    }
    public async Task<List<string>> CategoryAsync(string action,string name="",string oldName="",IEnumerable<string>? names=null)
    {
        Demand("Expense categories",action=="Rename"?"Edit":action=="Delete"?"Delete":"Add");
        var values=(names??[name]).Select(n=>System.Text.RegularExpressions.Regex.Replace(n.Trim(),@"\s+"," ")).Where(n=>n.Length>0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if(action!="Delete"&&(values.Length==0||values.Any(n=>n.Length>100||n.IndexOfAny(['<','>'])>=0)))throw new InvalidOperationException("Use plain category names of up to 100 characters.");
        var result=await api.PostAsync<CategoryResult>("api/expense-categories",new{action,name=values.FirstOrDefault()??name,oldName,names=values});return result.Categories;
    }
    public Task<FinancialReport> ReportAsync(DateTime from,DateTime to,CancellationToken ct=default)
    {Demand("Reports");if(from.Date>to.Date)throw new InvalidOperationException("Select a valid date range.");return api.GetAsync<FinancialReport>($"api/reports/profitability?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}",ct);}
    public async Task FinalizeAsync(Statement reviewed)
    {
        Demand("Payouts","Edit");if(reviewed.Finalized||reviewed.NeedsReview||reviewed.Rows.Count==0&&!reviewed.IsSalary)throw new InvalidOperationException("Resolve all review items before finalizing.");
        var fresh=await api.GetAsync<EarningsEnvelope>($"api/payroll/statement?month={Uri.EscapeDataString(reviewed.Month)}&driverId={Uri.EscapeDataString(reviewed.DriverId)}");
        if(fresh.Statement.Finalized||!Equal(JsonSerializer.SerializeToElement(reviewed,ApiRepository.Json),JsonSerializer.SerializeToElement(fresh.Statement,ApiRepository.Json)))throw new ApiException(HttpStatusCode.Conflict,"The statement changed. Refresh and review before finalizing.");
        var records=await RecordsAsync("driverPayStatements");if(records.Rows.Any(r=>r.TryGetProperty("driverId",out var d)&&d.GetString()==reviewed.DriverId&&r.TryGetProperty("month",out var m)&&m.GetString()==reviewed.Month))throw new InvalidOperationException("This statement has already been finalized. Refresh it.");var snapshot=JsonSerializer.SerializeToElement(reviewed,ApiRepository.Json).EnumerateObject().ToDictionary(p=>p.Name,p=>(object?)p.Value.Clone());
        snapshot["id"]=$"PAYSTAT-{reviewed.DriverId}-{reviewed.Month}";snapshot["finalizedAt"]=DateTimeOffset.UtcNow.ToString("O");
        await api.PostAsync<JsonElement>("api/records/driverPayStatements",new{version=records.Version,record=snapshot});
    }
    public async Task ReviewExtraAsync(ExtraReviewRequest review)
    {
        Demand("Reservations","Edit");if(review.Decision is not("Approved" or "Rejected")||review.Amount<0||review.Amount>1000000||review.Note.Length>1000)throw new InvalidOperationException("Review the extra request and amount.");
        if(review.Decision=="Approved"&&!review.CustomerConfirmed)throw new InvalidOperationException("Confirm the charge with the customer before approval.");
        var response=await api.GetAsync<PushApi.TripResponse>("api/trips/"+Uri.EscapeDataString(review.TripId));
        if(response.Trip?.Extras.Any(e=>e.Id==review.ExtraId&&e.Status=="Pending")!=true)throw new InvalidOperationException("This extra has already been reviewed. Refresh the journey.");
        await api.PostAsync<JsonElement>("api/trips/extras/review",review.Decision=="Rejected"?review with{Amount=0,CustomerConfirmed=false}:review);
    }
    public async Task CancelTripAsync(string id)
    {
        Demand("Reservations","Edit");var t=(await api.GetAsync<PushApi.TripResponse>("api/trips/"+Uri.EscapeDataString(id))).Trip??throw new InvalidOperationException("Journey no longer exists.");
        if(t.IsActive||t.Status is "Completed" or "Cancelled" or "No-show")throw new InvalidOperationException("This journey cannot be cancelled in its current state.");
        await api.PostAsync<JsonElement>($"api/reservations/{Uri.EscapeDataString(id)}/cancel",new{});
    }
}
