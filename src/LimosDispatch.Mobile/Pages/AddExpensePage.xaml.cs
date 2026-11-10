using LimosDispatch.Mobile.ViewModels;
namespace LimosDispatch.Mobile.Pages;
public partial class AddExpensePage:ContentPage {readonly AddExpenseViewModel model; public AddExpensePage(AddExpenseViewModel model){InitializeComponent();BindingContext=this.model=model;} protected override async void OnAppearing(){base.OnAppearing();await LimosDispatch.Core.Helpers.UserFeedback.RunAsync(model.LoadAsync);}}

