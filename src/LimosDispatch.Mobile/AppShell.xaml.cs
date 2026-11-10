using LimosDispatch.Mobile.Pages;
namespace LimosDispatch.Mobile;
public partial class AppShell:Shell {
private EventHandler? languageChanged;
public AppShell(IServiceProvider services,UserProfile user){
InitializeComponent();
Loaded += (_, _) => RefreshTabFonts();
Navigated += (_, _) => RefreshTabFonts();
LocalizationService.Instance.Changed += OnFontLanguageChanged;
RidesTab.ContentTemplate=new DataTemplate(()=>services.GetRequiredService<RidesPage>());
ExpensesTab.ContentTemplate=new DataTemplate(()=>services.GetRequiredService<ExpensesPage>());
EarningsTab.ContentTemplate=new DataTemplate(()=>services.GetRequiredService<EarningsPage>());
FourthTab.Title=user.CanDispatch?"Tracking":"More"; FourthTab.Icon=user.CanDispatch?"tracking.png":"more.png";
FourthTab.ContentTemplate=new DataTemplate(()=>user.CanDispatch?(Page)services.GetRequiredService<TrackingPage>():services.GetRequiredService<MoreMenuPage>());
if(user.CanDispatch)((TabBar)Items[0]).Items.Add(new Tab{Title = LocalizationService.Text("More"),Icon="more.png",Items={new ShellContent{Title = LocalizationService.Text("More"),ContentTemplate=new DataTemplate(()=>services.GetRequiredService<MoreMenuPage>())}}});
void Labels(object? sender, EventArgs args) { RidesTab.Title=LocalizationService.Text("Rides"); ExpensesTab.Title=LocalizationService.Text("Expenses"); EarningsTab.Title=LocalizationService.Text("Earnings"); FourthTab.Title=LocalizationService.Text(user.CanDispatch?"Tracking":"More"); if(user.CanDispatch)((TabBar)Items[0]).Items[4].Title=LocalizationService.Text("More"); }
languageChanged=Labels; LocalizationService.Instance.Changed+=languageChanged; Labels(null,EventArgs.Empty);
}
private void OnFontLanguageChanged(object? sender, EventArgs args) => RefreshTabFonts();
private void RefreshTabFonts() => Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(120), NativeTabFonts.Apply);
protected override bool OnBackButtonPressed(){
#if ANDROID
var page=CurrentPage;
if(page is RoutePlaybackPage playback&&playback.TryExitFullScreen())return true;
if(Navigation.ModalStack.Count>0||CurrentItem?.CurrentItem?.Navigation.NavigationStack.Count>1)return base.OnBackButtonPressed();
if(page is TrackingPage tracking&&tracking.TryExitFullScreen())return true;
if(page?.BindingContext is ViewModels.BaseViewModel {IsBusy:true})return true;
_=FeedbackService.ConfirmExitAsync(()=>Window?.Page==this&&CurrentPage==page&&Navigation.ModalStack.Count==0&&CurrentItem?.CurrentItem?.Navigation.NavigationStack.Count<=1,()=>Application.Current?.Quit());
return true;
#else
return base.OnBackButtonPressed();
#endif
}
}
