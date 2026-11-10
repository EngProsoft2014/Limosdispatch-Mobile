using LimosDispatch.Mobile.ViewModels;
using LimosDispatch.Core.Helpers;
namespace LimosDispatch.Mobile.Pages;
public partial class AddressSearchPage : ContentPage
{
    private readonly AddressSearchViewModel model;
    private readonly TaskCompletionSource<GoogleAddress?> result = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool closing;
    public Task<GoogleAddress?> Result => result.Task;
    public AddressSearchPage(AddressSearchViewModel model) { InitializeComponent(); BindingContext = this.model = model; }
    public void Configure(string title) => Heading.Text = title;
    protected override void OnDisappearing() { model.Stop(); base.OnDisappearing(); if (!closing) result.TrySetResult(null); }
    private async void OnResultTapped(object? sender, TappedEventArgs args)
    {
        if (model.IsBusy || closing || sender is not BindableObject { BindingContext: AddressSuggestion suggestion }) return;
        await UserFeedback.RunAsync(async () => { model.IsBusy = true; try { var address = await model.SelectAsync(suggestion); if (!closing) await Close(address); } finally { model.IsBusy = false; } });
    }
    private async Task Close(GoogleAddress? value) { if (closing) return; closing = true; try { await Navigation.PopModalAsync(); result.TrySetResult(value); } catch { closing = false; throw; } }
    private async void OnCancel(object? sender, EventArgs args) => await UserFeedback.RunAsync(() => Close(null));
    protected override bool OnBackButtonPressed() { _ = UserFeedback.RunAsync(() => Close(null)); return true; }
}
