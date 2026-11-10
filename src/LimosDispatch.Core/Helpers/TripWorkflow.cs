using LimosDispatch.Core.Models;
namespace LimosDispatch.Core.Helpers;
public static class TripWorkflow
{
    public static string StageLabel(string status) => status switch
    { "Enroute" => "On the way", "Arrived" => "At the pickup location", "Onboard" => "Passenger on board", "Completed" => "Finished", _ => status };
    public static string? Next(string status) => status switch
    { "Confirmed" => "Enroute", "Enroute" => "Arrived", "Arrived" => "Onboard", "Onboard" => "Completed", _ => null };
    public static string ActionLabel(string status) => Next(status) switch
    { "Enroute" => "On the way", "Arrived" => "At the pickup location", "Onboard" => "Passenger on board", "Completed" => "Finish journey", _ => "No further action" };
    public static void ValidateExtra(Trip trip, TripExtraRequest request)
    {
        if (!trip.IsActive) throw new InvalidOperationException("Start your journey before requesting an extra.");
        if (request.TripId != trip.Id || !Guid.TryParse(request.RequestId, out _)) throw new InvalidOperationException("Refresh this journey before requesting an extra.");
        if (request.Type is not ("Distance" or "Time") || request.Quantity < 0.01m || request.Quantity > 100000m)
            throw new InvalidOperationException("Enter an extra quantity between 0.01 and 100000.");
        if (string.IsNullOrWhiteSpace(request.Description) || request.Description.Trim().Length > 1000)
            throw new InvalidOperationException("Enter an extra description of up to 1000 characters.");
    }
    public static void ValidateTransition(Trip trip, string next)
    { if (Next(trip.Status) != next) throw new InvalidOperationException("Refresh the journey before changing its status."); }
    public static long PickupUnix(DateTime date, TimeSpan time, string timezone)
    {
        var local = DateTime.SpecifyKind(date.Date.Add(time), DateTimeKind.Unspecified);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timezone);
        if (zone.IsInvalidTime(local)) throw new InvalidOperationException("This pickup time does not exist due to daylight saving time.");
        if (zone.IsAmbiguousTime(local)) throw new InvalidOperationException("This pickup time occurs twice due to daylight saving time. Choose an unambiguous time.");
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone)).ToUnixTimeMilliseconds();
    }
}
