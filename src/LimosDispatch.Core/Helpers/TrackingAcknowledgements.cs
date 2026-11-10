using LimosDispatch.Core.Models;
namespace LimosDispatch.Core.Helpers;
public static class TrackingAcknowledgements
{
    public static int Apply(List<GpsPoint> pending, IReadOnlyList<GpsPoint> sent, LocationAck ack)
    {
        var submitted = sent.Select(p => p.CapturedAt).ToHashSet();
        var accepted = (ack.AcknowledgedCapturedAt??[]).Where(submitted.Contains).ToHashSet();
        if (accepted.Count == 0) throw new InvalidDataException("Location upload was not acknowledged. It will be retried.");
        // inserted counts archive writes, not accepted live points. Never use it to discard the queue.
        return pending.RemoveAll(p => accepted.Contains(p.CapturedAt));
    }
}
