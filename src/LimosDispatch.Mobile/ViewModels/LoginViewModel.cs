using LimosDispatch.Core.Constants;
using LimosDispatch.Core.GenericRepository;
namespace LimosDispatch.Mobile.ViewModels;
public sealed class LoginViewModel : BaseViewModel
{
    private readonly AuthService auth;
    private readonly IGenericRepository api;
    private readonly NavigationService nav;
    private readonly RealtimeService realtime;
    private readonly SecureSessionStore sessionStore;
    private readonly LoginMemory loginMemory;
    private Task pendingClear = Task.CompletedTask;
    private bool loadingSavedLogin, rememberMe;
    private readonly CompanySettingsService settings;
    private bool attemptedRestore;
    private string company = "", email = "", password = "", newPassword = "";
    private bool changing;
    private string emailError = "", companyError = "", passwordError = "";
    private bool validating;
    public string EmailError { get => emailError; private set => Set(ref emailError, value); }
    public string CompanyError { get => companyError; private set => Set(ref companyError, value); }
    public string PasswordError { get => passwordError; private set => Set(ref passwordError, value); }
    private bool Validate()
    {
        CompanyError = string.IsNullOrWhiteSpace(Company) ? "Enter your company code." : Company.Trim().Length > 63 || !Company.Trim().All(c => char.IsAsciiLetterOrDigit(c) || c == '-') ? "Use letters, numbers and hyphens for the company code." : "";
        EmailError = LimosDispatch.Core.Helpers.InputValidation.EmailError(Email, true);
        PasswordError = string.IsNullOrEmpty(Password) ? "Enter your password." : "";
        return CompanyError.Length + EmailError.Length + PasswordError.Length == 0;
    }
    public string Company
    {
        get => company;
        set
        {
            var changed = LoginMemory.NormalizeCompany(company) != LoginMemory.NormalizeCompany(value);
            if (!Set(ref company, value) || !changed || loadingSavedLogin) return;
            Email = ""; Password = ""; NewPassword = "";
            Set(ref rememberMe, false, nameof(RememberMe));
            MustChangePassword = false;
            pendingClear = ClearRememberedLogin();
        }
    }
    public bool RememberMe
    {
        get => rememberMe;
        set
        {
            if (Set(ref rememberMe, value) && !value && !loadingSavedLogin)
                pendingClear = ClearRememberedLogin();
        }
    }
    private async Task ClearRememberedLogin()
    {
        await loginMemory.ClearAsync();
        await sessionStore.SetPersistenceAsync(false);
    }
    public string Email { get => email; set { if (Set(ref email, value) && validating) Validate(); } }
    public string Password { get => password; set { if (Set(ref password, value) && validating) Validate(); } }
    public string NewPassword { get => newPassword; set => Set(ref newPassword, value); }
    public bool MustChangePassword { get => changing; set => Set(ref changing, value); }
    public AsyncCommand LoginCommand { get; }
    public AsyncCommand ChangePasswordCommand { get; }
    public LoginViewModel(AuthService auth, IGenericRepository api, NavigationService nav, RealtimeService realtime, SecureSessionStore sessionStore, CompanySettingsService settings, LoginMemory loginMemory)
    {
        this.auth = auth; this.api = api; this.nav = nav; this.realtime = realtime; this.sessionStore = sessionStore; this.settings = settings; this.loginMemory = loginMemory;
        LoginCommand = new(() => Run(Login));
        ChangePasswordCommand = new(() => Run(ChangePassword));
    }
    public override async Task LoadAsync()
    {
        if (attemptedRestore) return;
        attemptedRestore = true;
        await Run(async () =>
        {
            var saved = await loginMemory.LoadAsync();
            loadingSavedLogin = true;
            try
            {
                if (saved is not null) { Company = saved.Company; Email = saved.Email; Password = saved.Password; RememberMe = true; }
            }
            finally { loadingSavedLogin = false; }
            MustChangePassword = auth.User?.MustChangePassword == true;
        });
    }
    private async Task Login()
    {
        validating = true; if (!Validate()) return;
        await pendingClear;
        if (!RememberMe) await loginMemory.ClearAsync();
        await sessionStore.SetPersistenceAsync(RememberMe);
        await realtime.ResetAsync();
        await auth.LoginAsync(Company, Email, Password);
        await CompleteLogin();
    }
    private async Task CompleteLogin()
    {
        var user = await api.GetAsync<UserProfile>(ApiRoutes.Me);
        auth.SetUser(user);
        MustChangePassword = user.MustChangePassword;
        if (MustChangePassword) return;
        await settings.LoadAsync(user);
        await loginMemory.SaveAsync(Company, Email, Password, RememberMe);
        Password = ""; NewPassword = "";
        nav.OpenWorkspace(user);
        _ = realtime.ResumeAsync();
    }
    private async Task ChangePassword()
    {
        if (NewPassword.Length < 12) throw new InvalidOperationException("Use a new password with at least 12 characters.");
        await api.PostAsync<System.Text.Json.JsonElement>("api/auth/change-password", new { currentPassword = Password, newPassword = NewPassword });
        Password = NewPassword;
        await CompleteLogin();
    }
}
