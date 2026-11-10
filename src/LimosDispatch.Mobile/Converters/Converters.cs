using System.Globalization;
namespace LimosDispatch.Mobile.Converters;
public sealed class StatusColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => Color.FromArgb(value?.ToString() switch
    { "Completed" => "#168269", "Enroute" or "Arrived" or "Onboard" => "#2473B6", "Cancelled" => "#B74343", _ => "#9B793E" });
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}
public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? v, Type t, object? p, CultureInfo c) => v is not true;
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) => v is not true;
}
public sealed class MoneyConverter : IValueConverter
{
 public object Convert(object? v,Type t,object? p,CultureInfo c){if(v is null)return LocalizationService.Text("Needs review");var prefix=LocalizationService.Text(p?.ToString()).TrimEnd();return ((prefix.Length==0?"":prefix+" ")+System.Convert.ToDecimal(v).ToString("N2",LocalizationService.NumericCulture)+" "+CompanySettingsService.Currency).TrimEnd();}
 public object ConvertBack(object? v,Type t,object? p,CultureInfo c)=>throw new NotSupportedException();
}
