using System.Windows.Input;
namespace LimosDispatch.Mobile.Controls;

public sealed class PasswordField : ContentView
{
    private readonly Entry input;
    private readonly ImageButton eye;
    public static readonly BindableProperty TextProperty = BindableProperty.Create(nameof(Text), typeof(string), typeof(PasswordField), "", BindingMode.TwoWay,
        propertyChanged: (view, _, value) => ((PasswordField)view).input.Text = (string?)value ?? "");
    public static readonly BindableProperty PlaceholderProperty = BindableProperty.Create(nameof(Placeholder), typeof(string), typeof(PasswordField), "Password",
        propertyChanged: (view, _, value) => ((PasswordField)view).input.Placeholder = (string)value);
    public static readonly BindableProperty ReturnCommandProperty = BindableProperty.Create(nameof(ReturnCommand), typeof(ICommand), typeof(PasswordField), null,
        propertyChanged: (view, _, value) => ((PasswordField)view).input.ReturnCommand = (ICommand?)value);
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public string Placeholder { get => (string)GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }
    public ICommand? ReturnCommand { get => (ICommand?)GetValue(ReturnCommandProperty); set => SetValue(ReturnCommandProperty, value); }
    public PasswordField()
    {
        input = new Entry { IsPassword = true, FlowDirection = FlowDirection.LeftToRight, HeightRequest = 44, MinimumHeightRequest = 0, BackgroundColor = Colors.Transparent, ReturnType = ReturnType.Go, IsTextPredictionEnabled = false, IsSpellCheckEnabled = false };
        input.TextChanged += (_, e) => { if (Text != e.NewTextValue) Text = e.NewTextValue ?? ""; };
        input.SetDynamicResource(Entry.HorizontalTextAlignmentProperty, "LatinFieldTextAlignment");
        eye = new ImageButton { Source = "eye.png", WidthRequest = 44, HeightRequest = 44, MinimumHeightRequest = 0, MinimumWidthRequest = 0, Padding = 11, BackgroundColor = Colors.Transparent };
        SemanticProperties.SetDescription(eye, "Show password");
        eye.Clicked += (_, _) =>
        {
            input.IsPassword = !input.IsPassword;
            eye.Source = input.IsPassword ? "eye.png" : "eye_off.png";
            SemanticProperties.SetDescription(eye, input.IsPassword ? "Show password" : "Hide password");
        };
        var grid = new Grid { ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(new GridLength(44))] };
        grid.Add(input); grid.Add(eye, 1);
        var border = new Border { Content = grid, Padding = new Thickness(12, 0, 0, 0), HeightRequest = 44, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 } };
        border.SetDynamicResource(BackgroundColorProperty, "FieldBackground");
        border.SetDynamicResource(Border.StrokeProperty, "Line");
        Content = border;
        HeightRequest = 44;
    }
}
