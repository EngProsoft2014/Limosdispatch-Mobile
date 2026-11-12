using LimosDispatch.Core.Models;
namespace LimosDispatch.Core.Helpers;
public static class RoutePlayback
{
    public static List<GpsPoint> Normalize(IEnumerable<GpsPoint> points) => points
        .Where(p => p.CapturedAt > 0 && p.CapturedAt <= 253402300799999 && double.IsFinite(p.Latitude) && double.IsFinite(p.Longitude)
            && Math.Abs(p.Latitude) <= 90 && Math.Abs(p.Longitude) <= 180 && double.IsFinite(p.Accuracy) && p.Accuracy >= 0)
        .OrderBy(p => p.CapturedAt).DistinctBy(p => (p.CapturedAt,p.Latitude,p.Longitude)).ToList();
    public static bool InDateRange(Trip t, DateTime from, DateTime to) => DateTime.TryParseExact(t.Date,"yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out var d) && d.Date >= from.Date && d.Date <= to.Date;
    public static GpsPoint At(IReadOnlyList<GpsPoint> points, long time) => At(points, time, out _);

    public static GpsPoint At(IReadOnlyList<GpsPoint> points, long time, out double? bearing)
    {
        if (points.Count == 0) throw new InvalidOperationException("No recorded locations for this journey.");

        var lower = 0;
        var upper = points.Count;
        while (upper - lower > 1)
        {
            var middle = (lower + upper) / 2;
            if (points[middle].CapturedAt <= time) lower = middle;
            else upper = middle;
        }

        var start = points[lower];
        bearing = null;
        if (upper < points.Count && IsConnected(start, points[upper]))
            bearing = SegmentBearing(start, points[upper]);

        // Preserve the incoming direction during stops and at the end of a segment.
        for (var i = lower; i > 0 && bearing is null && IsConnected(points[i - 1], points[i]); i--)
            bearing = SegmentBearing(points[i - 1], points[i]);

        if (time <= start.CapturedAt || upper == points.Count || !IsConnected(start, points[upper]))
            return start;

        var end = points[upper];
        var progress = (double)(time - start.CapturedAt) / (end.CapturedAt - start.CapturedAt);
        var delta = ((end.Longitude - start.Longitude + 540) % 360) - 180;
        var longitude = ((start.Longitude + delta * progress + 540) % 360) - 180;
        return new(time, start.Latitude + (end.Latitude - start.Latitude) * progress,
            longitude, start.Accuracy + (end.Accuracy - start.Accuracy) * progress);
    }

    private static bool IsConnected(GpsPoint start, GpsPoint end) =>
        end.CapturedAt - start.CapturedAt is > 0 and <= 120000;

    private static double? SegmentBearing(GpsPoint start, GpsPoint end)
    {
        if (TrackingPointFilter.DistanceMeters(start.Latitude, start.Longitude, end.Latitude, end.Longitude) < 3)
            return null;

        const double radians = Math.PI / 180;
        var from = start.Latitude * radians;
        var to = end.Latitude * radians;
        var longitude = (end.Longitude - start.Longitude) * radians;
        var east = Math.Sin(longitude) * Math.Cos(to);
        var north = Math.Cos(from) * Math.Sin(to) - Math.Sin(from) * Math.Cos(to) * Math.Cos(longitude);
        return (Math.Atan2(east, north) / radians + 360) % 360;
    }

    public static double DistanceKm(IReadOnlyList<GpsPoint> p)
    {
        double total=0;
        for(int i=1;i<p.Count;i++) { if(p[i].CapturedAt-p[i-1].CapturedAt>120000)continue; var a=p[i-1];var b=p[i]; double x=(b.Latitude-a.Latitude)*Math.PI/180,y=(b.Longitude-a.Longitude)*Math.PI/180;
            var h=Math.Pow(Math.Sin(x/2),2)+Math.Cos(a.Latitude*Math.PI/180)*Math.Cos(b.Latitude*Math.PI/180)*Math.Pow(Math.Sin(y/2),2);total+=6371*2*Math.Asin(Math.Sqrt(Math.Clamp(h,0,1))); }
        return total;
    }
}
