namespace LimosDispatch.Mobile.ViewModels;
public abstract class BaseViewModel : ObservableObject
{
    protected BaseViewModel() => LocalizationService.Instance.Changed += OnLanguageChanged;
    private void OnLanguageChanged(object? sender, EventArgs args) => Raise(string.Empty);
    private bool busy;
    private bool running;
    private TaskCompletionSource? backgroundFinished;
    private string error = "";
    public bool IsBusy { get => busy; set => Set(ref busy, value); }
    public string Error { get => error; set { Set(ref error, value); Raise(nameof(HasError)); } }
    public bool HasError => !string.IsNullOrEmpty(Error);
    public AsyncCommand RefreshCommand { get; protected set; } = null!;
    protected async Task Run(Func<Task> action, bool showBusy = true)
    {
        if (running || IsBusy)
        {
            // User actions wait for a silent refresh rather than disappearing while the controls look enabled.
            if (!showBusy || IsBusy || backgroundFinished is not {} pending) return;
            await pending.Task;
            if (running || IsBusy) return;
        }
        running = true;
        if (!showBusy) backgroundFinished = new(TaskCreationOptions.RunContinuationsAsynchronously);
        if (showBusy) IsBusy = true;
        Error = "";
        try { await action(); }
        catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested && ex.InnerException is not TimeoutException) { }
        catch (Exception ex) { Error = LimosDispatch.Core.Helpers.UserFeedback.Message(ex); await LimosDispatch.Core.Helpers.UserFeedback.ShowAsync(Error); }
        finally
        {
            if (showBusy) IsBusy = false;
            running = false;
            if (!showBusy) { var pending = backgroundFinished; backgroundFinished = null; pending?.TrySetResult(); }
        }
    }
    public virtual Task LoadAsync() => Task.CompletedTask;
}
