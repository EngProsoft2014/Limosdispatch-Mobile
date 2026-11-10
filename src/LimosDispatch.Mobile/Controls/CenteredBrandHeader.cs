namespace LimosDispatch.Mobile.Controls;

// Center against the window, rather than the space left by back/account buttons.
public sealed class CenteredBrandHeader : Grid
{
    public bool ShowNotifications { get; set; } = true;
    private RideNotifications? inbox;
    private readonly Label badge = new() { FontSize = 10, TextColor = Colors.White, BackgroundColor = Color.FromArgb("#A14135"), Padding = new Thickness(4, 1), HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Start, InputTransparent = true };
    private void UpdateBadge() { badge.Text = inbox?.UnreadCount > 99 ? "99+" : inbox?.UnreadCount.ToString(); badge.IsVisible = ShowNotifications && inbox?.UnreadCount > 0; }
    private readonly Image logo = new() { Source = "brand_logo.png", WidthRequest = 180, HeightRequest = 44, Aspect = Aspect.AspectFit, HorizontalOptions = LayoutOptions.Center, InputTransparent = true };
    private readonly Label title = new() { TextColor = Colors.White, FontSize = 20, FontFamily = LocalizationService.Instance.Code == "ar" ? "ElMessiriBold" : "OpenSansSemibold", VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.Start, Margin = new Thickness(16, 0, 0, 0), InputTransparent = true };
    public static readonly BindableProperty TitleProperty = BindableProperty.Create(nameof(Title), typeof(string), typeof(CenteredBrandHeader), "",
        propertyChanged: (view, _, value) => { var header = (CenteredBrandHeader)view; header.title.Text = LocalizationService.Text((string)value); header.title.IsVisible = !string.IsNullOrEmpty((string)value); });
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public CenteredBrandHeader()
    {
        HeightRequest = 44; HorizontalOptions = LayoutOptions.Fill;
        title.IsVisible = false;
        title.SetDynamicResource(Label.FontFamilyProperty, "AppFontSemibold");
        Loaded += (_, _) => { LocalizationService.Instance.Changed += LanguageChanged; LanguageChanged(null, EventArgs.Empty); };
        Unloaded += (_, _) => LocalizationService.Instance.Changed -= LanguageChanged;
        Children.Add(logo); Children.Add(title);
        var bell = new ImageButton { Source = "bell.png", BackgroundColor = Colors.Transparent, WidthRequest = 44, HeightRequest = 44, Padding = 11, HorizontalOptions = LayoutOptions.End };
        SemanticProperties.SetDescription(bell, "Notifications");
        bell.Clicked += (_, _) => _ = LimosDispatch.Core.Helpers.UserFeedback.RunAsync(() => Handler!.MauiContext!.Services.GetRequiredService<NavigationService>().PushAsync<Pages.NotificationsPage>());
        Children.Add(bell); Children.Add(badge);
        SemanticProperties.SetDescription(logo, "LimosDispatch");
        SizeChanged += (_, _) => Dispatcher.Dispatch(CenterLogo);
        HandlerChanged += (_, _) => Dispatcher.Dispatch(CenterLogo);
        Loaded += (_, _) => { Dispatcher.Dispatch(CenterLogo); bell.IsVisible = ShowNotifications; badge.IsVisible = false; if (!ShowNotifications) return; inbox = Handler?.MauiContext?.Services.GetRequiredService<RideNotifications>(); if (inbox is not null) { inbox.Changed -= UpdateBadge; inbox.Changed += UpdateBadge; inbox.Start(); UpdateBadge(); } };
        Unloaded += (_, _) => { if (inbox is not null) inbox.Changed -= UpdateBadge; };
    }
    private void LanguageChanged(object? sender, EventArgs args) => title.Text = LocalizationService.Text(Title);
    private void CenterLogo()
    {
#if ANDROID
        if (Handler?.PlatformView is global::Android.Views.View native && native.Width > 0 &&
            Platform.CurrentActivity?.Window?.DecorView is { Width: > 0 } window)
        {
            var location = new int[2]; native.GetLocationInWindow(location);
            var density = native.Resources?.DisplayMetrics?.Density ?? 1;
            logo.TranslationX = (window.Width / 2d - location[0] - native.Width / 2d) / density;
        }
#elif IOS
        if (Handler?.PlatformView is UIKit.UIView native && native.Window is { } window && native.Bounds.Width > 0)
        {
            var frame = native.ConvertRectToView(native.Bounds, window);
            logo.TranslationX = window.Bounds.Width / 2d - frame.X - frame.Width / 2d;
        }
#endif
    }
}
