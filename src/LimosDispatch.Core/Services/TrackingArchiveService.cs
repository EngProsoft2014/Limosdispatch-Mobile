using LimosDispatch.Core.Models;
using LimosDispatch.Core.GenericRepository;
using LimosDispatch.Core.Helpers;
namespace LimosDispatch.Core.Services;
public sealed class TrackingArchiveService(IGenericRepository api)
{
    public async Task<List<GpsPoint>> LocationsAsync(string tripId,CancellationToken ct=default)
    {
        if(string.IsNullOrWhiteSpace(tripId))throw new ArgumentException("Select a journey.");
        var all=new List<GpsPoint>(); long after=0;
        for(int page=0;page<1000;page++)
        {
            var result=await api.GetAsync<LocationHistoryPage>($"api/trips/{Uri.EscapeDataString(tripId)}/location-history"+(after>0?$"?after={after}":""),ct);
            all.AddRange(result.Points);
            if(all.Count>100000)throw new InvalidOperationException("This route is too large to load on this device.");
            if(result.NextAfter is not { } next || next<=after || result.Points.Count==0)return RoutePlayback.Normalize(all);
            after=next;
        }
        throw new InvalidOperationException("The route could not be loaded completely. Please try again.");
    }
}
