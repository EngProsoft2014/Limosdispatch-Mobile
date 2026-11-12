using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using LimosDispatch.Core.GenericRepository;
using LimosDispatch.Core.Helpers;
namespace LimosDispatch.Mobile.ViewModels;
public sealed class AddExpenseViewModel:BaseViewModel
{
    private readonly DispatchService dispatch;private readonly ReceiptService receipts;private readonly AuthService auth;private readonly NavigationService nav;private readonly FinanceService finance;
    private string draftId=Guid.NewGuid().ToString();private long version;private bool loaded,saved,conflict;
    private Expense? original;private Receipt? receipt;private ImageSource? preview;
    private string name="",category="",amount="",notes="",driver="",vehicle="",scope="General",kind="Operating expense";private DateTime date=DateTime.Today;
    public string Name{get=>name;set=>Set(ref name,value);}public string Category{get=>category;set=>Set(ref category,value);}public string Amount{get=>amount;set=>Set(ref amount,value);}public string Notes{get=>notes;set=>Set(ref notes,value);}public string Driver{get=>driver;set=>Set(ref driver,value);}public string Vehicle{get=>vehicle;set=>Set(ref vehicle,value);}
    public string Scope{get=>scope;set{Set(ref scope,value);Raise(nameof(IsVehicle));}}public string CostKind{get=>kind;set=>Set(ref kind,value);}
    public DateTime Date{get=>date;set=>Set(ref date,value);}public DateTime Today=>DateTime.Today;
    public bool IsVehicle=>Scope=="Vehicle";public bool IsEditing=>original is not null;public string PageTitle=>LocalizationService.Text(IsEditing?"Edit expense":"Add an expense");
    public string SaveLabel=>LocalizationService.Text(IsEditing?"Save changes":"Save expense");public bool CanSave=>!conflict&&!IsBusy;
    public bool IsDispatcher=>auth.User?.IsDriver!=true||auth.User.CanDispatch;public bool CanManageCategories=>auth.User?.Can("Expense categories")==true;
    public ImageSource? Preview{get=>preview;private set=>Set(ref preview,value);}public bool HasReceipt=>receipt is not null;public bool HasImage=>receipt?.Data.StartsWith("data:image/",StringComparison.OrdinalIgnoreCase)==true;public bool HasDocument=>HasReceipt&&!HasImage;public string AttachmentName=>receipt?.Name??"";
    public ObservableCollection<string> Categories{get;}=[];public ObservableCollection<string> Drivers{get;}=[];public ObservableCollection<string> Vehicles{get;}=[];
    public IReadOnlyList<string> Scopes{get;}=["General","Vehicle"];public IReadOnlyList<string> CostKinds{get;}=["Operating expense","Driver payroll payment","Other staff salary"];
    public AsyncCommand CaptureCommand{get;}public AsyncCommand PickCommand{get;}public AsyncCommand FileCommand{get;}public AsyncCommand RemoveReceiptCommand{get;}public AsyncCommand OpenAttachmentCommand{get;}public AsyncCommand SaveCommand{get;}public AsyncCommand CategoriesCommand{get;}
    public AddExpenseViewModel(DispatchService dispatch,ReceiptService receipts,AuthService auth,NavigationService nav,FinanceService finance)
    {
        this.dispatch=dispatch;this.receipts=receipts;this.auth=auth;this.nav=nav;this.finance=finance;Driver=IsDispatcher?"":auth.User?.Name??"";
        CaptureCommand=new(()=>Run(async()=>SetReceipt(await receipts.CaptureAsync())));PickCommand=new(()=>Run(async()=>SetReceipt(await receipts.PickAsync())));FileCommand=new(()=>Run(async()=>SetReceipt(await receipts.PickFileAsync())));
        RemoveReceiptCommand=new(()=>{receipt=null;Preview=null;NotifyReceipt();return Task.CompletedTask;});OpenAttachmentCommand=new(()=>Run(async()=>{if(receipt is not null)await ReceiptService.OpenAsync(receipt);}));
        SaveCommand=new(()=>Run(Save));RefreshCommand=new(()=>{loaded=false;conflict=false;return LoadAsync();});CategoriesCommand=new(()=>Run(()=>nav.PushAsync<Pages.ExpenseCategoriesPage>()));PropertyChanged+=(_,e)=>{if(e.PropertyName==nameof(IsBusy))Raise(nameof(CanSave));};
    }
    public void Configure(Expense expense){draftId=expense.Id;original=JsonSerializer.Deserialize<Expense>(JsonSerializer.Serialize(expense,ApiRepository.Json),ApiRepository.Json);}
    private void NotifyReceipt(){foreach(var p in new[]{nameof(HasReceipt),nameof(HasImage),nameof(HasDocument),nameof(AttachmentName)})Raise(p);}
    private void SetReceipt(Receipt? value){if(value is null)return;receipt=value;var bytes=ReceiptFiles.Read(value);Preview=HasImage?ImageSource.FromStream(()=>new MemoryStream(bytes)):null;NotifyReceipt();}
    public override Task LoadAsync()=>Run(async()=>
    {
        var choices = await finance.ExpenseChoicesAsync();
        Categories.Clear();
        foreach (var choice in choices.Categories.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.OrdinalIgnoreCase))
            Categories.Add(choice);

        var records = await dispatch.ExpensesAsync();
        foreach (var choice in records.Rows.Select(e => e.Category).Where(c => !string.IsNullOrWhiteSpace(c)))
            if (!Categories.Contains(choice, StringComparer.OrdinalIgnoreCase)) Categories.Add(choice);
        Drivers.Clear();Drivers.Add("");foreach(var d in choices.Drivers)Drivers.Add(d.Name);Vehicles.Clear();foreach(var v in choices.Vehicles)Vehicles.Add(v.Plate+" · "+v.Name);
        if(loaded){if(original is null)version=records.Version;return;}version=records.Version;
        if(original is not null){original=records.Rows.FirstOrDefault(e=>e.Id==draftId)??throw new InvalidOperationException("This expense is no longer available.");Name=original.Name;Category=original.Category;Amount=original.Amount.ToString("0.##",LocalizationService.NumericCulture);Notes=original.Notes;Driver=original.Driver;Vehicle=original.Vehicle;Scope=original.Scope;CostKind=original.CostKind;Date=DateTime.ParseExact(original.Date,"yyyy-MM-dd",CultureInfo.InvariantCulture);receipt=null;Preview=null;if(original.Receipt is not null)SetReceipt(original.Receipt);NotifyReceipt();}
        if(!string.IsNullOrEmpty(Vehicle)&&!Vehicles.Contains(Vehicle))Vehicles.Add(Vehicle);if(!string.IsNullOrEmpty(Driver)&&!Drivers.Contains(Driver))Drivers.Add(Driver);
        loaded=true;Raise(string.Empty);
    });
    private async Task Save()
    {
        if(saved||conflict)return;ScreenRules.ValidateExpenseDate(Date,Today);var input=InputValidation.NormalizeDigits(Amount).Replace('٫','.');
        if(!InputValidation.TryDecimal(input,LocalizationService.Culture,out var value)||value<=0)throw new InvalidOperationException("Enter a positive amount.");
        if(string.IsNullOrWhiteSpace(Name)||!Categories.Contains(Category))throw new InvalidOperationException("Enter a description and select an existing category.");
        if(IsVehicle&&!Vehicles.Contains(Vehicle))throw new InvalidOperationException("Select the linked vehicle for vehicle expenses.");
        var e=original is null?new Expense{Id=draftId}:JsonSerializer.Deserialize<Expense>(JsonSerializer.Serialize(original,ApiRepository.Json),ApiRepository.Json)!;
        e.Name=Name.Trim();e.Category=Category;e.Amount=value;e.Date=Date.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);e.Driver=IsDispatcher?Driver:auth.User!.Name;e.Scope=Scope;e.Vehicle=IsVehicle?Vehicle:"";e.CostKind=CostKind;e.Notes=Notes.Trim();e.Receipt=receipt;
        if(!await nav.ConfirmAsync(SaveLabel,LocalizationService.Format("Save expense {0} for {1}?",e.Name,e.Amount)))return;
        try{await dispatch.SaveExpenseAsync(e,version,original:original);}catch(ApiException ex)when((int)ex.Status==409){conflict=true;Raise(nameof(CanSave));throw;}
        saved=true;await nav.PopAsync();
    }
}
