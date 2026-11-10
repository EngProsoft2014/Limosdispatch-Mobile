namespace LimosDispatch.Mobile;
public partial class App : Application {
readonly NavigationService navigation; readonly RealtimeService realtime;
public App(NavigationService navigation, RealtimeService realtime, AuthService auth, TrackingService tracking, ThemeService themes, PushService push){InitializeComponent();LocalizationService.Instance.Restore(Resources);themes.Restore(Resources);UserAppTheme=themes.Selected=="Dark"?AppTheme.Dark:AppTheme.Light;this.navigation=navigation;this.realtime=realtime;this.push=push;
LimosDispatch.Core.Helpers.UserFeedback.Presenter=FeedbackService.ShowErrorAsync;
TaskScheduler.UnobservedTaskException+=(_,e)=>{e.SetObserved();_=LimosDispatch.Core.Helpers.UserFeedback.ReportAsync(e.Exception.GetBaseException());};
auth.SignedOut+=()=>{if(navigation.IsRestoringSession)return;MainThread.BeginInvokeOnMainThread(()=>{_=LimosDispatch.Core.Helpers.UserFeedback.RunAsync(async()=>{try{await tracking.StopAsync(true);await realtime.ResetAsync();}finally{navigation.OpenLogin();}});});};}
protected override Window CreateWindow(IActivationState? state){
var window=new Window(navigation.CreateStartup());
window.Resumed+=(_,_)=>{_=LimosDispatch.Core.Helpers.UserFeedback.RunAsync(realtime.ResumeAsync);};
window.Resumed+=(_,_)=>{_=LimosDispatch.Core.Helpers.UserFeedback.RunAsync(push.ResumeAsync);};
window.Stopped+=(_,_)=>{_=LimosDispatch.Core.Helpers.UserFeedback.RunAsync(realtime.PauseAsync);};
return window;
}private readonly PushService push;}
