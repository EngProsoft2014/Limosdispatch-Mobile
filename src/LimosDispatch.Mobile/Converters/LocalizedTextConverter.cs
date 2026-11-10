using System.Globalization;
using LimosDispatch.Mobile.Services;
namespace LimosDispatch.Mobile.Converters;
public sealed class LocalizedTextConverter : IValueConverter
{
    public object Convert(object? value, Type target, object? parameter, CultureInfo culture) => parameter is "SystemRule"
        ? SystemRuleText.Translate(value?.ToString())
        : parameter is string format
        ? string.Format(LocalizationService.Culture, LocalizationService.Text(format), value)
        : value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal ? ((IFormattable)value).ToString(null,LocalizationService.NumericCulture) : LocalizationService.Text(value?.ToString());
    public object ConvertBack(object? value, Type target, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
