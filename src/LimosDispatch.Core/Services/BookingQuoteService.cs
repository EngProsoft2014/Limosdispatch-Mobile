using LimosDispatch.Core.GenericRepository;
using LimosDispatch.Core.Models;
namespace LimosDispatch.Core.Services;
public sealed class BookingQuoteService(IGenericRepository api)
{
    public async Task<ReservationQuote> QuoteAsync(decimal fare, string customerId, CancellationToken ct = default, string couponCode = "", string paymentMethod = "Cash", string? tripId = null)
    {
        if (!new[] { "Cash", "Zelle", "Check", "Credit Card" }.Contains(paymentMethod)) throw new InvalidOperationException("Choose a valid payment method.");
        var quote = await api.PostAsync<ReservationQuote>("api/reservations/quote", new { fare, customerId, couponCode = couponCode.Trim().ToUpperInvariant(), tripId, paymentMethod, quoteId = (string?)null }, ct);
        if (quote.Price < 0 || quote.FareBeforeDiscount != fare || quote.TotalAfterDiscount < 0) throw new InvalidDataException("The price summary could not be verified. Refresh and try again.");
        return quote;
    }
    public static void Apply(Trip trip, ReservationQuote quote, string paymentMethod = "Cash")
    {
        trip.Price = quote.Price; trip.FareBeforeDiscount = quote.FareBeforeDiscount; trip.CustomerDiscount = quote.CustomerDiscount;
        trip.CouponDiscount = quote.CouponDiscount; trip.TotalAfterDiscount = quote.TotalAfterDiscount;
        trip.CreditCardFeeRate = quote.CreditCardFeeRate; trip.CreditCardFeeFixed = quote.CreditCardFeeFixed; trip.CreditCardFee = quote.CreditCardFee;
        trip.CouponCode = quote.CouponCode; trip.CouponId = quote.CouponId; trip.CustomerDiscountSnapshot = quote.CustomerDiscountSnapshot;
        trip.PaymentMethod = paymentMethod;
    }
}
