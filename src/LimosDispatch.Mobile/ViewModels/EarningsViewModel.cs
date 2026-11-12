using System.Collections.ObjectModel;
using LimosDispatch.Core.GenericRepository;
namespace LimosDispatch.Mobile.ViewModels;
public sealed record DriverEarnings(string Name,decimal? Total,string Contract,string Error="");
public sealed class EarningsViewModel:BaseViewModel
{
 private readonly DispatchService dispatch;private readonly AuthService auth;private readonly FinanceService finance;private readonly NavigationService nav;private readonly StatementExportService export;
 private DateTime month=new(DateTime.Today.Year,DateTime.Today.Month,1);private Statement statement=new();private Driver? driver;private FinancialReport report=new();private CompanyInfo company=new();private bool selecting;private int request;
 public DateTime ReportFrom { get; set; } = new(DateTime.Today.Year, DateTime.Today.Month, 1);
 public DateTime ReportTo { get; set; } = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(1).AddDays(-1);
 public DateTime Month{get=>month;set=>Set(ref month,value);}public Statement Statement{get=>statement;private set{Set(ref statement,value);Raise(nameof(CanFinalize));}}public FinancialReport Report{get=>report;private set=>Set(ref report,value);}
 public ObservableCollection<Driver> Drivers{get;}=[];public ObservableCollection<DriverEarnings> DriverTotals{get;}=[];
 public Driver? SelectedDriver{get=>driver;set{if(Set(ref driver,value)&&!selecting)_=LoadStatement();}}
 public bool CanReport=>auth.User?.Can("Reports")==true;public bool CanViewDrivers=>auth.User?.Can("Payouts")==true;public bool CanManagePay=>auth.User?.Can("Payouts","Edit")==true;
 public bool CanFinalize=>CanManagePay&&!Statement.Finalized&&!Statement.NeedsReview&&(Statement.Rows.Count>0||Statement.IsSalary);
 public decimal Net=>Report.NetProfit;public decimal Revenue=>Report.Revenue;public decimal Wages=>Report.DriverWages;public decimal Expenses=>Report.Operating;
 public decimal Collected{get;private set;}public decimal Outstanding{get;private set;}public decimal PayrollPaid{get;private set;}public bool HasCollectionMetrics{get;private set;}
 public string FinancialNote=>LocalizationService.Text(Report.Note);public bool IsDispatcher=>CanReport;public bool HasIssues=>Report.Issues.Count>0;public string Issues=>string.Join("\n",Report.Issues);
 public AsyncCommand CsvCommand{get;}public AsyncCommand PdfCommand{get;}public AsyncCommand FinalizeCommand{get;}public AsyncCommand PaySystemsCommand{get;}public AsyncCommand BalancesCommand{get;}
 public EarningsViewModel(DispatchService dispatch,AuthService auth,FinanceService finance,NavigationService nav,StatementExportService export){this.dispatch=dispatch;this.auth=auth;this.finance=finance;this.nav=nav;this.export=export;RefreshCommand=new(LoadAsync);CsvCommand=new(()=>Run(()=>export.CsvAsync(Statement,company)));PdfCommand=new(()=>Run(()=>export.PdfAsync(Statement,company)));FinalizeCommand=new(()=>Run(async()=>{if(!await nav.ConfirmAsync("Finalize statement",LocalizationService.Text("Finalize this reviewed statement? Its rules and amounts will be preserved.")))return;await finance.FinalizeAsync(Statement);await FetchStatement();}));PaySystemsCommand=new(()=>Run(()=>nav.PushAsync<Pages.PaySystemsPage>()));BalancesCommand=new(()=>Run(()=>nav.PushAsync<Pages.DriverBalancesPage>()));}
 public override Task LoadAsync()=>Run(async()=>{
   var selected=driver?.PayrollId;selecting=true;try{if(CanViewDrivers){var list=await dispatch.DriversAsync();Drivers.Clear();foreach(var d in list.Where(d=>!string.IsNullOrWhiteSpace(d.PayrollId)))Drivers.Add(d);driver=Drivers.FirstOrDefault(d=>d.PayrollId==selected)??Drivers.FirstOrDefault();Raise(nameof(SelectedDriver));}}finally{selecting=false;}
   // The selected statement is shown before optional company summaries. No sequential N+1 requests.
   if(auth.User?.IsDriver==true||driver is not null)await FetchStatement();else Statement=new();
   if(CanReport){Report=await finance.ReportAsync(ReportFrom,ReportTo);Raise(string.Empty);}
   HasCollectionMetrics=false;
   if(CanReport&&auth.User?.Can("Reservations")==true&&auth.User.Can("Expenses")){var w=await finance.WorkspaceAsync();if(w.Data.ContainsKey("trips")&&w.Data.ContainsKey("expenses")){var from=ReportFrom.ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture);var to=ReportTo.ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture);bool InRange(string date)=>date.Length>=10&&string.CompareOrdinal(date[..10],from)>=0&&string.CompareOrdinal(date[..10],to)<=0;var trips=FinanceService.Read(w,"trips",new List<Trip>()).Where(t=>!t.IsTest&&InRange(t.Date)).ToList();Collected=trips.Where(t=>t.Paid=="Paid"&&t.Status is not("Cancelled" or "No-show")).Sum(t=>t.Price);Outstanding=trips.Where(t=>t.Status=="Completed"&&t.Paid is not("Paid" or "Refunded")).Sum(t=>t.Price);PayrollPaid=FinanceService.Read(w,"expenses",new List<Expense>()).Where(e=>InRange(e.Date)&&e.CostKind=="Driver payroll payment").Sum(e=>e.Amount);HasCollectionMetrics=true;}}Raise(string.Empty);
   if(CanViewDrivers){var key=Month.ToString("yyyy-MM");using var throttle=new SemaphoreSlim(3);var totals=await Task.WhenAll(Drivers.Select(async d=>{await throttle.WaitAsync();try{var s=(await dispatch.StatementAsync(key,d.PayrollId)).Statement;return new DriverEarnings(d.Name,s.Total,s.Contract);}catch(Exception ex){return new DriverEarnings(d.Name,null,"",LimosDispatch.Core.Helpers.UserFeedback.Message(ex));}finally{throttle.Release();}}));DriverTotals.Clear();foreach(var t in totals)DriverTotals.Add(t);}
 });
 private Task LoadStatement()=>Run(FetchStatement);
 private async Task FetchStatement(){var ticket=++request;var key=Month.ToString("yyyy-MM");var id=CanViewDrivers?SelectedDriver?.PayrollId:null;if(CanViewDrivers&&string.IsNullOrWhiteSpace(id)){Statement=new();return;}var result=await dispatch.StatementAsync(key,id);if(ticket!=request)return;Statement=result.Statement;company=result.Company;}
}
