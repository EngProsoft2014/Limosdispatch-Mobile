using System.Globalization;
using Microsoft.Maui.Controls.Shapes;
namespace LimosDispatch.Mobile.Controls;

public sealed class DateChoicePicker : ContentView
{
    private readonly Label text = new() { FontSize = 13, VerticalOptions = LayoutOptions.Center };
    private bool open;
    public static readonly BindableProperty DateProperty = BindableProperty.Create(nameof(Date), typeof(DateTime), typeof(DateChoicePicker), DateTime.Today, BindingMode.TwoWay, propertyChanged: Changed);
    public static readonly BindableProperty MinimumDateProperty = BindableProperty.Create(nameof(MinimumDate), typeof(DateTime), typeof(DateChoicePicker), new DateTime(1900, 1, 1), propertyChanged: Changed);
    public static readonly BindableProperty MaximumDateProperty = BindableProperty.Create(nameof(MaximumDate), typeof(DateTime), typeof(DateChoicePicker), new DateTime(2100, 12, 31), propertyChanged: Changed);
    public static readonly BindableProperty FormatProperty = BindableProperty.Create(nameof(Format), typeof(string), typeof(DateChoicePicker), "dd MMM yyyy", propertyChanged: Changed);
    public static readonly BindableProperty TextColorProperty = BindableProperty.Create(nameof(TextColor), typeof(Color), typeof(DateChoicePicker), null, propertyChanged: Changed);
    public DateTime Date { get => (DateTime)GetValue(DateProperty); set => SetValue(DateProperty, value); }
    public DateTime MinimumDate { get => (DateTime)GetValue(MinimumDateProperty); set => SetValue(MinimumDateProperty, value); }
    public DateTime MaximumDate { get => (DateTime)GetValue(MaximumDateProperty); set => SetValue(MaximumDateProperty, value); }
    public string Format { get => (string)GetValue(FormatProperty); set => SetValue(FormatProperty, value); }
    public Color? TextColor { get => (Color?)GetValue(TextColorProperty); set => SetValue(TextColorProperty, value); }
    private void OnLanguageChanged(object? sender,EventArgs e)=>Update();
    private static void Changed(BindableObject sender, object oldValue, object newValue) => ((DateChoicePicker)sender).Update();
    private void Update()
    {
        text.Text = Date.ToString(Format, LocalizationService.Culture);
        if (TextColor is null) text.SetDynamicResource(Label.TextColorProperty, "Ink"); else text.TextColor = TextColor;
    }
    public DateChoicePicker()
    {
        LocalizationService.Instance.Changed+=OnLanguageChanged;
        HeightRequest = 44;
        text.FlowDirection = FlowDirection.LeftToRight;
        text.SetDynamicResource(Label.HorizontalTextAlignmentProperty, "FieldTextAlignment");
        var row = new Grid { ColumnDefinitions = [new(GridLength.Star), new(new GridLength(18))] }; row.Add(text);
        row.Add(new Image { Source = "calendar.png", HeightRequest = 18, WidthRequest = 18 }, 1);
        var tap = new TapGestureRecognizer(); tap.Tapped += async (_, _) =>
        {
            if (open) return; open = true;
            try
            {
                var popup = new CalendarPopup(Date, MinimumDate, MaximumDate);
                await Application.Current!.Windows[0].Page!.Navigation.PushModalAsync(popup, false);
                var result = await popup.Result; if (result is not null) Date = result.Value;
            }
            catch (Exception ex) { await LimosDispatch.Core.Helpers.UserFeedback.ReportAsync(ex); }
            finally { open = false; }
        };
        row.GestureRecognizers.Add(tap); Content = row; Update();
    }
}

internal sealed class CalendarPopup : ContentPage
{
    private readonly TaskCompletionSource<DateTime?> completion = new();
    private readonly DateTime selected, minimum, maximum;
    private DateTime month;
    private readonly Grid days = new() { ColumnSpacing = 0, RowSpacing = 2 };
    private readonly Label monthTitle = new() { FontSize = 17, FontFamily = LocalizationService.Instance.Code == "ar" ? "ElMessiriBold" : "OpenSansSemibold", HorizontalTextAlignment = TextAlignment.Center, VerticalOptions = LayoutOptions.Center };
    private readonly Button previous = new() { Text = "‹", WidthRequest = 44, FontSize = 24, BackgroundColor = Colors.Transparent };
    private readonly Button next = new() { Text = "›", WidthRequest = 44, FontSize = 24, BackgroundColor = Colors.Transparent };
    private bool closing;
    public Task<DateTime?> Result => completion.Task;
    public CalendarPopup(DateTime selected, DateTime minimum, DateTime maximum)
    {
        this.minimum = minimum.Date; this.maximum = maximum.Date;
        this.selected = selected.Date < minimum.Date ? minimum.Date : selected.Date > maximum.Date ? maximum.Date : selected.Date;
        month = new(this.selected.Year, this.selected.Month, 1);
        BackgroundColor = Color.FromArgb("#99071018"); Shell.SetNavBarIsVisible(this, false);
        previous.SetDynamicResource(Button.TextColorProperty, "AccentText"); next.SetDynamicResource(Button.TextColorProperty, "AccentText");
        previous.Clicked += (_, _) => { month = month.AddMonths(-1); Render(); };
        next.Clicked += (_, _) => { month = month.AddMonths(1); Render(); };
        var navigation = new Grid { ColumnDefinitions = [new(new GridLength(44)), new(GridLength.Star), new(new GridLength(44))] }; navigation.Add(previous); navigation.Add(monthTitle, 1); navigation.Add(next, 2);
        var week = new Grid(); for (var c = 0; c < 7; c++) { days.ColumnDefinitions.Add(new(GridLength.Star)); week.ColumnDefinitions.Add(new(GridLength.Star)); week.Add(new Label { Text = LocalizationService.Culture.DateTimeFormat.AbbreviatedDayNames[c], FontSize = 10, HorizontalTextAlignment = TextAlignment.Center }, c); }
        var cancel = new Button { Text = LocalizationService.Text("Cancel"), BackgroundColor = Colors.Transparent }; cancel.SetDynamicResource(Button.TextColorProperty, "AccentText"); cancel.Clicked += async (_, _) => await Close(null);
        var today = new Button { Text = LocalizationService.Text("Today"), IsEnabled = DateTime.Today >= this.minimum && DateTime.Today <= this.maximum }; today.Clicked += async (_, _) => await Close(DateTime.Today);
        var footer = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Star)], ColumnSpacing = 12 }; footer.Add(cancel); footer.Add(today, 1);
        var content = new VerticalStackLayout { Spacing = 12, Children = { new Label { Text = LocalizationService.Text("Select date"), FontSize = 19, FontFamily = LocalizationService.Instance.Code == "ar" ? "ElMessiriBold" : "OpenSansSemibold" }, navigation, week, days, footer } };
        var panel = new Border { Content = content, Padding = 16, Margin = 20, StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 24 }, VerticalOptions = LayoutOptions.Center, MaximumWidthRequest = 440 }; panel.SetDynamicResource(BackgroundColorProperty, "Surface"); Content = new Grid { Children = { panel } }; Render();
    }
    private void Render()
    {
        monthTitle.Text = month.ToString("MMMM yyyy",LocalizationService.Culture); days.Children.Clear(); days.RowDefinitions.Clear();
        var offset = (int)month.DayOfWeek; var count = DateTime.DaysInMonth(month.Year, month.Month);
        for (var r = 0; r < (offset + count + 6) / 7; r++) days.RowDefinitions.Add(new(new GridLength(44)));
        previous.IsEnabled = month > new DateTime(minimum.Year, minimum.Month, 1);
        next.IsEnabled = month < new DateTime(maximum.Year, maximum.Month, 1);
        for (var day = 1; day <= count; day++)
        {
            var date = new DateTime(month.Year, month.Month, day);
            var button = new Button { Text = day.ToString(), HeightRequest = 44, MinimumWidthRequest = 0, Padding = 0, FontSize = 13, BackgroundColor = Colors.Transparent, IsEnabled = date >= minimum && date <= maximum, Opacity = date >= minimum && date <= maximum ? 1 : 0.25 };
            button.SetDynamicResource(Button.TextColorProperty, "Ink");
            if (date == selected) { button.SetDynamicResource(Button.BackgroundColorProperty, "Gold"); button.TextColor = Color.FromArgb("#111D28"); }
            button.Clicked += async (_, _) => await Close(date);
            days.Add(button, (offset + day - 1) % 7, (offset + day - 1) / 7);
        }
    }
    private async Task Close(DateTime? date) { if (closing) return; closing = true; try { await Navigation.PopModalAsync(false); completion.TrySetResult(date); } catch (Exception ex) { closing = false; await LimosDispatch.Core.Helpers.UserFeedback.ReportAsync(ex); } }
    protected override bool OnBackButtonPressed() { _ = Close(null); return true; }
    protected override void OnDisappearing() { base.OnDisappearing(); if (!closing) completion.TrySetResult(null); }
}
