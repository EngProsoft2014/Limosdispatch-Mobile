using System.Text.Json;
using LimosDispatch.Core.GenericRepository;
namespace LimosDispatch.Mobile.ViewModels;
public sealed class ExpenseDetailViewModel(FinanceService finance,AuthService auth,NavigationService nav):BaseViewModel
{
    public Expense Expense{get;private set;}=new();private long version;private JsonElement original;
    public bool CanEdit=>auth.User?.Can("Expenses","Edit")==true;public bool CanDelete=>auth.User?.Can("Expenses","Delete")==true;
    public AsyncCommand EditCommand=>new(()=>Run(()=>nav.EditExpenseAsync(Expense)));public AsyncCommand DeleteCommand=>new(()=>Run(Delete));public AsyncCommand ReceiptCommand=>new(()=>Run(async()=>{if(Expense.Receipt is not null)await Application.Current!.Windows[0].Page!.Navigation.PushAsync(new Pages.ReceiptPage(Expense.Receipt));}));
    public void Configure(Expense e){LimosDispatch.Core.Helpers.ExpenseAccess.Demand(auth.User??throw new UnauthorizedAccessException(),e.Driver);Expense=e;Raise(string.Empty);}
    public override Task LoadAsync()=>Run(async()=>{var records=await finance.RecordsAsync("expenses");version=records.Version;original=records.Rows.FirstOrDefault(e=>FinanceService.Id(e)==Expense.Id);Expense=original.ValueKind==JsonValueKind.Object?original.Deserialize<Expense>(ApiRepository.Json)!:throw new InvalidOperationException("This expense is no longer available.");LimosDispatch.Core.Helpers.ExpenseAccess.Demand(auth.User??throw new UnauthorizedAccessException(),Expense.Driver);Raise(string.Empty);});
    private async Task Delete(){if(!await nav.ConfirmAsync("Delete expense",LocalizationService.Format("Delete expense {0}? This cannot be undone.",Expense.Name)))return;await finance.DeleteAsync("expenses",Expense.Id,original,version,"Expenses");await nav.PopAsync();}
}
