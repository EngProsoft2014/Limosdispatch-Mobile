using System.Net.Http.Json;
using LimosDispatch.Core.Constants;
using LimosDispatch.Core.Models;
using LimosDispatch.Core.GenericRepository;
namespace LimosDispatch.Core.Services;
public interface ISessionStore
{
    Task<TokenPair?> LoadAsync();
    Task SaveAsync(TokenPair tokens);
    Task ClearAsync();
}
public sealed class AuthService(HttpClient client, ISessionStore store) : IAccessTokenProvider
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private TokenPair? current;
    private bool loaded;
    public UserProfile? User { get; private set; }
    public event Action? SignedOut;
    public async Task LoginAsync(string company, string email, string password, CancellationToken ct = default)
    {
        company = company.Trim().ToLowerInvariant();
        if (!System.Text.RegularExpressions.Regex.IsMatch(company, "^[a-z0-9][a-z0-9-]{0,62}$")) throw new InvalidOperationException("Enter a valid company code, for example lux.");
        await gate.WaitAsync(ct);
        try
        {
            using var r = await client.PostAsJsonAsync(ApiRoutes.Login, new LoginRequest(company, email.Trim(), password), ApiRepository.Json, ct);
            var pair = await ApiRepository.Read<TokenPair>(r, ct);
            if (string.IsNullOrEmpty(pair.AccessToken) || string.IsNullOrEmpty(pair.RefreshToken)) throw new InvalidOperationException("The login response did not include session tokens.");
            pair.Company = company;
            await store.ClearAsync(); // Tenant switch never reuses an old session.
            current = pair; loaded = true; User = null;
            await store.SaveAsync(pair);
        }
        finally { gate.Release(); }
    }
    public void SetUser(UserProfile user)
    {
        if (current is null || !user.Company.Equals(current.Company, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Company mismatch. Sign in again.");
        User = user;
    }
    public Task<string> GetTokenAsync(CancellationToken ct = default) => ValidToken(null, ct);
    public Task<string> RefreshAfterRejectionAsync(string rejected, CancellationToken ct = default) => ValidToken(rejected, ct);
    public async Task RejectTokenAsync(string rejected, CancellationToken ct = default)
    {
        await gate.WaitAsync(ct);
        try
        {
            // A delayed response from an old account must not clear a newer login.
            if (current?.AccessToken == rejected) await ClearInternal();
        }
        finally { gate.Release(); }
    }
    public async Task ValidateCurrentSessionAsync(CancellationToken ct = default)
    {
        if (User is null) return;
        // Use the normal 401/refresh path, including rejection of a renewed token.
        // This also checks still-unexpired access tokens against server revocation.
        await new ApiRepository(client, this).GetAsync<UserProfile>(ApiRoutes.Me, ct);
    }
    private async Task<string> ValidToken(string? rejected, CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            if (!loaded) { current = await store.LoadAsync(); loaded = true; }
            if (current is null) throw new UnauthorizedAccessException("Sign in to your company workspace.");
            if ((rejected is null && current.ExpiresAt > DateTimeOffset.UtcNow.AddSeconds(60)) ||
                (rejected is not null && current.AccessToken != rejected)) return current.AccessToken;
            using var response = await client.PostAsJsonAsync(ApiRoutes.Refresh, new { refreshToken = current.RefreshToken }, ct);
            if (!response.IsSuccessStatusCode)
            {
                if ((int)response.StatusCode is 400 or 401 or 403)
                {
                    await ClearInternal();
                    throw new UnauthorizedAccessException("Session renewal failed. Sign in again.");
                }
                throw new ApiException(response.StatusCode, "Session renewal is temporarily unavailable. Try again.");
            }
            var next = await ApiRepository.Read<TokenPair>(response, ct);
            if (string.IsNullOrWhiteSpace(next.AccessToken) || string.IsNullOrWhiteSpace(next.RefreshToken)) { await ClearInternal(); throw new UnauthorizedAccessException("Invalid renewed session."); }
            next.Company = current.Company;
            current = next;
            // Persist the rotated pair atomically in one secure-storage entry.
            try { await store.SaveAsync(next); } catch { await ClearInternal(); throw; }
            return next.AccessToken;
        }
        finally { gate.Release(); }
    }
    private async Task ClearInternal() { current = null; User = null; await store.ClearAsync(); SignedOut?.Invoke(); }
    public async Task ForgetSessionAsync()
    {
        await gate.WaitAsync();
        try { await ClearInternal(); }
        finally { gate.Release(); }
    }
    public async Task LogoutAsync(IGenericRepository api)
    {
        await gate.WaitAsync();
        try
        {
            if (current is not null)
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, "api/auth/logout") { Content = JsonContent.Create(new { refreshToken = current.RefreshToken }) };
                req.Headers.Authorization = new("Bearer", current.AccessToken);
                try { using var response = await client.SendAsync(req); } catch (HttpRequestException) { }
            }
            await ClearInternal();
        }
        finally { gate.Release(); }
    }
}
