using LimosDispatch.Core.Models;
namespace LimosDispatch.Core.Services;
public interface IOfflineSyncStore
{
    Task<OfflineJourney?> GetAsync(OfflineScope scope,string tripId);
    Task<List<OfflineJourney>> ListAsync(OfflineScope scope);
    Task UpdateAsync(OfflineScope scope,string tripId,Action<OfflineJourney> update);
    Task CaptureAsync(OfflineScope scope,string tripId,GpsPoint point);
    Task<List<GpsPoint>> PointsAsync(OfflineScope scope,string tripId,int limit,long capturedAfter=0);
    Task AcknowledgeAsync(OfflineScope scope,string tripId,IReadOnlyCollection<long> capturedAt);
    Task ApplyBatchAsync(OfflineScope scope,string tripId,OfflineHistoryBatch batch,OfflineHistoryReceipt receipt);
    Task<OfflineCapabilities?> CapabilitiesAsync(OfflineScope scope);
    Task SaveCapabilitiesAsync(OfflineScope scope,OfflineCapabilities value);
}
