using LimosDispatch.Mobile.ViewModels;
namespace LimosDispatch.Mobile.Pages;
public partial class ExpenseCategoriesPage:ContentPage
{private readonly ExpenseCategoriesViewModel model;public ExpenseCategoriesPage(ExpenseCategoriesViewModel model){InitializeComponent();BindingContext=this.model=model;}protected override async void OnAppearing(){base.OnAppearing();await model.LoadAsync();}}
