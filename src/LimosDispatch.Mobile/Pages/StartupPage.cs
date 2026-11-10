using LimosDispatch.Mobile.ViewModels;
namespace LimosDispatch.Mobile.Pages;

public partial class StartupPage : ContentPage
{
    private readonly StartupViewModel model;
    private bool requested;
    public StartupPage(StartupViewModel model)
    {
        InitializeComponent(); BindingContext = this.model = model;
        Loaded += OnPageLoaded;
    }
    private void OnPageLoaded(object? sender, EventArgs e)
    {
        if (requested) return; requested = true;
#if ANDROID
        if (Handler?.PlatformView is global::Android.Views.View native && native.ViewTreeObserver is { IsAlive: true } observer)
        {
            // Post session work after the first draw, so it cannot hold the native splash.
            FirstDraw? listener = null;
            listener = new FirstDraw(() => native.Post(async () =>
            {
                if (observer.IsAlive) observer.RemoveOnDrawListener(listener);
                listener?.Dispose();
                await model.LoadAsync();
            }));
            observer.AddOnDrawListener(listener); native.Invalidate();
            return;
        }
#endif
        Dispatcher.Dispatch(async () => await model.LoadAsync());
    }
    protected override bool OnBackButtonPressed()
    {
#if ANDROID
        if (!model.IsBusy) _ = FeedbackService.ConfirmExitAsync(() => Window?.Page == this, () => Application.Current?.Quit());
        return true;
#else
        return base.OnBackButtonPressed();
#endif
    }
#if ANDROID
    private sealed class FirstDraw(Action drawn) : Java.Lang.Object, global::Android.Views.ViewTreeObserver.IOnDrawListener
    {
        private bool fired;
        public void OnDraw() { if (fired) return; fired = true; drawn(); }
    }
#endif
}
