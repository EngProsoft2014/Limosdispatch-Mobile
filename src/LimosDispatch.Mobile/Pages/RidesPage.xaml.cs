using LimosDispatch.Mobile.ViewModels;
namespace LimosDispatch.Mobile.Pages;
public partial class RidesPage:ContentPage {readonly RidesViewModel model; public RidesPage(RidesViewModel model){InitializeComponent();BindingContext=this.model=model;} protected override async void OnAppearing(){base.OnAppearing();model.Subscribe();await LimosDispatch.Core.Helpers.UserFeedback.RunAsync(model.LoadAsync);}protected override void OnDisappearing(){model.Unsubscribe();base.OnDisappearing();}}

