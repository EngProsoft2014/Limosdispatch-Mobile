using System.Text.Json;
using System.Text.Json.Serialization;
namespace LimosDispatch.Core.Models;
public sealed class ReservationQuote
{
    [JsonRequired, JsonConverter(typeof(FlexibleDecimalConverter))] public decimal Price { get; set; }
    [JsonRequired, JsonConverter(typeof(FlexibleDecimalConverter))] public decimal FareBeforeDiscount { get; set; }
    [JsonConverter(typeof(FlexibleDecimalConverter))] public decimal CustomerDiscount { get; set; }
    [JsonConverter(typeof(FlexibleDecimalConverter))] public decimal CouponDiscount { get; set; }
    [JsonRequired, JsonConverter(typeof(FlexibleDecimalConverter))] public decimal TotalAfterDiscount { get; set; }
    [JsonConverter(typeof(FlexibleDecimalConverter))] public decimal CreditCardFeeRate { get; set; }
    [JsonConverter(typeof(FlexibleDecimalConverter))] public decimal CreditCardFeeFixed { get; set; }
    [JsonConverter(typeof(FlexibleDecimalConverter))] public decimal CreditCardFee { get; set; }
    public string CouponCode { get; set; } = "";
    public string CouponId { get; set; } = "";
    public string Notice { get; set; } = "";
    public JsonElement? CustomerDiscountSnapshot { get; set; }
}
