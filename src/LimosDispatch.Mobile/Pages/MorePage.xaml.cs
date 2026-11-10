using LimosDispatch.Mobile.ViewModels;
namespace LimosDispatch.Mobile.Pages;
public partial class MorePage:ContentPage {readonly MoreViewModel model; public MorePage(MoreViewModel model){InitializeComponent();BindingContext=this.model=model;} protected override async void OnAppearing(){base.OnAppearing();await LimosDispatch.Core.Helpers.UserFeedback.RunAsync(model.LoadAsync);}}

