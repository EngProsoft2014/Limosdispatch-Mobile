using System.Net;
using System.Text.Json;
using LimosDispatch.Core.GenericRepository;
namespace LimosDispatch.Core.Helpers;

public static class UserFeedback
{
    public static Func<string, Task> Presenter { get; set; } = _ => Task.CompletedTask;
    private const string Fallback = "Unable to complete this action. Please try again or refresh the screen.";
    public static string SafeMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message) || message.Length > 350 || message.Contains('<') || message.Contains('>') || message.Contains(" at ", StringComparison.Ordinal) || message.Contains("System.", StringComparison.Ordinal)) return Fallback;
        return message.Trim();
    }
    public static string Message(Exception error) => error switch
    {
        ApiException api when api.Status == HttpStatusCode.Unauthorized => "Your session has expired or your sign-in details are incorrect. Please sign in again.",
        ApiException api when api.Status == HttpStatusCode.Forbidden => "You do not have permission to perform this action.",
        ApiException api when api.Status == HttpStatusCode.Conflict => "Data has changed on the server. Refresh and review it before saving.",
        ApiException api when api.Status == HttpStatusCode.TooManyRequests => "Too many requests. Please wait a moment and try again.",
        ApiException api when (int)api.Status >= 500 => "The server is currently unavailable. Please try again shortly.",
        ApiException api => SafeMessage(api.Message),
        HttpRequestException => "Unable to connect. Check your internet connection and try again.",
        TimeoutException or OperationCanceledException => "The request timed out. Refresh and check the result before saving again.",
        JsonException or InvalidDataException or FormatException or InvalidCastException => "Unable to read the received data. Refresh or try again later.",
        UnauthorizedAccessException => "Access is unavailable. Please check your app permissions.",
        InvalidOperationException validation => SafeMessage(validation.Message),
        _ => Fallback
    };
    public static async Task ShowAsync(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        try { await Presenter(SafeMessage(message)); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Feedback unavailable: {ex.GetType().Name}"); }
    }
    public static Task ReportAsync(Exception error) => ShowAsync(Message(error));
    public static async Task RunAsync(Func<Task> action)
    {
        try { await action(); }
        catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested && ex.InnerException is not TimeoutException) { }
        catch (Exception ex) { await ReportAsync(ex); }
    }
}
