namespace LimosDispatch.Core.Constants;
public static class ApiRoutes
{
    public const string BaseUrl = "https://api.limosdispatch.com/";
    public const string Login = "api/auth/login", Refresh = "api/auth/refresh", Me = "api/auth/me";
    public const string Trips = "api/trips", Portal = "api/driver/portal", Status = "api/driver/trip/status";
    public const string Tracking = "api/tracking/drivers", Assign = "api/trips/assign";
    public const string DriverEvents = "hubs/driver-events";
    public static string Record(string module) => $"api/records/{Uri.EscapeDataString(module)}";
}
