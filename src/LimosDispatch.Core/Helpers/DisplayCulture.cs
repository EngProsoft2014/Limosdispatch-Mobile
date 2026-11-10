using System.Globalization;
namespace LimosDispatch.Core.Helpers;
/// <summary>Translated dates and text, with English digits and numeric punctuation in every language.</summary>
public static class DisplayCulture
{
 public static CultureInfo Numbers {get;}=CultureInfo.GetCultureInfo("en-US");
 public static CultureInfo ForLanguage(string code)
 {
  var culture=(CultureInfo)CultureInfo.GetCultureInfo(code=="ar"?"ar-EG":code=="es"?"es-ES":"en-US").Clone();
  culture.NumberFormat=(NumberFormatInfo)Numbers.NumberFormat.Clone();
  return CultureInfo.ReadOnly(culture);
 }
}
