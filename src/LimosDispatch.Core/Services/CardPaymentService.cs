using LimosDispatch.Core.GenericRepository;
using LimosDispatch.Core.Models;
namespace LimosDispatch.Core.Services;
public sealed class CardPaymentService(IGenericRepository api, DispatchService dispatch)
{
    public Task<CardSettings> SettingsAsync(CancellationToken ct = default) => api.GetAsync<CardSettings>("api/reservations/card-settings", ct);
    public async Task<Trip> RefreshTripAsync(string id, CancellationToken ct = default) => (await dispatch.TripsAsync(ct: ct)).Trips.FirstOrDefault(t => t.Id == id) ?? throw new InvalidOperationException("This ride is no longer available.");
    public static List<ChildSeat> Seats(string booster, string infant)
    {
        var result = new List<ChildSeat>();
        foreach (var (type, value) in new[] { ("Booster seat", booster), ("Infant seat", infant) })
        {
            if (!int.TryParse(Helpers.InputValidation.NormalizeDigits(value), out var count) || count < 0 || count > 20) throw new InvalidOperationException("Each child seat quantity must be a whole number from 0 to 20.");
            if (count > 0) result.Add(new(type, count));
        }
        return result;
    }
}
