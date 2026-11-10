using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
namespace LimosDispatch.Core.GenericRepository;
public sealed class ApiException(HttpStatusCode status, string message) : Exception(message)
{ public HttpStatusCode Status { get; } = status;
public string Code {get;init;}="";public bool? Retryable {get;init;}public string RequestId {get;init;}="";public string CurrentTripStatus {get;init;}="";public TimeSpan? RetryAfter {get;init;}
}
public interface IAccessTokenProvider
{
    Task<string> GetTokenAsync(CancellationToken ct = default);
    Task<string> RefreshAfterRejectionAsync(string rejected, CancellationToken ct = default);
}
public interface IGenericRepository
{
    Task<T> GetAsync<T>(string path, CancellationToken ct = default);
    Task<T> PostAsync<T>(string path, object body, CancellationToken ct = default);
    Task<T> PutAsync<T>(string path, object body, CancellationToken ct = default);
    Task<T> DeleteAsync<T>(string path, CancellationToken ct = default);
    Task<T> DeleteAsync<T>(string path, object body, CancellationToken ct = default);
}
public sealed class ApiRepository(HttpClient client, IAccessTokenProvider tokens) : IGenericRepository
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };
    public Task<T> GetAsync<T>(string path, CancellationToken ct = default) => Send<T>(HttpMethod.Get, path, null, ct);
    public Task<T> PostAsync<T>(string path, object body, CancellationToken ct = default) => Send<T>(HttpMethod.Post, path, body, ct);
    public Task<T> PutAsync<T>(string path, object body, CancellationToken ct = default) => Send<T>(HttpMethod.Put, path, body, ct);
    public Task<T> DeleteAsync<T>(string path, CancellationToken ct = default) => Send<T>(HttpMethod.Delete, path, null, ct);
    public Task<T> DeleteAsync<T>(string path, object body, CancellationToken ct = default) => Send<T>(HttpMethod.Delete, path, body, ct);
    private async Task<T> Send<T>(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (client.Timeout != Timeout.InfiniteTimeSpan) deadline.CancelAfter(client.Timeout);
        var requestToken = deadline.Token;
        try
        {
            var token = await tokens.GetTokenAsync(requestToken);
            using var response = await Execute(method, path, body, token, requestToken);
            if (response.StatusCode != HttpStatusCode.Unauthorized) return await Read<T>(response, requestToken);
            token = await tokens.RefreshAfterRejectionAsync(token, requestToken);
            // Retry only a rejected 401 request. Never retry ambiguous writes or 409 conflicts.
            using var retry = await Execute(method, path, body, token, requestToken);
            return await Read<T>(retry, requestToken);
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested) { throw new TimeoutException("The API request timed out. Refresh and check its result before retrying a write.", ex); }
        catch (JsonException ex) { throw new InvalidDataException("The API returned data in an unexpected format.", ex); }
    }
    private Task<HttpResponseMessage> Execute(HttpMethod method, string path, object? body, string token, CancellationToken ct)
    {
        if (Uri.TryCreate(path, UriKind.Absolute, out _)) throw new ArgumentException("API paths must be relative.");
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body, options: Json);
        return SendAndDispose(request, ct);
    }
    private async Task<HttpResponseMessage> SendAndDispose(HttpRequestMessage request, CancellationToken ct)
    { using (request) return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct); }
    public static async Task<T> Read<T>(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            var message = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "Your session expired. Sign in again.",
                HttpStatusCode.Forbidden => "Your account does not have permission for this action.",
                HttpStatusCode.Conflict => "Data changed on the server. Refresh and review before saving again.",
                HttpStatusCode.TooManyRequests => "Too many requests. Please try again shortly.",
                _ => $"The server could not complete the request ({(int)response.StatusCode})."
            };
            string code="",requestId="",tripStatus="";bool? retryable=null;
            var body = response.Content.Headers.ContentLength > 16_384 ? "" : await response.Content.ReadAsStringAsync(ct);
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    if(doc.RootElement.TryGetProperty("code",out var c)&&c.ValueKind==JsonValueKind.String)code=c.GetString()??"";
                    if(doc.RootElement.TryGetProperty("requestId",out var q)&&q.ValueKind==JsonValueKind.String)requestId=q.GetString()??"";
                    if(doc.RootElement.TryGetProperty("currentTripStatus",out var t)&&t.ValueKind==JsonValueKind.String)tripStatus=t.GetString()??"";
                    if(doc.RootElement.TryGetProperty("retryable",out var b)&&b.ValueKind is JsonValueKind.True or JsonValueKind.False)retryable=b.GetBoolean();
                    foreach (var key in new[] { "error", "message", "detail", "title" })
                        if (doc.RootElement.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()))
                        { message = LimosDispatch.Core.Helpers.UserFeedback.SafeMessage(value.GetString()); break; }
                }
            }
            catch (JsonException) { }
            var retryAfter=response.Headers.RetryAfter?.Delta;
            if(retryAfter is null&&response.Headers.RetryAfter?.Date is {} at)retryAfter=at-DateTimeOffset.UtcNow;
            throw new ApiException(response.StatusCode, message){Code=code,RequestId=requestId,CurrentTripStatus=tripStatus,Retryable=retryable,RetryAfter=retryAfter};
        }
        if (response.StatusCode == HttpStatusCode.NoContent) return default!;
        return await response.Content.ReadFromJsonAsync<T>(Json, ct) ?? throw new JsonException("The API returned an empty response.");
    }
}
