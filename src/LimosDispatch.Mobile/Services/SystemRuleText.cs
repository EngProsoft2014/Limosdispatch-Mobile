using System.Text.RegularExpressions;
namespace LimosDispatch.Mobile.Services;

// Only known, generated calculation templates are translated. Unknown text is preserved.
internal static class SystemRuleText
{
    private const string Number = @"([0-9]+(?:\.[0-9]+)?)";
    internal static string Translate(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        var match = Regex.Match(value, "^Distance tier: fixed " + Number + "$", RegexOptions.CultureInvariant);
        if (match.Success) return LocalizationService.Format("Distance tier: fixed {0}", match.Groups[1].Value);
        match = Regex.Match(value, "^" + Number + " booked h \\+ " + Number + " approved min [x×] " + Number + " / h$", RegexOptions.CultureInvariant);
        if (match.Success) return LocalizationService.Format("{0} booked h + {1} approved min × {2} / h", match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value);
        return value;
    }
}
