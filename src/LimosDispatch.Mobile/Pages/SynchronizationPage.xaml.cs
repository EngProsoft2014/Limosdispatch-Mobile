using LimosDispatch.Mobile.ViewModels;
namespace LimosDispatch.Mobile.Pages;
public partial class SynchronizationPage:ContentPage
{
    private readonly RealtimeService realtime;private bool visible;
    private readonly SynchronizationViewModel model;private readonly IDispatcherTimer timer;
    public SynchronizationPage(SynchronizationViewModel model,RealtimeService realtime)
    {
        InitializeComponent();this.realtime=realtime;this.model=model;BindingContext=model;
        Shell.SetTitleView(this,new Controls.CenteredBrandHeader());
        HeadingLabel.Text=LocalizationService.Text("Synchronization");
        DescriptionLabel.Text=LocalizationService.Text("Completed items disappear from this page after 24 hours. Pending journeys and items needing review stay visible.");
        SyncNowButton.Text=LocalizationService.Text("Sync now");EmptyLabel.Text=LocalizationService.Text("No saved journeys are waiting for synchronization.");
        timer=Dispatcher.CreateTimer();timer.Interval=TimeSpan.FromSeconds(5);timer.Tick+=async(_,_)=>{if(realtime.IsForeground&&visible)await model.RefreshInBackgroundAsync();};
    }
    protected override async void OnAppearing(){base.OnAppearing();visible=true;realtime.ForegroundChanged+=OnForeground;model.Subscribe();await model.LoadAsync();timer.Start();}
    private void OnForeground(bool foreground)=>Dispatcher.Dispatch(()=>{timer.Stop();if(foreground&&visible){_=model.RefreshInBackgroundAsync();timer.Start();}});
    protected override void OnDisappearing(){visible=false;timer.Stop();realtime.ForegroundChanged-=OnForeground;model.Unsubscribe();base.OnDisappearing();}
}
