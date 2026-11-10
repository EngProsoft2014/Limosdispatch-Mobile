namespace LimosDispatch.Core.Helpers;
public sealed class TrackingUploadBackoff
{
    private int failures;
    private DateTimeOffset retryAt;
    public bool CanAttempt(DateTimeOffset now) => now >= retryAt;
    public void Succeeded() { failures = 0; retryAt = default; }
    public void Failed(DateTimeOffset now)
    {
        failures = Math.Min(failures + 1, 3);
        retryAt = now.AddSeconds(15 * (1 << (failures - 1)));
    }
}
