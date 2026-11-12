using LimosDispatch.Core.GenericRepository;
using LimosDispatch.Core.Models;

namespace LimosDispatch.Core.Services;

public sealed class MileageApi(IGenericRepository api)
{
    private const string Route = "api/mileage";

    public Task<MileageReport> ListAsync(DateOnly? from = null, DateOnly? to = null) =>
        api.GetAsync<MileageReport>(from is null ? Route : FormattableString.Invariant($"{Route}?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}"));

    public Task<MileageOcr> ReadAsync(string photo) => api.PostAsync<MileageOcr>(Route + "/read", new { photo });

    public Task<MileageShift> SaveAsync(Guid requestId, MileageShift? shift, string vehicleId, decimal miles, string photo, bool isLate, string reason) =>
        api.PostAsync<MileageShift>(Route, shift is null
            ? (object)new { requestId, mode = "check-in", vehicleId, miles, photo, isLate, reason }
            : new { requestId, mode = "check-out", shiftId = shift.Id, miles, photo, isLate, reason });

    public Task<byte[]> PhotoAsync(string id) => api.GetAsync<byte[]>(Route + "/photo/" + Uri.EscapeDataString(id));
}
