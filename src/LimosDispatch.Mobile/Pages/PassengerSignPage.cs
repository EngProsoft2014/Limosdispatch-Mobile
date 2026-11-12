namespace LimosDispatch.Mobile.Pages;
// Passenger sign is a name board, not a signature capture form.
public sealed class PassengerSignPage : ContentPage
{
    private readonly Label name;
    private bool previousWake;
    public PassengerSignPage(string passenger, ImageSource? companyLogo)
    {
        BackgroundColor = Color.FromArgb("#F6F0E5");
        Shell.SetNavBarIsVisible(this, false);
        name = new Label { Text = passenger.Trim(), TextColor = Color.FromArgb("#101820"), HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center, LineBreakMode = LineBreakMode.WordWrap, FontAutoScalingEnabled = false, FontAttributes = FontAttributes.Bold };
        name.SetDynamicResource(Label.FontFamilyProperty, "AppFontSemibold");
        var close = new Button { Text = "×", FontSize = 28, WidthRequest = 44, HeightRequest = 44, Padding = 0, BackgroundColor = Color.FromArgb("#253746"), TextColor = Colors.White, CornerRadius = 22, HorizontalOptions = LayoutOptions.End };
        close.Clicked += async (_, _) => await LimosDispatch.Core.Helpers.UserFeedback.RunAsync(async () => await Navigation.PopModalAsync());
        var layout = new Grid { Padding = 20, RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto)] };
        layout.Add(close, 0, 0);
        layout.Add(name, 0, 1);
        if (companyLogo is not null)
        {
            var logo = new Image
            {
                Source = companyLogo,
                Aspect = Aspect.AspectFit,
                WidthRequest = 124,
                HeightRequest = 48,
                HorizontalOptions = LayoutOptions.End
            };
            layout.Add(logo, 0, 2);
        }
        Content = layout;
        SizeChanged += (_, _) => name.FontSize = Math.Clamp(Math.Min(Width / Math.Max(6, passenger.Length / 2d), Height / 6), 24, 100);
    }
    protected override void OnAppearing() { base.OnAppearing(); previousWake = DeviceDisplay.Current.KeepScreenOn; DeviceDisplay.Current.KeepScreenOn = true; }
    protected override void OnDisappearing() { DeviceDisplay.Current.KeepScreenOn = previousWake; base.OnDisappearing(); }
}
