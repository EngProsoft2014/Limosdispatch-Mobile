using LimosDispatch.Mobile.ViewModels;
namespace LimosDispatch.Mobile.Pages;
public partial class ExpensesPage:ContentPage {readonly ExpensesViewModel model; public ExpensesPage(ExpensesViewModel model){InitializeComponent();BindingContext=this.model=model;} protected override async void OnAppearing(){base.OnAppearing();await LimosDispatch.Core.Helpers.UserFeedback.RunAsync(model.LoadAsync);}
    private async void OnFiltersClicked(object? sender, EventArgs e) =>
        await LimosDispatch.Core.Helpers.UserFeedback.RunAsync(async () =>
        {
        var from = new Controls.DateChoicePicker { Date = model.From, MaximumDate = model.Today };
        var to = new Controls.DateChoicePicker { Date = model.To, MaximumDate = model.Today };
        var scope = new Controls.ChoicePicker { ItemsSource = model.Scopes, SelectedItem = model.ScopeFilter, Title = LocalizationService.Text("Expense type") };
        var vehicle = new Controls.ChoicePicker { ItemsSource = await model.GetVehicleNamesAsync(), SelectedItem = model.VehicleFilter, Title = LocalizationService.Text("Select vehicle") };
        var fields = new List<View>
        {
            Controls.FilterDialog.Pair(Controls.FilterDialog.Field("From", from), Controls.FilterDialog.Field("To", to)),
            Controls.FieldBorder.Wrap(scope),
            Controls.FieldBorder.Wrap(vehicle)
        };
        Controls.ChoicePicker? driver = null;
        if (model.IsDispatcher)
        {
            var all = new DriverOption(null, LocalizationService.Text("All chauffeurs"));
            var options = (await model.GetDriverNamesAsync()).Select(name => new DriverOption(name, name)).Prepend(all).ToArray();
            driver = new Controls.ChoicePicker
            {
                Title = LocalizationService.Text("Choose chauffeur"),
                DisplayMember = nameof(DriverOption.Label),
                ItemsSource = options,
                SelectedItem = options.FirstOrDefault(option => option.Name == model.DriverFilter) ?? all
            };
            fields.Add(Controls.FieldBorder.Wrap(driver));
        }
        if (!await Controls.FilterDialog.ShowAsync(Navigation, fields.ToArray())) return;
        if (from.Date > to.Date) throw new InvalidOperationException("Choose a valid date range.");
        model.From = from.Date;
        model.To = to.Date;
        model.ScopeFilter = (string?)scope.SelectedItem ?? model.ScopeFilter;
        model.VehicleFilter = (string?)vehicle.SelectedItem ?? model.VehicleFilter;
        model.DriverFilter = (driver?.SelectedItem as DriverOption)?.Name;
        await model.LoadAsync();
        });
    private sealed record DriverOption(string? Name, string Label);
}
