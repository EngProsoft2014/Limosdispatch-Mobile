using LimosDispatch.Core.Models;
namespace LimosDispatch.Core.Helpers;
public static class RoutePlayback
{
    public static List<GpsPoint> Normalize(IEnumerable<GpsPoint> points) => points
        .Where(p => p.CapturedAt > 0 && p.CapturedAt <= 253402300799999 && double.IsFinite(p.Latitude) && double.IsFinite(p.Longitude)
            && Math.Abs(p.Latitude) <= 90 && Math.Abs(p.Longitude) <= 180 && double.IsFinite(p.Accuracy) && p.Accuracy >= 0)
        .OrderBy(p => p.CapturedAt).DistinctBy(p => (p.CapturedAt,p.Latitude,p.Longitude)).ToList();
    public static bool InDateRange(Trip t, DateTime from, DateTime to) => DateTime.TryParseExact(t.Date,"yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out var d) && d.Date >= from.Date && d.Date <= to.Date;
    public static GpsPoint At(IReadOnlyList<GpsPoint> points, long time)
    {
        if (points.Count == 0) throw new InvalidOperationException("No recorded locations for this journey.");
        if (time <= points[0].CapturedAt) return points[0];
        if (time >= points[^1].CapturedAt) return points[^1];
        int lo=0,hi=points.Count-1;
        while(hi-lo>1) { int m=(lo+hi)/2; if(points[m].CapturedAt<=time)lo=m;else hi=m; }
        var a=points[lo];var b=points[hi];var f=(double)(time-a.CapturedAt)/Math.Max(1,b.CapturedAt-a.CapturedAt);
        // Do not invent travel across a GPS outage. Hold last known position until the next fix.
        if(b.CapturedAt-a.CapturedAt>120000)return a;
        var delta=((b.Longitude-a.Longitude+540)%360)-180;
        var lng=((a.Longitude+delta*f+540)%360)-180;
        return new(time,a.Latitude+(b.Latitude-a.Latitude)*f,lng,a.Accuracy+(b.Accuracy-a.Accuracy)*f);
    }
    public static double DistanceKm(IReadOnlyList<GpsPoint> p)
    {
        double total=0;
        for(int i=1;i<p.Count;i++) { if(p[i].CapturedAt-p[i-1].CapturedAt>120000)continue; var a=p[i-1];var b=p[i]; double x=(b.Latitude-a.Latitude)*Math.PI/180,y=(b.Longitude-a.Longitude)*Math.PI/180;
            var h=Math.Pow(Math.Sin(x/2),2)+Math.Cos(a.Latitude*Math.PI/180)*Math.Cos(b.Latitude*Math.PI/180)*Math.Pow(Math.Sin(y/2),2);total+=6371*2*Math.Asin(Math.Sqrt(Math.Clamp(h,0,1))); }
        return total;
    }
}
