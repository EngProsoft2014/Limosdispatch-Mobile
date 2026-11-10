using LimosDispatch.Mobile.ViewModels;
namespace LimosDispatch.Mobile.Pages;
public partial class LoginPage:ContentPage {readonly LoginViewModel model; public LoginPage(LoginViewModel model){InitializeComponent();BindingContext=this.model=model;} protected override async void OnAppearing(){base.OnAppearing();await LimosDispatch.Core.Helpers.UserFeedback.RunAsync(model.LoadAsync);} private void OnRememberMeTapped(object? sender, TappedEventArgs e) => model.RememberMe = !model.RememberMe;protected override bool OnBackButtonPressed(){
#if ANDROID
if(Navigation.NavigationStack.Count>1)return base.OnBackButtonPressed();
if(!model.IsBusy)_=FeedbackService.ConfirmExitAsync(()=>Window?.Page is NavigationPage root&&root.CurrentPage==this&&Navigation.NavigationStack.Count==1,()=>Application.Current?.Quit());return true;
#else
return base.OnBackButtonPressed();
#endif
}}


