using LimosDispatch.Core.Models;
namespace LimosDispatch.Core.Helpers;
/// <summary>Position and presence clocks are independent; heartbeats never create GPS positions.</summary>
public sealed class LiveTrackingState
{
    private sealed class Entry
    {
        public string TripId = "";
        public DriverLocation? Position;
        public long LastContact;
        public long LastCaptured;
        public string Motion = "";
    }
    private readonly Dictionary<string, Entry> drivers = [];
    private static bool Assigned(string driverId, string tripId, IReadOnlyList<Trip> trips) =>
        !string.IsNullOrEmpty(driverId) && !string.IsNullOrEmpty(tripId) && trips.Any(t => t.IsActive && t.Id == tripId && t.DriverId == driverId);
    private static bool TimeValid(long time) => time > 0 && time <= 253402300799999;
    private static bool PositionValid(double lat, double lon, double accuracy) => double.IsFinite(lat) && double.IsFinite(lon) && double.IsFinite(accuracy) && Math.Abs(lat) <= 90 && Math.Abs(lon) <= 180 && accuracy >= 0;
    private Entry EntryFor(string driverId, string tripId)
    {
        if (!drivers.TryGetValue(driverId, out var entry) || entry.TripId != tripId) drivers[driverId] = entry = new() { TripId = tripId };
        return entry;
    }
    public bool Position(LocationFrame frame, IReadOnlyList<Trip> trips)
    {
        if (!Assigned(frame.DriverId, frame.TripId, trips) || !TimeValid(frame.CapturedAt) || !TimeValid(frame.ReceivedAt) || !PositionValid(frame.Latitude,frame.Longitude,frame.Accuracy)) return false;
        var entry = EntryFor(frame.DriverId,frame.TripId);
        if (frame.CapturedAt <= entry.LastCaptured || frame.ReceivedAt < (entry.Position?.ReceivedAt ?? 0)) return false;
        if (entry.Position is { } old && TrackingPointFilter.DistanceMeters(old.Latitude,old.Longitude,frame.Latitude,frame.Longitude) >= 3) entry.Motion = "moving";
        entry.Position = new() { Latitude=frame.Latitude, Longitude=frame.Longitude, Accuracy=frame.Accuracy, CapturedAt=frame.CapturedAt, ReceivedAt=frame.ReceivedAt, Sharing=true };
        entry.LastCaptured=frame.CapturedAt; entry.LastContact=Math.Max(entry.LastContact,frame.ReceivedAt); return true;
    }
    public bool Presence(TrackingHeartbeat beat, IReadOnlyList<Trip> trips)
    {
        if (!Assigned(beat.DriverId,beat.TripId,trips) || !TimeValid(beat.ReceivedAt) || beat.Motion is not ("moving" or "stationary")) return false;
        var entry=EntryFor(beat.DriverId,beat.TripId); if(beat.ReceivedAt <= entry.LastContact)return false;
        entry.LastContact=beat.ReceivedAt;entry.Motion=beat.Motion;return true;
    }
    public void Snapshot(TrackingSnapshot snapshot,IReadOnlyList<Trip> trips)
    {
        Prune(trips);
        var included=new HashSet<string>();
        foreach(var p in snapshot.Locations??[])
        {
            if(!Assigned(p.DriverId,p.TripId,trips)||!TimeValid(p.StoredAt)||!PositionValid(p.Latitude,p.Longitude,p.Accuracy))continue;
            included.Add(p.DriverId);var entry=EntryFor(p.DriverId,p.TripId);
            if(p.StoredAt >= (entry.Position?.ReceivedAt ?? 0))
                entry.Position=new(){Latitude=p.Latitude,Longitude=p.Longitude,Accuracy=p.Accuracy,ReceivedAt=p.StoredAt,Sharing=true};
            entry.LastContact=Math.Max(entry.LastContact,p.StoredAt);
        }
        // Do not erase a location event that raced ahead of the snapshot response.
        if(snapshot.ServerNow>0)foreach(var id in drivers.Keys.Where(id=>!included.Contains(id)&&(drivers[id].Position?.ReceivedAt??0)<=snapshot.ServerNow).ToArray())drivers.Remove(id);
    }
    public void Prune(IReadOnlyList<Trip> trips)
    { foreach(var id in drivers.Keys.Where(id=>!Assigned(id,drivers[id].TripId,trips)).ToArray())drivers.Remove(id); }
    public List<TrackedDriver> Build(IReadOnlyList<Trip> trips,string tripId="")
    {
        Prune(trips);var result=new List<TrackedDriver>();
        foreach(var pair in drivers)
        {
            var entry=pair.Value;if(entry.Position is null || tripId.Length>0&&entry.TripId!=tripId)continue;
            var trip=trips.First(t=>t.Id==entry.TripId);
            result.Add(new(){Id=pair.Key,DriverId=pair.Key,TripId=entry.TripId,Name=trip.DriverName,DriverName=trip.DriverName,Location=entry.Position,LastContactAt=entry.LastContact,Motion=entry.Motion});
        }
        return result;
    }
}
