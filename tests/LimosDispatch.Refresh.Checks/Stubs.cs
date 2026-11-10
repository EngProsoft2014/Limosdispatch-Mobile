global using LimosDispatch.Mobile.Helpers;
namespace LimosDispatch.Mobile.ViewModels;
public sealed class LocalizationService { public static LocalizationService Instance {get;}=new();public event EventHandler? Changed;public void Change()=>Changed?.Invoke(this,EventArgs.Empty); }
