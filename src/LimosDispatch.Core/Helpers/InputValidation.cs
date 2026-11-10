using System.Net.Mail;
namespace LimosDispatch.Core.Helpers;
public static class InputValidation
{
    public static string NormalizeDigits(string? text) => string.Concat((text ?? "").Select(c => char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.DecimalDigitNumber ? (char)('0' + (int)char.GetNumericValue(c)) : c));
    // Numeric form fields accept decimal separators, never interpret one as a grouping separator.
    public static bool TryDecimal(string? text,System.Globalization.CultureInfo culture,out decimal value)
    {
        var input=NormalizeDigits(text).Replace("٬","").Replace('٫','.');
        const System.Globalization.NumberStyles style=System.Globalization.NumberStyles.AllowDecimalPoint|System.Globalization.NumberStyles.AllowLeadingSign|System.Globalization.NumberStyles.AllowLeadingWhite|System.Globalization.NumberStyles.AllowTrailingWhite;
        return decimal.TryParse(input,style,culture,out value)||decimal.TryParse(input,style,System.Globalization.CultureInfo.InvariantCulture,out value);
    }
    public static string NameError(string? value)
    {
        value = value?.Trim() ?? "";
        return value.Length == 0 ? "Enter the passenger name." : value.Length > 150 || value.Any(c => c is '<' or '>') ? "Use a name of up to 150 characters without HTML symbols." : "";
    }
    public static string EmailError(string? value, bool required = false)
    {
        value = value?.Trim() ?? "";
        if (value.Length == 0) return required ? "Enter your email address." : "";
        if (value.Length > 254 || value.Any(char.IsWhiteSpace) || !MailAddress.TryCreate(value, out var email) || !email.Address.Equals(value, StringComparison.OrdinalIgnoreCase)) return "Enter a valid email address, such as name@example.com.";
        var parts = email.Host.Split('.');
        return email.User.Length > 64 || parts.Length < 2 || parts.Any(p => p.Length == 0 || p.StartsWith('-') || p.EndsWith('-')) || parts.Last().Length < 2 ? "Enter a valid email address, such as name@example.com." : "";
    }
    public static string PhoneError(string? value)
    {
        value = NormalizeDigits(value).Trim();
        if (value.Length == 0) return "Enter a phone number.";
        var digits = value.Count(c => c is >= '0' and <= '9');
        if (digits < 7 || digits > 15 || value.Any(c => !(c is >= '0' and <= '9') && c is not ('+' or ' ' or '-' or '(' or ')' or '.')) || value.Count(c => c == '+') > 1 || value.Contains('+') && value[0] != '+')
            return "Enter a valid phone number with 7 to 15 digits. A leading + is allowed.";
        return "";
    }
}
