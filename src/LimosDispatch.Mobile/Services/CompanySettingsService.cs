using System.Text.Json;
using LimosDispatch.Core.GenericRepository;
namespace LimosDispatch.Mobile.Services;
public sealed class CompanySettingsService(IGenericRepository api, DispatchService dispatch)
{
    public static string Currency { get; private set; } = "";
    public async Task LoadAsync(UserProfile user)
    {
        Currency = "";
        try
        {
            if (user.IsDriver) Currency = (await dispatch.StatementAsync(DateTime.Today.ToString("yyyy-MM"))).Company.Currency;
            else
            {
                var settings = await api.GetAsync<JsonElement>("api/data/settings");
                if (settings.TryGetProperty("currency", out var currency)) Currency = currency.GetString() ?? "";
            }
        }
        catch (Exception ex) { await LimosDispatch.Core.Helpers.UserFeedback.ReportAsync(ex); }
        if (Currency.Length != 3 || !Currency.All(char.IsLetter)) Currency = "";
        Currency = Currency.ToUpperInvariant();
    }
}
