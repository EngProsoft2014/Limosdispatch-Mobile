using Microsoft.Maui.Controls.Shapes;
using LimosDispatch.Core.Helpers;

namespace LimosDispatch.Mobile.Pages;

internal sealed class FleetActionsPopup : ContentPage
{
    private readonly INavigation navigation;
    private readonly TaskCompletionSource<string?> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool closing;
    public Task<string?> Result => completion.Task;

    public FleetActionsPopup(INavigation navigation, string title, IEnumerable<(string Key, string Label)> options)
    {
        this.navigation = navigation;
        BackgroundColor = Color.FromArgb("#99071018");
        Shell.SetNavBarIsVisible(this, false);
        var body = new VerticalStackLayout { Spacing = 8 };
        var heading = new Label { Text = title, FontSize = 18, FontAttributes = FontAttributes.Bold };
        heading.SetDynamicResource(Label.TextColorProperty, "Ink");
        heading.VerticalOptions = LayoutOptions.Center;
        var header = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Auto)], ColumnSpacing = 8 };
        header.Add(heading);
        var close = new Button { Text = "\u00D7", WidthRequest = 44, HeightRequest = 44, FontSize = 24, Padding = 0, CornerRadius = 12 };
        close.SetDynamicResource(Button.BackgroundColorProperty, "SoftSurface");
        close.SetDynamicResource(Button.TextColorProperty, "Ink");
        SemanticProperties.SetDescription(close, LocalizationService.Text("Close"));
        close.Clicked += async (_, _) => await UserFeedback.RunAsync(() => CloseAsync(null));
        header.Add(close, 1);
        body.Add(header);
        foreach (var (key, label) in options) Add(key, label);
        var panel = new Border { Content = body, Padding = 18, Margin = 20, StrokeThickness = 0,
            MaximumWidthRequest = 480, VerticalOptions = LayoutOptions.Center,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(22) } };
        panel.SetDynamicResource(BackgroundColorProperty, "Surface");
        Content = new Grid { Children = { panel } };

        void Add(string? key, string label)
        {
            var button = new Button { Text = label, HeightRequest = 44, CornerRadius = 12 };
            if (key == "delete")
            {
                button.SetDynamicResource(Button.BackgroundColorProperty, "FieldBackground");
                button.SetAppThemeColor(Button.TextColorProperty, Color.FromArgb("#B42318"), Color.FromArgb("#FFB4AB"));
            }
            else
            {
                button.SetDynamicResource(Button.BackgroundColorProperty, "SoftSurface");
                button.SetDynamicResource(Button.TextColorProperty, "Ink");
            }
            button.Clicked += async (_, _) => await UserFeedback.RunAsync(() => CloseAsync(key));
            body.Add(button);
        }
    }

    private async Task CloseAsync(string? value)
    {
        if (closing) return;
        closing = true;
        try
        {
            await navigation.PopModalAsync(false);
            completion.TrySetResult(value);
        }
        catch { closing = false; throw; }
    }

    protected override bool OnBackButtonPressed()
    {
        _ = UserFeedback.RunAsync(() => CloseAsync(null));
        return true;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (!closing) completion.TrySetResult(null);
    }
}
