using LimosDispatch.Core.Constants;
using LimosDispatch.Core.GenericRepository;
using LimosDispatch.Core.Models;
namespace LimosDispatch.Core.Services;

public sealed class SessionRestoreService(AuthService auth, ISessionStore store, IGenericRepository api)
{
    public async Task<UserProfile?> RestoreAsync(CancellationToken ct = default)
    {
        var saved = await store.LoadAsync();
        if (saved is null) return null;
        try
        {
            await auth.GetTokenAsync(ct);
            var user = await api.GetAsync<UserProfile>(ApiRoutes.Me, ct);
            if (!user.Company.Equals(saved.Company, StringComparison.OrdinalIgnoreCase))
            {
                await auth.ForgetSessionAsync(); return null;
            }
            auth.SetUser(user);
            return user;
        }
        catch (UnauthorizedAccessException) { await auth.ForgetSessionAsync(); return null; }
        catch (ApiException ex) when ((int)ex.Status is 401 or 403) { await auth.ForgetSessionAsync(); return null; }
    }
}
