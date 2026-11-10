using System.Collections.ObjectModel;
using System.Text.Json;
namespace LimosDispatch.Mobile.ViewModels;
public sealed class DriverBalancesViewModel:BaseViewModel
{
 readonly FinanceService finance;readonly NavigationService nav;long version;FinanceChoice? driver,system;string search="",amount="";List<FinanceChoice> all=[];
 public ObservableCollection<FinanceChoice> Drivers{get;}=[];public ObservableCollection<FinanceChoice> Systems{get;}=[];public FinanceChoice? SelectedDriver{get=>driver;set{Set(ref driver,value);SelectedSystem=Systems.FirstOrDefault(s=>s.Id==S(driver?.Record,"paySystemId"));Raise(nameof(Balance));}}public FinanceChoice? SelectedSystem{get=>system;set=>Set(ref system,value);}public string Search{get=>search;set{Set(ref search,value);Filter();}}public string Amount{get=>amount;set=>Set(ref amount,value);}public decimal Balance=>decimal.TryParse(S(driver?.Record,"balance"),System.Globalization.NumberStyles.Number,System.Globalization.CultureInfo.InvariantCulture,out var n)?n:0;
 public AsyncCommand AssignCommand{get;}public AsyncCommand SettleCommand{get;}
 public DriverBalancesViewModel(FinanceService finance,NavigationService nav){this.finance=finance;this.nav=nav;RefreshCommand=new(LoadAsync);AssignCommand=new(()=>Run(Assign));SettleCommand=new(()=>Run(Settle));}
 static string S(JsonElement? r,string key)=>r is {} o&&o.ValueKind==JsonValueKind.Object&&o.TryGetProperty(key,out var p)?p.ToString():"";
 public override Task LoadAsync()=>Run(Reload);
 async Task Reload(){finance.Demand("Payouts","Edit");var w=await finance.WorkspaceAsync();version=w.Version;all=FinanceService.Read(w,"drivers",new List<JsonElement>()).Select(r=>new FinanceChoice(FinanceService.Id(r),S(r,"name"),r)).ToList();Systems.Clear();foreach(var r in FinanceService.Read(w,"driverPaySystems",new List<JsonElement>()))Systems.Add(new(FinanceService.Id(r),S(r,"name"),r));var old=SelectedDriver?.Id;Filter();SelectedDriver=all.FirstOrDefault(d=>d.Id==old);}
 void Filter(){Drivers.Clear();foreach(var d in all.Where(d=>d.Name.Contains(Search,StringComparison.CurrentCultureIgnoreCase)))Drivers.Add(d);}
 async Task Assign(){if(SelectedDriver is null||SelectedSystem is null)throw new InvalidOperationException("Select a chauffeur and pay system.");if(!await nav.ConfirmAsync("Assign pay system",LocalizationService.Text("Apply this pay system to the selected chauffeur?")))return;await finance.UpdateAsync("drivers",SelectedDriver.Id,SelectedDriver.Record,new(){["paySystemId"]=SelectedSystem.Id},version,"Payouts");await Reload();}
 async Task Settle(){if(SelectedDriver is null)throw new InvalidOperationException("Select a chauffeur.");var n=PaySystemsViewModel.Number(Amount);if(n<=0||n>Balance)throw new InvalidOperationException("Settlement must be positive and cannot exceed the outstanding balance.");if(!await nav.ConfirmAsync("Record settlement",LocalizationService.Text("Record this legacy balance settlement? No money transfer is made.")))return;await finance.UpdateAsync("drivers",SelectedDriver.Id,SelectedDriver.Record,new(){["balance"]=Balance-n},version,"Payouts");Amount="";await Reload();}
}
