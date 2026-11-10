namespace LimosDispatch.Mobile.Pages;
public partial class ReceiptPage : ContentPage
{
    private readonly Receipt receipt;
    public ReceiptPage(Receipt receipt)
    {
        InitializeComponent(); this.receipt = receipt; ReceiptName.Text = receipt.Name;
        try
        {
            var bytes = LimosDispatch.Core.Helpers.ReceiptFiles.Read(receipt);
            if (receipt.Data.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase)) ReceiptImage.Source = ImageSource.FromStream(() => new MemoryStream(bytes));
            else { ReceiptImage.IsVisible = false; DocumentPanel.IsVisible = true; }
        }
        catch (InvalidOperationException ex) { ReceiptName.Text = ex.Message; }
    }
    private async void OnOpenDocument(object? sender, EventArgs e)
    {
        try { await ReceiptService.OpenAsync(receipt); }
        catch (Exception ex) { await LimosDispatch.Core.Helpers.UserFeedback.ReportAsync(ex); }
    }
}

