namespace LimosDispatch.Mobile.Services;

public sealed class SecureLoginMemoryStorage : ILoginMemoryStorage
{
    private const string Key = "limosdispatch.remembered-login.v1";
    public Task<string?> ReadAsync() => SecureStorage.Default.GetAsync(Key);
    public Task WriteAsync(string value) => SecureStorage.Default.SetAsync(Key, value);
    public Task RemoveAsync() { SecureStorage.Default.Remove(Key); return Task.CompletedTask; }
}
