using LimosDispatch.Core.Models;
namespace LimosDispatch.Core.Helpers;
public static class RideInbox
{
    public static List<RideNotice> Build(IEnumerable<Trip> trips, bool dispatch, DateTimeOffset now)
    {
        var notices = new List<RideNotice>();
        foreach (var trip in trips)
        {
            if (!dispatch)
            {
                if (!ScreenRules.IsPast(trip, now)) notices.Add(new($"ride|{trip.Id}|{trip.PickupAt}|{trip.Status}|{trip.DriverId}", "Upcoming ride", trip.Route, trip, trip.PickupAt));
                continue;
            }
            if (string.Equals(trip.BookingSource, "Website", StringComparison.OrdinalIgnoreCase) || string.Equals(trip.BookingSource, "Website booking", StringComparison.OrdinalIgnoreCase))
                notices.Add(new($"website|{trip.Id}", "Website booking", $"{trip.Name} · {trip.Route}", trip, trip.PickupAt));
            foreach (var extra in trip.Extras ?? [])
                if (string.Equals(extra.Status, "Pending", StringComparison.OrdinalIgnoreCase))
                    notices.Add(new($"extra|{trip.Id}|{extra.Id}", "Extra request awaiting review", $"{trip.DriverName} · {extra.Quantity:0.##} {extra.Unit} · {extra.Description}", trip, extra.RequestedAt));
        }
        return dispatch ? notices.OrderByDescending(n => n.CreatedAt).ToList() : notices.OrderBy(n => n.CreatedAt).ToList();
    }
}
