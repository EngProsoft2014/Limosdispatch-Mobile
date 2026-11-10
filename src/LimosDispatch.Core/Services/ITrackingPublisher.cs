using LimosDispatch.Core.Models;
namespace LimosDispatch.Core.Services;
public interface ITrackingPublisher
{
    Task<LocationAck> PublishAsync(string tripId, List<GpsPoint> points, CancellationToken ct = default);
    Task HeartbeatAsync(string tripId, string motion, CancellationToken ct = default);
    Task StopAsync();
}
