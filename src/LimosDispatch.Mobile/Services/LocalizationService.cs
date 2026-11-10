using System.Globalization;
using System.Resources;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
namespace LimosDispatch.Mobile.Services;
public sealed record LanguageOption(string Code, string Name);
public sealed class LocalizationService
{
    public static LocalizationService Instance { get; } = new();
    private readonly ResourceManager resources = new("LimosDispatch.Mobile.Resources.Localization.Strings", typeof(LocalizationService).Assembly);
    private readonly WeakEventManager events = new();
    private ResourceDictionary? dictionary;
    private bool applied;
    public static CultureInfo NumericCulture=>LimosDispatch.Core.Helpers.DisplayCulture.Numbers;
    public static CultureInfo Culture=>LimosDispatch.Core.Helpers.DisplayCulture.ForLanguage(Instance.Code);
    public string Code { get; private set; } = Preferences.Default.Get("app-language", "en");
    public IReadOnlyList<LanguageOption> Languages { get; } = [new("en", "English"), new("ar", "العربية"), new("es", "Español")];
    public event EventHandler Changed { add => events.AddEventHandler(value); remove => events.RemoveEventHandler(value); }
    public static string Key(string text)
    {
        var name = Regex.Replace(text, "[^A-Za-z0-9]+", "_").Trim('_');
        if (name.Length > 70) name = name[..70];
        return "Ui_" + name + "_" + Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant()[..6];
    }
    public static string Text(string? english) => string.IsNullOrEmpty(english) ? "" : Instance.resources.GetString(Key(english), CultureInfo.GetCultureInfo(Instance.Code)) ?? english;
    public static string Format(string template, params object?[] args) => string.Format(Culture, Text(template), args);
    public void Restore(ResourceDictionary target) { dictionary = target; Apply(Code); }
    public void Apply(string code)
    {
        if (!Languages.Any(l => l.Code == code)) code = "en";
        if (applied && Code == code) return;
        Code = code; Preferences.Default.Set("app-language", code);
        var culture = Culture;
        CultureInfo.CurrentCulture = culture; CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(code); CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.CurrentUICulture;
        if (dictionary is not null)
        {
            foreach (System.Collections.DictionaryEntry entry in resources.GetResourceSet(CultureInfo.InvariantCulture, true, true)!)
                dictionary[(string)entry.Key] = resources.GetString((string)entry.Key, CultureInfo.CurrentUICulture) ?? entry.Value!;
            dictionary["AppFlowDirection"] = code == "ar" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            dictionary["AppFontRegular"] = code == "ar" ? "ElMessiriRegular" : "OpenSansRegular";
            dictionary["AppFontSemibold"] = code == "ar" ? "ElMessiriBold" : "OpenSansSemibold";
            dictionary["FieldTextAlignment"] = code == "ar" ? TextAlignment.End : TextAlignment.Start;
            dictionary["LatinFieldTextAlignment"] = code == "ar" ? TextAlignment.End : TextAlignment.Start;
        }
        applied = true; events.HandleEvent(this, EventArgs.Empty, nameof(Changed));
    }
}
