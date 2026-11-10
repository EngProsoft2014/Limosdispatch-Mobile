namespace LimosDispatch.Core.Models;
public sealed class LocationFrame
{
    public string TripId { get; set; } = "";
    public string DriverId { get; set; } = "";
    public long CapturedAt { get; set; }
    public long ReceivedAt { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Accuracy { get; set; }
}
public sealed class TrackingSnapshotPosition
{
    public string TripId { get; set; } = "";
    public string DriverId { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Accuracy { get; set; }
    public long StoredAt { get; set; }
}
public sealed class TrackingSnapshot
{
    public List<TrackingSnapshotPosition> Locations { get; set; } = [];
    public long ServerNow { get; set; }
}
public sealed class TrackingHeartbeat
{
    public string TripId { get; set; } = "";
    public string DriverId { get; set; } = "";
    public string Motion { get; set; } = "";
    public long ReceivedAt { get; set; }
}
public sealed class LocationAck
{
    public int Inserted { get; set; }
    public long ReceivedAt { get; set; }
    public List<long> AcknowledgedCapturedAt { get; set; } = [];
    public int HistoryIntervalMs { get; set; }
    public LocationFrame? Current { get; set; }
}
public sealed class TrackingQueueState
{
    public List<GpsPoint> Pending { get; set; } = [];
    public GpsPoint? LastAccepted { get; set; }
}
