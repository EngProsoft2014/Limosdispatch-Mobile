using LimosDispatch.Core.Helpers;
using LimosDispatch.Mobile.ViewModels;
namespace LimosDispatch.Mobile.Pages;
public partial class PassengerSearchPage : ContentPage
{
    private readonly PassengerSearchViewModel model;
    private readonly TaskCompletionSource<Passenger?> result = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool closing;
    public Task<Passenger?> Result => result.Task;
    public PassengerSearchPage(PassengerSearchViewModel model) { InitializeComponent(); BindingContext = this.model = model; model.Selected = passenger => Close(passenger); }
    protected override async void OnAppearing() { base.OnAppearing(); await UserFeedback.RunAsync(model.LoadAsync); }
    protected override void OnDisappearing() { base.OnDisappearing(); if (!closing) result.TrySetResult(null); }
    private async Task Close(Passenger? value) { if (closing) return; closing = true; try { await Navigation.PopModalAsync(); result.TrySetResult(value); } catch { closing = false; throw; } }
    private async void OnResultTapped(object? sender, TappedEventArgs args) { if (!closing && !model.IsBusy && sender is BindableObject { BindingContext: Passenger p }) await UserFeedback.RunAsync(() => Close(p)); }
    private async void OnCancel(object? sender, EventArgs args) { if (!model.IsBusy) await UserFeedback.RunAsync(() => Close(null)); }
    private void OnNameUnfocused(object? sender, FocusEventArgs args) => model.ValidateName();
    private void OnPhoneUnfocused(object? sender, FocusEventArgs args) => model.ValidatePhone();
    private void OnEmailUnfocused(object? sender, FocusEventArgs args) => model.ValidateEmail();
    protected override bool OnBackButtonPressed() { if (!model.IsBusy) _ = UserFeedback.RunAsync(() => Close(null)); return true; }
}
