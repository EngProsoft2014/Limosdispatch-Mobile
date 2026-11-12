using LimosDispatch.Mobile.Controls;
using LimosDispatch.Mobile.ViewModels;
namespace LimosDispatch.Mobile.Pages;

public partial class EarningsPage : ContentPage
{
    private readonly EarningsViewModel model;

    public EarningsPage(EarningsViewModel model)
    {
        InitializeComponent();
        BindingContext = this.model = model;
        var from = new DateChoicePicker { Date = model.ReportFrom, Format = "dd MMM yy" };
        var to = new DateChoicePicker { Date = model.ReportTo, Format = "dd MMM yy" };
        var apply = new Button { Text = LocalizationService.Text("Apply"), VerticalOptions = LayoutOptions.End, Padding = new Thickness(10, 0) };
        apply.Clicked += async (_, _) => await LimosDispatch.Core.Helpers.UserFeedback.RunAsync(async () =>
        {
            if (from.Date > to.Date) throw new InvalidOperationException("Choose a valid date range.");
            model.ReportFrom = from.Date;
            model.ReportTo = to.Date;
            model.Month = new DateTime(to.Date.Year, to.Date.Month, 1);
            await model.LoadAsync();
        });
        ReportRange.Add(FilterDialog.Field("From", from), 0);
        ReportRange.Add(FilterDialog.Field("To", to), 1);
        ReportRange.Add(apply, 2);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LimosDispatch.Core.Helpers.UserFeedback.RunAsync(model.LoadAsync);
    }
}
