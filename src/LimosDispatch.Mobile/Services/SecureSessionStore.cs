using System.Text.Json;
namespace LimosDispatch.Mobile.Services;
public sealed class SecureSessionStore(LoginMemory loginMemory) : ISessionStore
{
    private const string Key = "limosdispatch.session.v1";
    private bool persist, initialized;
    private TokenPair? memory;
    public Task SetPersistenceAsync(bool remember)
    {
        persist = remember; initialized = true;
        if (!remember) SecureStorage.Default.Remove(Key);
        return Task.CompletedTask;
    }
    public async Task<TokenPair?> LoadAsync()
    {
        if (!initialized) { persist = await loginMemory.LoadAsync() is not null; initialized = true; }
        if (!persist) { SecureStorage.Default.Remove(Key); return memory; }
        try { var s = await SecureStorage.Default.GetAsync(Key); return s is null ? null : JsonSerializer.Deserialize<TokenPair>(s); }
        catch { SecureStorage.Default.Remove(Key); return null; }
    }
    public Task SaveAsync(TokenPair tokens)
    {
        memory = tokens;
        return persist ? SecureStorage.Default.SetAsync(Key, JsonSerializer.Serialize(tokens)) : Task.CompletedTask;
    }
    public Task ClearAsync() { memory = null; SecureStorage.Default.Remove(Key); return Task.CompletedTask; }
}
