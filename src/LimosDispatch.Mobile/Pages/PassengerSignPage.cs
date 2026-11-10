namespace LimosDispatch.Mobile.Pages;
// Passenger sign is a name board, not a signature capture form.
public sealed class PassengerSignPage : ContentPage
{
    private readonly Label name;
    private bool previousWake;
    public PassengerSignPage(string passenger)
    {
        BackgroundColor = Color.FromArgb("#B2D2F4");
        Shell.SetNavBarIsVisible(this, false);
        name = new Label { Text = passenger.Trim(), TextColor = Color.FromArgb("#101820"), HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center, LineBreakMode = LineBreakMode.WordWrap, FontAutoScalingEnabled = false };
        name.SetDynamicResource(Label.FontFamilyProperty, "AppFontSemibold");
        var close = new Button { Text = "×", FontSize = 28, WidthRequest = 44, HeightRequest = 44, Padding = 0, BackgroundColor = Color.FromArgb("#253746"), TextColor = Colors.White, CornerRadius = 22, HorizontalOptions = LayoutOptions.End };
        close.Clicked += async (_, _) => await LimosDispatch.Core.Helpers.UserFeedback.RunAsync(async () => await Navigation.PopModalAsync());
        var layout = new Grid { Padding = 20, RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(new GridLength(1, GridUnitType.Star)), new RowDefinition(new GridLength(3, GridUnitType.Star))] };
        layout.Add(close, 0, 0);
        layout.Add(new Image { Source = "brand_logo.png", Aspect = Aspect.AspectFit, MaximumHeightRequest = 150 }, 0, 1);
        layout.Add(name, 0, 2); Content = layout;
        SizeChanged += (_, _) => name.FontSize = Math.Clamp(Math.Min(Width / Math.Max(6, passenger.Length / 2d), Height / 6), 24, 100);
    }
    protected override void OnAppearing() { base.OnAppearing(); previousWake = DeviceDisplay.Current.KeepScreenOn; DeviceDisplay.Current.KeepScreenOn = true; }
    protected override void OnDisappearing() { DeviceDisplay.Current.KeepScreenOn = previousWake; base.OnDisappearing(); }
}
