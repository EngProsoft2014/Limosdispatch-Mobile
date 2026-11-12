namespace LimosDispatch.Core.Models;

public sealed record MileageReading(decimal Miles, string PhotoId, DateTimeOffset? CapturedAt = null, string? Reason = null, bool IsLate = false);
public sealed record MileageShift(string Id, string UserId, string Driver, string VehicleId, string Vehicle, string Day, MileageReading Start, MileageReading? End);
public sealed record MileageVehicle(string Id, string? Name, string? Plate, string? Label)
{
    public string Display => Label ?? string.Join(" · ", new[] { Name, Plate }.Where(x => !string.IsNullOrWhiteSpace(x)));
}
public sealed record MileageReport(string Today, List<MileageShift> Items, List<MileageShift> Open, List<MileageVehicle> Vehicles, decimal TotalMiles);
public sealed record MileageOcr(decimal? Miles);
