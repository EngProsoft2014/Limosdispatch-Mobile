using Microsoft.Maui.Controls.Shapes;
namespace LimosDispatch.Mobile.Controls;

public sealed class LoadingDots : ContentView
{
    private readonly Ellipse[] dots;
    private IDispatcherTimer? timer;
    private bool loaded;
    private int active;
    public static readonly BindableProperty IsRunningProperty = BindableProperty.Create(nameof(IsRunning), typeof(bool), typeof(LoadingDots), false,
        propertyChanged: (view, _, _) => ((LoadingDots)view).UpdateTimer());
    public bool IsRunning { get => (bool)GetValue(IsRunningProperty); set => SetValue(IsRunningProperty, value); }
    public LoadingDots()
    {
        HeightRequest = 24; InputTransparent = true;
        dots = Enumerable.Range(0, 4).Select(_ => new Ellipse { WidthRequest = 8, HeightRequest = 8, Fill = new SolidColorBrush(Color.FromArgb("#B99B62")), Opacity = 0.2 }).ToArray();
        var row = new HorizontalStackLayout { Spacing = 9, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
        foreach (var dot in dots) row.Children.Add(dot);
        Content = row;
        Loaded += (_, _) => { loaded = true; UpdateTimer(); };
        Unloaded += (_, _) => { loaded = false; timer?.Stop(); };
        HandlerChanged += (_, _) => { if (Handler is null) timer?.Stop(); };
    }
    private void UpdateTimer()
    {
        if (!loaded || !IsRunning) { timer?.Stop(); return; }
        if (timer is null)
        {
            timer = Dispatcher.CreateTimer(); timer.Interval = TimeSpan.FromMilliseconds(180);
            timer.Tick += (_, _) =>
            {
                if (!loaded || !IsRunning) { timer.Stop(); return; }
                active = (active + 1) % dots.Length; Paint();
            };
        }
        active = 0; Paint(); timer.Start();
    }
    private void Paint()
    {
        for (var i = 0; i < dots.Length; i++) dots[i].Opacity = i == active ? 1 : i == (active + dots.Length - 1) % dots.Length ? 0.5 : 0.2;
    }
}
