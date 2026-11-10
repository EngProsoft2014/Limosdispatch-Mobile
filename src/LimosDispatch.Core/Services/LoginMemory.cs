using System.Text.Json;
namespace LimosDispatch.Core.Services;

public sealed record RememberedLogin(string Company, string Email, string Password);
public interface ILoginMemoryStorage
{
    Task<string?> ReadAsync();
    Task WriteAsync(string value);
    Task RemoveAsync();
}

// Serialize edits so a company-change clear cannot be overtaken by a save.
public sealed class LoginMemory(ILoginMemoryStorage storage)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    public static string NormalizeCompany(string company) => company.Trim().ToLowerInvariant();
    public async Task<RememberedLogin?> LoadAsync()
    {
        await gate.WaitAsync();
        try
        {
            var json = await storage.ReadAsync();
            if (json is null) return null;
            try { return JsonSerializer.Deserialize<RememberedLogin>(json); }
            catch (JsonException) { await storage.RemoveAsync(); return null; }
        }
        finally { gate.Release(); }
    }
    public async Task SaveAsync(string company, string email, string password, bool remember)
    {
        await gate.WaitAsync();
        try
        {
            if (remember)
                await storage.WriteAsync(JsonSerializer.Serialize(new RememberedLogin(NormalizeCompany(company), email.Trim(), password)));
            else await storage.RemoveAsync();
        }
        finally { gate.Release(); }
    }
    public Task ClearAsync() => SaveAsync("", "", "", false);
}
