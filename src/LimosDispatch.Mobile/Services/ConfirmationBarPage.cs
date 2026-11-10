using Microsoft.Maui.Controls.Shapes;
namespace LimosDispatch.Mobile.Services;

internal sealed class ConfirmationBarPage : ContentPage
{
    private readonly TaskCompletionSource<bool> result = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly IDispatcherTimer timer;
    private bool closing;
    public Task<bool> Result => result.Task;
    public ConfirmationBarPage(string message, string action)
    {
        BackgroundColor = Color.FromArgb("#44071018");
        Shell.SetNavBarIsVisible(this, false);
        var text = new Label { Text = LocalizationService.Text(message), FontSize = 14, Margin = new Thickness(0,0,0,12) };
        text.SetDynamicResource(Label.TextColorProperty,"Ink");
        text.SetDynamicResource(Label.FontFamilyProperty,"AppFontRegular");
        var cancel = new Button { Text = LocalizationService.Text("Cancel"), BackgroundColor = Colors.Transparent, HeightRequest = 44 };
        cancel.SetDynamicResource(Button.TextColorProperty,"Muted");
        var confirm = new Button { Text = LocalizationService.Text(action), HeightRequest = 44 };
        var buttons = new Grid { ColumnDefinitions = [new(GridLength.Star),new(GridLength.Star)], ColumnSpacing = 8 };
        buttons.Add(cancel,0); buttons.Add(confirm,1);
        var contents = new VerticalStackLayout { Spacing=0 }; contents.Add(text); contents.Add(buttons);
        var card = new Border { Content=contents,Padding=16,StrokeShape=new RoundRectangle { CornerRadius=16 },VerticalOptions=LayoutOptions.End };
        card.SetDynamicResource(Border.BackgroundColorProperty,"Surface"); card.SetDynamicResource(Border.StrokeProperty,"Line");
        Content = new Grid { Padding=new Thickness(16,16,16,32),Children={card} };
        timer = Dispatcher.CreateTimer(); timer.Interval=TimeSpan.FromSeconds(30);
        timer.Tick += (_,_) => { _ = LimosDispatch.Core.Helpers.UserFeedback.RunAsync(() => Close(false)); };
        cancel.Clicked += (_,_) => { _ = LimosDispatch.Core.Helpers.UserFeedback.RunAsync(() => Close(false)); };
        confirm.Clicked += (_,_) => { _ = LimosDispatch.Core.Helpers.UserFeedback.RunAsync(() => Close(true)); };
    }
    private async Task Close(bool accepted)
    {
        if (closing) return; closing=true; timer.Stop();
        try { await Navigation.PopModalAsync(false); result.TrySetResult(accepted); }
        catch { result.TrySetResult(false); throw; }
    }
    protected override void OnAppearing() { base.OnAppearing(); timer.Start(); }
    protected override void OnDisappearing() { timer.Stop(); if (!closing) result.TrySetResult(false); base.OnDisappearing(); }
    protected override bool OnBackButtonPressed() { _ = LimosDispatch.Core.Helpers.UserFeedback.RunAsync(() => Close(false)); return true; }
}
