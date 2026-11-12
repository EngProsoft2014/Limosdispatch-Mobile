using System.Globalization;
using LimosDispatch.Core.Helpers;
using LimosDispatch.Mobile.Controls;

namespace LimosDispatch.Mobile.Pages;

public partial class MileageReportPage : ContentPage
{
    private readonly MileageApi api;
    private readonly AuthService auth;
    private readonly DispatchService dispatch;
    private readonly VerticalStackLayout results = new() { Spacing = 10 };
    private readonly DateChoicePicker from = new(), to = new();
    private static readonly TimeSpan EndOfDay = new(23, 59, 0);
    private readonly TimePicker fromTime = new() { Time = TimeSpan.Zero, Format = "HH:mm" };
    private readonly TimePicker toTime = new() { Time = EndOfDay, Format = "HH:mm" };
    private bool loading;
    private string? selectedDriverId;
    private sealed record DriverFilter(string? Id, string Name);

    public MileageReportPage(MileageApi api, AuthService auth, DispatchService dispatch)
    {
        InitializeComponent();
        this.api = api;
        this.auth = auth;
        this.dispatch = dispatch;
        Title = LocalizationService.Text("Mileage");
        FlowDirection = LocalizationService.Instance.Code == "ar" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        Shell.SetTitleView(this, new CenteredBrandHeader());
        var filter = new ImageButton { Source = "filter_menu.png", WidthRequest = 44, HeightRequest = 44, Padding = 10, BackgroundColor = Colors.Transparent };
        SemanticProperties.SetDescription(filter, LocalizationService.Text("Filters"));
        filter.Clicked += async (_, _) => await UserFeedback.RunAsync(OpenFiltersAsync);
        var heading = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Auto)] };
        heading.Add(new Label { Text = Title, FontSize = 26, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center });
        heading.Add(filter, 1);
        Rows.Add(heading);
        Rows.Add(results);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (results.Count == 0) _ = LoadAsync(false);
    }

    private IReadOnlyList<DriverFilter> drivers = [];

    private async Task OpenFiltersAsync()
    {
        if (loading) return;
        var startDate = new DateChoicePicker { Date = from.Date };
        var endDate = new DateChoicePicker { Date = to.Date };
        var startTime = new TimePicker { Time = fromTime.Time, Format = "HH:mm" };
        var endTime = new TimePicker { Time = toTime.Time, Format = "HH:mm" };
        var fields = new List<View>
        {
            FilterDialog.Pair(FilterDialog.Field("From", startDate), FilterDialog.Field("To", endDate)),
            FilterDialog.Pair(FilterDialog.Field("From time", startTime), FilterDialog.Field("To time", endTime))
        };
        if (auth.User?.CanDispatch == true)
        {
            var directory = await dispatch.DriversAsync();
            drivers = directory.Where(driver => !string.IsNullOrWhiteSpace(driver.TrackingId))
                .Select(driver => new DriverFilter(driver.TrackingId, driver.Name))
                .Concat(drivers)
                .DistinctBy(driver => driver.Id, StringComparer.OrdinalIgnoreCase)
                .OrderBy(driver => driver.Name)
                .ToArray();
        }

        var all = new DriverFilter(null, LocalizationService.Text("All chauffeurs"));
        var picker = new ChoicePicker
        {
            Title = all.Name, DisplayMember = nameof(DriverFilter.Name),
            ItemsSource = new[] { all }.Concat(drivers).ToArray(),
            SelectedItem = drivers.FirstOrDefault(x => x.Id == selectedDriverId) ?? all
        };
        if (auth.User?.CanDispatch == true) fields.Add(FieldBorder.Wrap(picker));
        if (!await FilterDialog.ShowAsync(Navigation, fields.ToArray())) return;

        if (startDate.Date.Date + (startTime.Time ?? TimeSpan.Zero) > endDate.Date.Date + (endTime.Time ?? EndOfDay))
            throw new InvalidOperationException("Choose a valid date range.");
        from.Date = startDate.Date;
        to.Date = endDate.Date;
        fromTime.Time = startTime.Time;
        toTime.Time = endTime.Time;
        selectedDriverId = (picker.SelectedItem as DriverFilter)?.Id;
        await LoadAsync(true);
    }

    private async Task LoadAsync(bool filter)
    {
        if (loading) return;
        loading = true;
        try
        {
            await UserFeedback.RunAsync(async () =>
            {
                var startTime = fromTime.Time ?? TimeSpan.Zero;
                var endTime = toTime.Time ?? EndOfDay;
                var start = from.Date.Date + startTime;
                var end = to.Date.Date + endTime;
                if (filter && start > end) throw new InvalidOperationException("Choose a valid date range.");
                var report = await api.ListAsync(filter ? DateOnly.FromDateTime(from.Date) : null,
                    filter ? DateOnly.FromDateTime(to.Date) : null);
                if (!filter && DateOnly.TryParseExact(report.Today, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var today))
                {
                    from.Date = new DateTime(today.Year, today.Month, 1);
                    to.Date = today.ToDateTime(TimeOnly.MinValue);
                }
                var hasTimeFilter = filter && (startTime != TimeSpan.Zero || endTime != EndOfDay);
                Render(report, hasTimeFilter ? start : null, hasTimeFilter ? end.AddMinutes(1) : null);
            });
        }
        finally { loading = false; }
    }

    private void Render(MileageReport report, DateTime? start, DateTime? end)
    {
        results.Clear();
        var list = new VerticalStackLayout { Spacing = 10 };
        var periodDrivers = report.Items.Where(x => !string.IsNullOrWhiteSpace(x.UserId)).DistinctBy(x => x.UserId)
            .OrderBy(x => x.Driver).Select(x => new DriverFilter(x.UserId, x.Driver)).ToList();
        drivers = drivers.Concat(periodDrivers).DistinctBy(x => x.Id).OrderBy(x => x.Name).ToArray();
        results.Add(list);
        Show(selectedDriverId);

        bool InRange(MileageReading? reading) => reading?.CapturedAt is { } at && at.LocalDateTime >= start && at.LocalDateTime < end;

        void Show(string? driver)
        {
            list.Clear();
            var shifts = report.Items.Where(x => (driver is null || string.Equals(x.UserId, driver, StringComparison.OrdinalIgnoreCase)) && (start is null || InRange(x.Start) || InRange(x.End))).OrderByDescending(x => x.Day).ToList();
            var total = driver is null && start is null ? report.TotalMiles : shifts.Sum(x => x.End is null ? 0 : x.End.Miles - x.Start.Miles);
            list.Add(new Border { Content = new Label { Text = LocalizationService.Format("Total mileage · {0} mi", Number(total)), FontSize = 18, FontAttributes = FontAttributes.Bold } });
            if (shifts.Count == 0) list.Add(new Label { Text = LocalizationService.Text("No mileage records in this period.") });
            foreach (var shift in shifts)
            {
                var content = new VerticalStackLayout { Spacing = 6 };
                content.Add(new Label { Text = shift.Driver, FontAttributes = FontAttributes.Bold });
                content.Add(new Label { Text = $"{shift.Day} · {shift.Vehicle}" });
                AddReading("Mileage check-in", shift.Start);
                if (shift.End is { } end)
                {
                    AddReading("Mileage check-out", end);
                    content.Add(new Label { Text = LocalizationService.Format("Total mileage · {0} mi", Number(end.Miles - shift.Start.Miles)) });
                }
                else content.Add(new Label { Text = LocalizationService.Text("Check-out pending") });
                list.Add(new Border { Content = content });

                void AddReading(string label, MileageReading reading)
                {
                    content.Add(new Label { Text = $"{LocalizationService.Text(label)} · {Number(reading.Miles)}" });
                    if (reading.CapturedAt is { } at)
                        content.Add(new Label { Text = at.ToLocalTime().ToString("g", LocalizationService.Culture) });
                    if (reading.IsLate) content.Add(new Label { Text = LocalizationService.Text("Late reading") });
                    if (!string.IsNullOrWhiteSpace(reading.Reason))
                        content.Add(new Label { Text = reading.Reason });
                    if (!string.IsNullOrWhiteSpace(reading.PhotoId))
                        content.Add(Action("View odometer photo", () => ShowPhotoAsync(reading.PhotoId)));
                }
            }
        }
    }

    private async Task ShowPhotoAsync(string id)
    {
        var bytes = await api.PhotoAsync(id);
        var stack = new Grid { RowDefinitions = [new(GridLength.Auto), new(GridLength.Star)], Padding = 16 };
        stack.Add(Action("Close", async () => await Navigation.PopModalAsync()));
        stack.Add(new Image { Source = ImageSource.FromStream(() => new MemoryStream(bytes, writable: false)), Aspect = Aspect.AspectFit }, 0, 1);
        var page = new ContentPage { Content = stack };
        page.SetDynamicResource(BackgroundColorProperty, "Canvas");
        await Navigation.PushModalAsync(page);
    }

    private static string Number(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    private static Button Action(string label, Func<Task> action) => new()
    {
        Text = LocalizationService.Text(label), HeightRequest = 44, Command = new AsyncCommand(() => UserFeedback.RunAsync(action))
    };
}
