using System.Collections;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using Microsoft.Maui.Controls.Shapes;
namespace LimosDispatch.Mobile.Controls;

// A shared choice popup instead of a platform-dependent native picker dialog.
public sealed class ChoicePicker : ContentView
{
    private readonly Label label = new() { FontSize = 13, VerticalOptions = LayoutOptions.Center, LineBreakMode = LineBreakMode.TailTruncation };
    private bool open;
    public static readonly BindableProperty EmptyItemTextProperty=BindableProperty.Create(nameof(EmptyItemText),typeof(string),typeof(ChoicePicker),"",propertyChanged:Changed);
    public string EmptyItemText{get=>(string)GetValue(EmptyItemTextProperty);set=>SetValue(EmptyItemTextProperty,value);}
    public static readonly BindableProperty LocalizeItemsProperty = BindableProperty.Create(nameof(LocalizeItems), typeof(bool), typeof(ChoicePicker), true, propertyChanged: Changed);
    public bool LocalizeItems { get => (bool)GetValue(LocalizeItemsProperty); set => SetValue(LocalizeItemsProperty, value); }
    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(nameof(ItemsSource), typeof(IEnumerable), typeof(ChoicePicker), null, propertyChanged: Changed);
    public static readonly BindableProperty SelectedItemProperty = BindableProperty.Create(nameof(SelectedItem), typeof(object), typeof(ChoicePicker), null, BindingMode.TwoWay, propertyChanged: Changed);
    public static readonly BindableProperty TitleProperty = BindableProperty.Create(nameof(Title), typeof(string), typeof(ChoicePicker), "Select an option", propertyChanged: Changed);
    public static readonly BindableProperty DisplayMemberProperty = BindableProperty.Create(nameof(DisplayMember), typeof(string), typeof(ChoicePicker), "", propertyChanged: Changed);
    public static readonly BindableProperty TextColorProperty = BindableProperty.Create(nameof(TextColor), typeof(Color), typeof(ChoicePicker), null, propertyChanged: Changed);
    public Color? TextColor { get => (Color?)GetValue(TextColorProperty); set => SetValue(TextColorProperty, value); }
    public IEnumerable? ItemsSource { get => (IEnumerable?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    public object? SelectedItem { get => GetValue(SelectedItemProperty); set => SetValue(SelectedItemProperty, value); }
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string DisplayMember { get => (string)GetValue(DisplayMemberProperty); set => SetValue(DisplayMemberProperty, value); }
    private static void Changed(BindableObject sender, object oldValue, object newValue) => ((ChoicePicker)sender).RefreshText();
    private string TextFor(object item) => item is string empty&&empty.Length==0&&!string.IsNullOrEmpty(EmptyItemText)?EmptyItemText: string.IsNullOrEmpty(DisplayMember) ? (LocalizeItems ? LocalizationService.Text(item.ToString()) : item.ToString() ?? "") : item.GetType().GetProperty(DisplayMember)?.GetValue(item)?.ToString() ?? "";
    private void RefreshText()
    {
        label.Text = SelectedItem is null || SelectedItem is string text && string.IsNullOrWhiteSpace(text) ? Title : TextFor(SelectedItem);
        if (TextColor is not null) label.TextColor = TextColor;
        else label.SetDynamicResource(Label.TextColorProperty, SelectedItem is null ? "Muted" : "Ink");
        SemanticProperties.SetDescription(this, $"{Title}: {label.Text}");
    }
    private void OnLanguageChanged(object? sender,EventArgs e)=>RefreshText();
    public ChoicePicker()
    {
        LocalizationService.Instance.Changed+=OnLanguageChanged;
        HeightRequest = 44;
        label.FlowDirection = FlowDirection.LeftToRight;
        label.SetDynamicResource(Label.HorizontalTextAlignmentProperty, "FieldTextAlignment");
        var layout = new Grid { ColumnDefinitions = [new(GridLength.Star), new(new GridLength(20))], ColumnSpacing = 8 };
        layout.Add(label);
        layout.Add(new Image { Source = "chevron_down.png", WidthRequest = 18, HeightRequest = 18, VerticalOptions = LayoutOptions.Center }, 1);
        var tap = new TapGestureRecognizer(); tap.Tapped += (_, _) => { _ = LimosDispatch.Core.Helpers.UserFeedback.RunAsync(ShowChoices); }; layout.GestureRecognizers.Add(tap);
        Content = layout;
        RefreshText();
    }
    public Task OpenAsync() => ShowChoices();
    private async Task ShowChoices()
    {
        if (open || !IsEnabled) return;
        open = true;
        try
        {
            var values = ItemsSource?.Cast<object>().ToList() ?? [];
            var popup = new ChoicePopup(Title, values.Select(v => new ChoiceRow(v, TextFor(v), Equals(v, SelectedItem))).ToList());
            var nav = Application.Current!.Windows[0].Page!.Navigation;
            await nav.PushModalAsync(popup, false);
            var result = await popup.Result;
            if (result is not null) SelectedItem = result;
        }
        finally { open = false; }
    }
}

internal sealed record ChoiceRow(object Value, string Text, bool Selected);
internal sealed class ChoicePopup : ContentPage
{
    private readonly TaskCompletionSource<object?> completion = new();
    private readonly ObservableCollection<ChoiceRow> visible = [];
    private bool closing;
    public Task<object?> Result => completion.Task;
    public ChoicePopup(string title, List<ChoiceRow> rows)
    {
        BackgroundColor = Color.FromArgb("#99071018");
        Shell.SetNavBarIsVisible(this, false);
        var header = new Label { Text = title, FontSize = 19, FontFamily = LocalizationService.Instance.Code == "ar" ? "ElMessiriBold" : "OpenSansSemibold", VerticalOptions = LayoutOptions.Center };
        var cancel = new Button { Text = LocalizationService.Text("Close"), WidthRequest = 72, BackgroundColor = Colors.Transparent, FontSize = 12 };
        cancel.SetDynamicResource(Button.TextColorProperty, "AccentText"); cancel.Clicked += async (_, _) => await Close(null);
        var top = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Auto)] }; top.Add(header); top.Add(cancel, 1);
        var search = new SearchBar { Placeholder = LocalizationService.Text("Search options"), IsVisible = rows.Count > 6, HeightRequest = 44 };
        var list = new CollectionView
        {
            ItemsSource = visible, SelectionMode = SelectionMode.None,
            HeightRequest = Math.Clamp(rows.Count, 1, 7) * 48,
            EmptyView = new Label { Text = LocalizationService.Text("No matching options"), Padding = 16 },
            ItemTemplate = new DataTemplate(() =>
            {
                var name = new Label { FontSize = 14, VerticalOptions = LayoutOptions.Center }; name.SetBinding(Label.TextProperty, nameof(ChoiceRow.Text));
                var tick = new Label { Text = "✓", FontSize = 20, VerticalOptions = LayoutOptions.Center }; tick.SetDynamicResource(Label.TextColorProperty, "AccentText"); tick.SetBinding(IsVisibleProperty, nameof(ChoiceRow.Selected));
                var row = new Grid { ColumnDefinitions = [new(GridLength.Star), new(new GridLength(26))], Padding = new Thickness(12, 0), HeightRequest = 48 }; row.Add(name); row.Add(tick, 1);
                name.InputTransparent = true; tick.InputTransparent = true;
                var tap = new TapGestureRecognizer();
                tap.Tapped += (_, _) => { if (row.BindingContext is ChoiceRow choice) _ = LimosDispatch.Core.Helpers.UserFeedback.RunAsync(() => Close(choice.Value)); };
                row.GestureRecognizers.Add(tap);
                return row;
            })
        };
        void Filter(string? query) { visible.Clear(); foreach (var row in rows.Where(r => string.IsNullOrWhiteSpace(query) || r.Text.Contains(query, StringComparison.OrdinalIgnoreCase))) visible.Add(row); }
        search.TextChanged += (_, e) => Filter(e.NewTextValue);
        SizeChanged += (_, _) => list.HeightRequest = Math.Min(Math.Clamp(rows.Count, 1, 7) * 48, Math.Max(48, Height - 220));
        Filter("");
        var body = new VerticalStackLayout { Spacing = 12, Children = { top, search, list } };
        var panel = new Border { Content = body, Padding = 18, Margin = 20, StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 24 }, VerticalOptions = LayoutOptions.Center, MaximumWidthRequest = 520 };
        panel.SetDynamicResource(BackgroundColorProperty, "Surface");
        Content = new Grid { Children = { panel } };
    }
    private async Task Close(object? value)
    {
        if (closing) return; closing = true;
        try { await Navigation.PopModalAsync(false); completion.TrySetResult(value); }
        catch (Exception ex) { closing = false; await LimosDispatch.Core.Helpers.UserFeedback.ReportAsync(ex); }
    }
    protected override bool OnBackButtonPressed() { _ = Close(null); return true; }
    protected override void OnDisappearing() { base.OnDisappearing(); if (!closing) completion.TrySetResult(null); }
}
