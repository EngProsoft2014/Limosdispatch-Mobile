using Microsoft.Maui.Controls.Shapes;
namespace LimosDispatch.Mobile.Controls;

public sealed class TimeChoicePicker : ContentView
{
    private readonly Label text = new() { FontSize = 13, VerticalOptions = LayoutOptions.Center };
    private bool open;
    public static readonly BindableProperty TimeProperty = BindableProperty.Create(nameof(Time), typeof(TimeSpan), typeof(TimeChoicePicker), TimeSpan.Zero, BindingMode.TwoWay,
        propertyChanged: (view, _, value) => ((TimeChoicePicker)view).text.Text = DateTime.Today.Add((TimeSpan)value).ToString("HH:mm"));
    public TimeSpan Time { get => (TimeSpan)GetValue(TimeProperty); set => SetValue(TimeProperty, value); }
    public TimeChoicePicker()
    {
        HeightRequest = 44; text.SetDynamicResource(Label.TextColorProperty, "Ink"); text.Text = "00:00";
        text.FlowDirection = FlowDirection.LeftToRight;
        text.SetDynamicResource(Label.HorizontalTextAlignmentProperty, "FieldTextAlignment");
        var row = new Grid { ColumnDefinitions = [new(GridLength.Star), new(new GridLength(18))] }; row.Add(text); row.Add(new Image { Source = "clock.png", WidthRequest = 18, HeightRequest = 18 }, 1);
        var tap = new TapGestureRecognizer(); tap.Tapped += async (_, _) =>
        {
            if (open) return; open = true;
            try
            {
                var popup = new TimePopup(Time);
                await Application.Current!.Windows[0].Page!.Navigation.PushModalAsync(popup, false);
                var time = await popup.Result; if (time is not null) Time = time.Value;
            }
            catch (Exception ex) { await LimosDispatch.Core.Helpers.UserFeedback.ReportAsync(ex); }
            finally { open = false; }
        };
        row.GestureRecognizers.Add(tap); Content = row;
    }
}

internal sealed class TimePopup : ContentPage
{
    private readonly TaskCompletionSource<TimeSpan?> completion = new();
    private bool closing;
    public Task<TimeSpan?> Result => completion.Task;
    public TimePopup(TimeSpan current)
    {
        BackgroundColor = Color.FromArgb("#99071018"); Shell.SetNavBarIsVisible(this, false);
        var hours = new ChoicePicker { Title = LocalizationService.Text("Hour (00–23)"), ItemsSource = Enumerable.Range(0, 24).Select(n => n.ToString("00")).ToList(), SelectedItem = current.Hours.ToString("00") };
        var minutes = new ChoicePicker { Title = LocalizationService.Text("Minute (00–59)"), ItemsSource = Enumerable.Range(0, 60).Select(n => n.ToString("00")).ToList(), SelectedItem = current.Minutes.ToString("00") };
        Border Field(ChoicePicker picker)
        {
            var border = new Border { Content = picker, Padding = new Thickness(12, 0), StrokeShape = new RoundRectangle { CornerRadius = 12 } };
            border.SetDynamicResource(BackgroundColorProperty, "FieldBackground"); border.SetDynamicResource(Border.StrokeProperty, "Line"); return border;
        }
        var fields = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Star)], ColumnSpacing = 12 };
        fields.Add(new VerticalStackLayout { Spacing = 8, Children = { new Label { Text = LocalizationService.Text("Hour"), FontSize = 12 }, Field(hours) } });
        fields.Add(new VerticalStackLayout { Spacing = 8, Children = { new Label { Text = LocalizationService.Text("Minute"), FontSize = 12 }, Field(minutes) } }, 1);
        var cancel = new Button { Text = LocalizationService.Text("Cancel"), BackgroundColor = Colors.Transparent }; cancel.SetDynamicResource(Button.TextColorProperty, "AccentText"); cancel.Clicked += async (_, _) => await Close(null);
        var save = new Button { Text = LocalizationService.Text("Set time") }; save.Clicked += async (_, _) => await Close(new TimeSpan(int.Parse((string)hours.SelectedItem!), int.Parse((string)minutes.SelectedItem!), 0));
        var footer = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Star)], ColumnSpacing = 12 }; footer.Add(cancel); footer.Add(save, 1);
        var panel = new Border { Padding = 20, Margin = 20, StrokeThickness = 0, MaximumWidthRequest = 440, VerticalOptions = LayoutOptions.Center, StrokeShape = new RoundRectangle { CornerRadius = 24 }, Content = new VerticalStackLayout { Spacing = 20, Children = { new Label { Text = LocalizationService.Text("Select time"), FontSize = 19, FontFamily = LocalizationService.Instance.Code == "ar" ? "ElMessiriBold" : "OpenSansSemibold" }, fields, new Label { Text = LocalizationService.Text("24-hour time"), FontSize = 11 }, footer } } };
        panel.SetDynamicResource(BackgroundColorProperty, "Surface"); Content = new Grid { Children = { panel } };
    }
    private async Task Close(TimeSpan? time) { if (closing) return; closing = true; try { await Navigation.PopModalAsync(false); completion.TrySetResult(time); } catch (Exception ex) { closing = false; await LimosDispatch.Core.Helpers.UserFeedback.ReportAsync(ex); } }
    protected override bool OnBackButtonPressed() { _ = Close(null); return true; }
}
