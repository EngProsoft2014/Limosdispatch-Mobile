using LimosDispatch.Core.Models;
namespace LimosDispatch.Core.Helpers;
public static class ScreenRules
{
    public static bool IsDriverPast(Trip trip) => trip.Status is "Completed" or "Cancelled" or "No-show";
    // A missed pickup time does not finish a reservation. Both roles use the same terminal states.
    public static bool IsPast(Trip trip, DateTimeOffset now) => IsDriverPast(trip);
    public static void ValidateExpenseDate(DateTime date, DateTime today)
    {
        if (date.Date > today.Date) throw new InvalidOperationException("An expense cannot be dated in the future. Choose today or an earlier date.");
    }
}
