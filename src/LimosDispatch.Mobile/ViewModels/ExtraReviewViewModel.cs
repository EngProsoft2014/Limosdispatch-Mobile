using System.Globalization;
using LimosDispatch.Core.Helpers;
namespace LimosDispatch.Mobile.ViewModels;
public sealed class ExtraReviewViewModel(FinanceService finance,NavigationService nav):BaseViewModel
{
 private string tripId="",amount="",note="";private bool confirmed;public TripExtra Extra{get;private set;}=new();
 public string Amount{get=>amount;set=>Set(ref amount,value);}public string Note{get=>note;set=>Set(ref note,value);}public bool CustomerConfirmed{get=>confirmed;set=>Set(ref confirmed,value);}
 public void Configure(string id,TripExtra extra){tripId=id;Extra=extra;Raise(string.Empty);}
 public AsyncCommand ApproveCommand=>new(()=>Run(()=>Review("Approved")));public AsyncCommand RejectCommand=>new(()=>Run(()=>Review("Rejected")));
 private async Task Review(string decision){decimal value=0;if(decision=="Approved"&&!InputValidation.TryDecimal(Amount,LocalizationService.Culture,out value))throw new InvalidOperationException("Enter a valid amount.");if(!await nav.ConfirmAsync("Review extra",LocalizationService.Text(decision=="Approved"?"Approve this extra charge after confirming it with the customer?":"Reject this extra request?")))return;await finance.ReviewExtraAsync(new(tripId,Extra.Id,decision,value,CustomerConfirmed,Note.Trim()));await nav.PopAsync();}
}
