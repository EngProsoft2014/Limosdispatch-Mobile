namespace LimosDispatch.Mobile.Pages;
public partial class StatementReportPage:ContentPage
{
 public StatementReportPage(string html){InitializeComponent();ReportWeb.Source=new HtmlWebViewSource{Html=html};}
 async void PrintClicked(object? sender,EventArgs e)=>await LimosDispatch.Core.Helpers.UserFeedback.RunAsync(()=>{
 #if ANDROID
 var web=ReportWeb.Handler?.PlatformView as Android.Webkit.WebView??throw new InvalidOperationException("Wait for the report to load.");var activity=Platform.CurrentActivity??throw new InvalidOperationException("The report is not available.");var manager=(Android.Print.PrintManager)activity.GetSystemService(Android.Content.Context.PrintService)!;manager.Print("LimosDispatch statement",web.CreatePrintDocumentAdapter("LimosDispatch statement"),null);
 #elif IOS
 var web=ReportWeb.Handler?.PlatformView as WebKit.WKWebView??throw new InvalidOperationException("Wait for the report to load.");var controller=UIKit.UIPrintInteractionController.SharedPrintController;controller.PrintFormatter=web.ViewPrintFormatter;controller.Present(true,null);
 #endif
 return Task.CompletedTask;});
}
