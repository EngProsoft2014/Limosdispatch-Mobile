using System.Collections.ObjectModel;
namespace LimosDispatch.Mobile.ViewModels;
public sealed class PassengerSearchViewModel : BaseViewModel
{
    private readonly PassengerService passengers;
    private List<Passenger> all = [];
    private bool loaded, showNew, noResults;
    private string query = "", name = "", phone = "", email = "";
    private readonly string requestId = "CUS-" + Guid.NewGuid();
    public ObservableCollection<Passenger> Results { get; } = [];
    public Func<Passenger, Task>? Selected { get; set; }
    public bool CanAdd => passengers.CanAdd;
    public bool ShowNew { get => showNew; set { Set(ref showNew, value); Raise(nameof(ShowResults)); Raise(nameof(CanShowAdd)); } }
    public bool CanShowAdd => CanAdd && ShowResults && loaded && NoResults && !string.IsNullOrWhiteSpace(Query);
    public bool ShowResults => !ShowNew;
    public bool NoResults { get => noResults; private set { Set(ref noResults, value); Raise(nameof(CanShowAdd)); } }
    public string Query { get => query; set { if (Set(ref query, value)) Apply(); } }
    private bool nameTouched, phoneTouched, emailTouched;
    private string nameError = "", phoneError = "", emailError = "";
    public string NameError { get => nameError; private set => Set(ref nameError, value); }
    public string PhoneError { get => phoneError; private set => Set(ref phoneError, value); }
    public string EmailError { get => emailError; private set => Set(ref emailError, value); }
    public string Name { get => name; set { if (Set(ref name, value) && nameTouched) ValidateName(); } }
    public string Phone { get => phone; set { if (Set(ref phone, value) && phoneTouched) ValidatePhone(); } }
    public string Email { get => email; set { if (Set(ref email, value) && emailTouched) ValidateEmail(); } }
    public void ValidateName() { nameTouched = true; NameError = LimosDispatch.Core.Helpers.InputValidation.NameError(Name); }
    public void ValidatePhone() { phoneTouched = true; PhoneError = LimosDispatch.Core.Helpers.InputValidation.PhoneError(Phone); }
    public void ValidateEmail() { emailTouched = true; EmailError = LimosDispatch.Core.Helpers.InputValidation.EmailError(Email); }
    public AsyncCommand NewCommand { get; }
    public AsyncCommand CancelNewCommand { get; }
    public AsyncCommand SaveCommand { get; }
    public PassengerSearchViewModel(PassengerService passengers)
    {
        this.passengers = passengers; RefreshCommand = new(LoadAsync);
        NewCommand = new(() => { if (CanShowAdd) { if (string.IsNullOrWhiteSpace(Name) && string.IsNullOrWhiteSpace(Phone)) { if (PassengerService.Digits(Query).Length >= 5 && !Query.Any(char.IsLetter)) Phone = Query; else Name = Query; } ShowNew = true; Error = ""; } return Task.CompletedTask; });
        CancelNewCommand = new(() => { ShowNew = false; Error = ""; return Task.CompletedTask; });
        SaveCommand = new(() => Run(async () =>
        {
            ValidateName(); ValidatePhone(); ValidateEmail();
            if (NameError.Length + PhoneError.Length + EmailError.Length > 0) return;
            var passenger = await passengers.SaveAsync(new Passenger { CustomerId = requestId, Name = Name, Phone = Phone, Email = Email });
            if (Selected is not null) await Selected(passenger);
        }));
    }
    public override Task LoadAsync() => Run(async () => { if (loaded) return; all = (await passengers.ListAsync()).Rows; loaded = true; Apply(); });
    private void Apply() { Results.Clear(); foreach (var p in PassengerService.Search(all, Query)) Results.Add(p); NoResults = loaded && Results.Count == 0; }
}
