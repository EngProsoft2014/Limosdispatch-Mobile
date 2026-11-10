using System.Collections.ObjectModel;
using System.Windows.Input;
namespace LimosDispatch.Mobile.ViewModels;
public sealed class ExpenseCategoriesViewModel(FinanceService finance,AuthService auth,NavigationService nav):BaseViewModel
{
    private List<string> all=[];private string name="",oldName="",query="";public string Name{get=>name;set=>Set(ref name,value);}public string Search{get=>query;set{Set(ref query,value);Apply();}}
    public bool CanAdd=>auth.User?.Can("Expense categories","Add")==true;public bool CanEdit=>auth.User?.Can("Expense categories","Edit")==true;public bool CanDelete=>auth.User?.Can("Expense categories","Delete")==true;public bool CanSave=>oldName==""?CanAdd:CanEdit;
    public string SaveLabel=>LocalizationService.Text(oldName==""?"Add category":"Rename category");public ObservableCollection<string> Categories{get;}=[];
    public ICommand SelectCommand=>new Command<string>(value=>{if(CanEdit){oldName=value;Name=value;Raise(nameof(SaveLabel));Raise(nameof(CanSave));}});
    public AsyncCommand NewCommand=>new(()=>{oldName="";Name="";Raise(nameof(SaveLabel));Raise(nameof(CanSave));return Task.CompletedTask;});
    public AsyncCommand SaveCommand=>new(()=>Run(async()=>{all=await finance.CategoryAsync(oldName==""?"Add":"Rename",Name,oldName);oldName="";Name="";Apply();Raise(string.Empty);}));
    public ICommand DeleteCommand=>new Command<string>(value=>{_=Run(async()=>{if(await nav.ConfirmAsync("Delete category",LocalizationService.Format("Delete category {0}?",value))){all=await finance.CategoryAsync("Delete",value,value);Apply();}});});
    public override Task LoadAsync()=>Run(async()=>{finance.Demand("Expense categories");var w=await finance.WorkspaceAsync();all=FinanceService.Read(w,"expenseCategories",new List<string>());if(all.Count==0)all=FinanceService.Read(w,"expenseLookups",new ExpenseLookups()).Categories;Apply();});
    private void Apply(){Categories.Clear();foreach(var c in all.Where(c=>string.IsNullOrWhiteSpace(Search)||c.Contains(Search,StringComparison.CurrentCultureIgnoreCase)).Order())Categories.Add(c);}
}
