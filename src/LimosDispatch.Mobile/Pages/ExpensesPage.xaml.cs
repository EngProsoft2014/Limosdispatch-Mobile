using LimosDispatch.Mobile.ViewModels;
namespace LimosDispatch.Mobile.Pages;
public partial class ExpensesPage:ContentPage {readonly ExpensesViewModel model; public ExpensesPage(ExpensesViewModel model){InitializeComponent();BindingContext=this.model=model;} protected override async void OnAppearing(){base.OnAppearing();await LimosDispatch.Core.Helpers.UserFeedback.RunAsync(model.LoadAsync);}}

