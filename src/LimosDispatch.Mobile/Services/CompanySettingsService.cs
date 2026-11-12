using System.Text.Json;
using LimosDispatch.Core.GenericRepository;
namespace LimosDispatch.Mobile.Services;
public sealed class CompanySettingsService(IGenericRepository api, DispatchService dispatch)
{
    public ImageSource? Logo { get; private set; }
    public static string Currency { get; private set; } = "";
    public async Task LoadAsync(UserProfile user)
    {
        Currency = "";
        Logo = null;
        try
        {
            if (user.IsDriver)
            {
                var company = (await dispatch.StatementAsync(DateTime.Today.ToString("yyyy-MM"))).Company;
                Currency = company.Currency;
                Logo = ReadLogo(company.Logo);
            }
            else
            {
                var settings = await api.GetAsync<JsonElement>("api/data/settings");
                if (settings.TryGetProperty("currency", out var currency)) Currency = currency.GetString() ?? "";
                if (settings.TryGetProperty("logo", out var logo)) Logo = ReadLogo(logo);
            }
        }
        catch (Exception ex) { await LimosDispatch.Core.Helpers.UserFeedback.ReportAsync(ex); }
        if (Logo is null && string.Equals(user.Company, "lux", StringComparison.OrdinalIgnoreCase))
        {
            // Public logo from the booking site linked to this tenant.
            Logo = ImageSource.FromUri(new Uri("https://yara.engprosoft.net/wp-content/themes/vip-luxury/assets/logo.png"));
        }

        if (Currency.Length != 3 || !Currency.All(char.IsLetter)) Currency = "";
        Currency = Currency.ToUpperInvariant();
    }

    private static ImageSource? ReadLogo(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String || value.GetString() is not { } logo)
            return null;

        if (logo.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase) && logo.Length < 3_000_000)
        {
            var comma = logo.IndexOf(',');
            if (comma > 0 && logo[..comma].EndsWith(";base64", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var bytes = Convert.FromBase64String(logo[(comma + 1)..]);
                    return ImageSource.FromStream(() => new MemoryStream(bytes, writable: false));
                }
                catch (FormatException) { return null; }
            }
        }
        return Uri.TryCreate(logo, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
            ? ImageSource.FromUri(uri) : null;
    }
}
