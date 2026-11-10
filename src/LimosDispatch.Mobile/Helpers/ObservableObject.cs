using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
namespace LimosDispatch.Mobile.Helpers;
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    { if (EqualityComparer<T>.Default.Equals(field, value)) return false; field = value; Raise(name); return true; }
    protected void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}
public sealed class AsyncCommand(Func<Task> execute, Func<bool>? canExecute = null) : ICommand
{
    private bool running;
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? p) => !running && (canExecute?.Invoke() ?? true);
    public async void Execute(object? p)
    {
        if (running) return;
        running = true;
        try { if (canExecute?.Invoke() == false) return; CanExecuteChanged?.Invoke(this, EventArgs.Empty); await execute(); }
        catch (Exception ex) { await LimosDispatch.Core.Helpers.UserFeedback.ReportAsync(ex); }
        finally
        {
            running = false;
            try { CanExecuteChanged?.Invoke(this, EventArgs.Empty); }
            catch (Exception ex) { await LimosDispatch.Core.Helpers.UserFeedback.ReportAsync(ex); }
        }
    }
}
