using LimosDispatch.Mobile.ViewModels;
namespace LimosDispatch.Mobile.Pages;
public partial class DriverHistoryPage:ContentPage
{
    private readonly DriverHistoryViewModel model;private readonly IServiceProvider services;
    public DriverHistoryPage(DriverHistoryViewModel model,IServiceProvider services){InitializeComponent();BindingContext=this.model=model;this.services=services;Shell.SetTitleView(this,new Controls.CenteredBrandHeader{Title=""});}
    public void Configure(Driver driver)=>model.Configure(driver);
    protected override async void OnAppearing(){base.OnAppearing();await model.LoadAsync();}
    private async void OnTripTapped(object? sender,TappedEventArgs e){if(sender is BindableObject{BindingContext:Trip trip})await LimosDispatch.Core.Helpers.UserFeedback.RunAsync(async()=>{var page=services.GetRequiredService<RoutePlaybackPage>();page.Configure(trip);await Navigation.PushAsync(page);});}
}
