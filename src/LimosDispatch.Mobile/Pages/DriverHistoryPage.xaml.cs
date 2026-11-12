using LimosDispatch.Mobile.ViewModels;
namespace LimosDispatch.Mobile.Pages;
public partial class DriverHistoryPage:ContentPage
{
    private readonly DriverHistoryViewModel model;private readonly IServiceProvider services;
    public DriverHistoryPage(DriverHistoryViewModel model,IServiceProvider services){InitializeComponent();BindingContext=this.model=model;this.services=services;Shell.SetTitleView(this,new Controls.CenteredBrandHeader{Title=""});}
    public void Configure(Driver driver)
    {
        loaded = false;
        model.Configure(driver);
    }
    private bool loaded;
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (loaded) return;
        await model.LoadAsync();
        loaded = true;
    }
    private async void OnTripTapped(object? sender,TappedEventArgs e){if(sender is BindableObject{BindingContext:Trip trip})await LimosDispatch.Core.Helpers.UserFeedback.RunAsync(async()=>{var page=services.GetRequiredService<RoutePlaybackPage>();page.Configure(trip);await Navigation.PushAsync(page);});}

    private async void OnFiltersClicked(object? sender, EventArgs e) =>
        await LimosDispatch.Core.Helpers.UserFeedback.RunAsync(async () =>
        {
        var from = new Controls.DateChoicePicker { Date = model.From, MaximumDate = model.Today };
        var to = new Controls.DateChoicePicker { Date = model.To, MaximumDate = model.Today };
        if (!await Controls.FilterDialog.ShowAsync(Navigation,
            Controls.FilterDialog.Pair(Controls.FilterDialog.Field("From", from), Controls.FilterDialog.Field("To", to)))) return;
        if (from.Date > to.Date) throw new InvalidOperationException("Choose a valid date range.");
        model.From = from.Date;
        model.To = to.Date;
        await model.LoadAsync();
        });
}
