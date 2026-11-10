using System.Collections.ObjectModel;
using LimosDispatch.Core.Helpers;
namespace LimosDispatch.Mobile.ViewModels;
public sealed class AddressSearchViewModel(GoogleAddresses google) : BaseViewModel
{
    private CancellationTokenSource? search;
    private string query = "", status = "Enter at least 3 characters to search Google addresses.";
    private readonly string session = Guid.NewGuid().ToString();
    private bool searching;
    public bool IsSearching { get => searching; private set => Set(ref searching, value); }
    public bool ShowIntro => Query.Trim().Length < 3;
    public ObservableCollection<AddressSuggestion> Suggestions { get; } = [];
    public string Query { get => query; set { if (Set(ref query, value)) { Raise(nameof(ShowIntro)); StartSearch(); } } }
    public string Status { get => status; private set => Set(ref status, value); }
    private void StartSearch()
    {
        search?.Cancel(); search?.Dispose(); search = new(); var ct = search.Token;
        Suggestions.Clear();
        IsSearching = false;
        if (Query.Trim().Length < 3) { Status = "Enter at least 3 characters to search Google addresses."; return; }
        var input = Query;
        _ = UserFeedback.RunAsync(async () =>
        {
            try
            {
                Status = "Searching Google addresses…";
                IsSearching = true;
                await Task.Delay(300, ct);
                var results = await google.SearchAsync(input, session, ct);
                if (ct.IsCancellationRequested) return;
                foreach (var result in results) Suggestions.Add(result);
                Status = results.Count == 0 ? "No suggestions. Try a more complete address." : "Choose the correct address below.";
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
            catch (Exception ex) { if (!ct.IsCancellationRequested) { Status = UserFeedback.Message(ex); await UserFeedback.ReportAsync(ex); } }
            finally { if (!ct.IsCancellationRequested) IsSearching = false; }
        });
    }
    public Task<GoogleAddress> SelectAsync(AddressSuggestion suggestion) => google.SelectAsync(suggestion, session);
    public void Stop() { search?.Cancel(); }
}
