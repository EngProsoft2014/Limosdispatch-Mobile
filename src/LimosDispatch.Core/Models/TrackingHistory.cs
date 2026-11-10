namespace LimosDispatch.Core.Models;
public sealed class LocationHistoryPage
{
    public List<GpsPoint> Points { get; set; } = [];
    public long? NextAfter { get; set; }
}
