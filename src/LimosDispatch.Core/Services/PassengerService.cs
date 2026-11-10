using System.Net.Mail;
using LimosDispatch.Core.Constants;
using LimosDispatch.Core.GenericRepository;
using LimosDispatch.Core.Models;
namespace LimosDispatch.Core.Services;
public sealed class PassengerService(IGenericRepository api, AuthService auth)
{
    public bool CanAdd => auth.User?.Can("Customers", "Add") == true;
    public Task<Records<Passenger>> ListAsync(CancellationToken ct = default) => api.GetAsync<Records<Passenger>>(ApiRoutes.Record("customers"), ct);
    public static string Digits(string? phone) => string.Concat(Helpers.InputValidation.NormalizeDigits(phone).Where(char.IsDigit));
    public static string CleanName(string name) => string.Join(' ', name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    public static IEnumerable<Passenger> Search(IEnumerable<Passenger> passengers, string query)
    {
        query = query.Trim(); var digits = Digits(query);
        return passengers.Where(p => string.IsNullOrWhiteSpace(query) || (p.Name ?? "").Contains(query, StringComparison.OrdinalIgnoreCase) || (p.Email ?? "").Contains(query, StringComparison.OrdinalIgnoreCase) || digits.Length > 0 && !query.Any(char.IsLetter) && Digits(p.Phone).Contains(digits)).OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase);
    }
    public async Task<Passenger> SaveAsync(Passenger draft, CancellationToken ct = default)
    {
        if (!CanAdd) throw new UnauthorizedAccessException("Customer add permission is required to save a passenger.");
        draft.Name = CleanName(draft.Name); draft.Phone = Helpers.InputValidation.NormalizeDigits(draft.Phone).Trim(); draft.Email = draft.Email.Trim();
        foreach (var error in new[] { Helpers.InputValidation.NameError(draft.Name), Helpers.InputValidation.PhoneError(draft.Phone), Helpers.InputValidation.EmailError(draft.Email) })
            if (error.Length > 0) throw new InvalidOperationException(error);
        if (string.IsNullOrWhiteSpace(draft.CustomerId)) throw new InvalidOperationException("A passenger request identifier is required.");
        var before = await ListAsync(ct);
        var duplicate = before.Rows.FirstOrDefault(p => p.CustomerId == draft.CustomerId || CleanName(p.Name).Equals(draft.Name, StringComparison.OrdinalIgnoreCase) && Digits(p.Phone) == Digits(draft.Phone));
        if (duplicate is not null) return duplicate;
        var saved = await api.PostAsync<Records<Passenger>>(ApiRoutes.Record("customers"), new RecordInput<Passenger>(before.Version, draft), ct);
        return saved.Rows.SingleOrDefault(p => p.CustomerId == draft.CustomerId) ?? throw new InvalidOperationException("The passenger was saved but could not be selected. Refresh the passenger list before trying again.");
    }
}
