using LimosDispatch.Mobile.ViewModels;
namespace LimosDispatch.Mobile.Pages;
public partial class ExpenseDetailPage:ContentPage
{private readonly ExpenseDetailViewModel model;public ExpenseDetailPage(ExpenseDetailViewModel model){InitializeComponent();BindingContext=this.model=model;}protected override async void OnAppearing(){base.OnAppearing();await model.LoadAsync();}}
