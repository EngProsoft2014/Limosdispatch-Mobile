using System.Text.RegularExpressions;
namespace LimosDispatch.Core.Models;

public sealed class PushNotice
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string Company { get; set; } = "";
    public string TripId { get; set; } = "";
    public string ExtraId { get; set; } = "";
    public string Revision { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
    public bool IsRead { get; set; }
}
public sealed class PushInbox
{
    public List<PushNotice> Items { get; set; } = [];
    public string? NextCursor { get; set; }
    public int UnreadCount { get; set; }
}
public sealed class PushCount { public int UnreadCount { get; set; } }
public sealed record PushDevice(string InstallationId, string Platform, string RegistrationToken, string AppVersion);
public sealed record PushTarget(string Company, string TripId, string NotificationId = "")
{
    public static bool ValidId(string? value) => value is { Length: > 0 and <= 128 } && Regex.IsMatch(value, @"^[A-Za-z0-9_-]+$", RegexOptions.CultureInvariant);
    public static PushTarget? Create(string? company, string? tripId, string? notificationId = "") =>
        company is { Length: > 0 and <= 63 } && Regex.IsMatch(company, @"^[a-z0-9][a-z0-9-]*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
        && ValidId(tripId) && (string.IsNullOrEmpty(notificationId) || ValidId(notificationId))
            ? new(company.ToLowerInvariant(), tripId!, notificationId ?? "") : null;
    public static PushTarget? FromData(IDictionary<string, string>? data)
    {
        if (data is null || !data.TryGetValue("type", out var type) || type is not ("ride_assigned" or "website_booking_created" or "trip_extra_requested" or "ride_reminder")) return null;
        if (!data.TryGetValue("company", out var company) || !data.TryGetValue("tripId", out var trip) || !data.TryGetValue("notificationId", out var id) || !ValidId(id)) return null;
        return Create(company, trip, id);
    }
    
}
