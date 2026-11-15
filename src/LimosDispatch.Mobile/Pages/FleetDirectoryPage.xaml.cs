using System.Text.Json;
using LimosDispatch.Core.GenericRepository;
using LimosDispatch.Core.Helpers;
namespace LimosDispatch.Mobile.Pages;

public partial class FleetDirectoryPage : ContentPage
{
    private readonly FinanceService finance;
    private readonly IGenericRepository api;
    private readonly string module, permission;
    private readonly (string Key, string Label)[] fields;
    private Records<JsonElement>? snapshot;
    private bool busy, editing;
    public FleetDirectoryPage(FinanceService finance, IGenericRepository api, bool vehicles)
    {
        this.finance = finance; this.api = api;
        module = vehicles ? "fleet" : "drivers";
        permission = vehicles ? "Fleet" : "Drivers";
        fields = vehicles ? [("name", "Make and model"), ("plate", "License plate"), ("category", "Vehicle type"), ("seats", "Passenger capacity"), ("insurance", "Insurance expiry"), ("status", "Availability"), ("odometer", "Odometer")]
            : [("name", "Name"), ("phone", "Phone"), ("email", "Email"), ("license", "License number"), ("vehicle", "Vehicle"), ("status", "Status"), ("limoLicenseNumber", "Limo license number"), ("limoLicenseExpiry", "Limo license expiry date"), ("paySystemId", "Driver pay system")];
        InitializeComponent();
        Heading.Text = Title = L(vehicles ? "Vehicles" : "Chauffeurs");
        Search.Placeholder = L("Search");
        AddButton.Text = L("Add");
    }
    protected override async void OnAppearing() { base.OnAppearing(); if (!editing) await LoadAsync(); }
    private async Task LoadAsync()
    {
        if (busy) return;
        busy = Loading.IsRunning = Loading.IsVisible = true;
        AddButton.IsEnabled = false;
        try { await UserFeedback.RunAsync(async () => { finance.Demand(permission); var workspace = await finance.WorkspaceAsync(); snapshot = new() { Version = workspace.Version, Rows = ReadSection(workspace) }; Render(); AddButton.IsEnabled = true; }); }
        finally { busy = Loading.IsRunning = Loading.IsVisible = false; }
    }
    private void SearchChanged(object? sender, TextChangedEventArgs e) => Render();
    private void Render()
    {
        Rows.Clear();
        if (snapshot is null) return;
        var query = Search.Text?.Trim() ?? "";
        var visible = snapshot.Rows.Where(row => fields.Any(f => Read(row, f.Key).Contains(query, StringComparison.OrdinalIgnoreCase))).ToList();
        if (visible.Count == 0) Rows.Add(new Label { Text = L("No matching options") });
        foreach (var row in visible)
        {
            var name = Read(row, "name");
            var status = Read(row, "status");
            var detail = string.Join(" · ", new[] { "plate", "category", "phone", "vehicle" }
                .Select(key => Read(row, key)).Where(value => value.Length > 0));
            var label = new Label { Text = name, FontSize = 16, FontAttributes = FontAttributes.Bold };
            label.SetDynamicResource(Label.TextColorProperty, "Ink");
            var caption = new Label { Text = detail, FontSize = 12, IsVisible = detail.Length > 0 };
            caption.SetDynamicResource(Label.TextColorProperty, "Muted");
            var badgeText = new Label { Text = L(status), FontSize = 11, FontAttributes = FontAttributes.Bold };
            badgeText.SetDynamicResource(Label.TextColorProperty, status.Equals("Active", StringComparison.OrdinalIgnoreCase) ? "AccentText" : "Muted");
            var badge = new Border { Content = badgeText, Padding = new Thickness(8, 3), StrokeThickness = 0,
                HorizontalOptions = LayoutOptions.Start, IsVisible = status.Length > 0,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(7) } };
            badge.SetDynamicResource(BackgroundColorProperty, "SoftSurface");
            var text = new VerticalStackLayout { Spacing = 4, VerticalOptions = LayoutOptions.Center };
            text.Add(label); text.Add(caption); text.Add(badge);
            var icon = new Image { Source = module == "drivers" ? "account_menu.png" : "rides.png",
                WidthRequest = 21, HeightRequest = 21, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
            var tile = new Border { Content = icon, WidthRequest = 40, HeightRequest = 40, Padding = 0, StrokeThickness = 0,
                VerticalOptions = LayoutOptions.Center,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(12) } };
            tile.SetDynamicResource(BackgroundColorProperty, "SoftSurface");
            var photo = FleetMedia.Preview(row, module == "fleet" ? "main" : "photo") ?? (module == "fleet" ? VehiclePhoto(row) : null);
            if (photo is not null)
            {
                tile.WidthRequest = 72;
                tile.HeightRequest = 52;
                tile.Content = new Image { Source = photo, Aspect = Aspect.AspectFill };
            }
            var content = new Grid { ColumnDefinitions = [new(GridLength.Auto), new(GridLength.Star)], ColumnSpacing = 10 };
            content.Add(tile); content.Add(text, 1);
            if (photo is not null)
                tile.GestureRecognizers.Add(new TapGestureRecognizer { Command = new AsyncCommand(() => UserFeedback.RunAsync(() => FleetMedia.OpenPhotoAsync(photo))) });
            (photo is null ? (View)content : text).GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new AsyncCommand(() => UserFeedback.RunAsync(() => EditAsync(row)))
            });
            var grid = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Auto)], ColumnSpacing = 6 };
            grid.Add(content);
            var menu = new Button { Text = "⋯", FontSize = 22, Padding = 0, WidthRequest = 44, HeightRequest = 44, CornerRadius = 12, VerticalOptions = LayoutOptions.Center };
            menu.SetDynamicResource(Button.BackgroundColorProperty, "SoftSurface");
            menu.SetDynamicResource(Button.TextColorProperty, "Ink");
            SemanticProperties.SetDescription(menu, $"{L("Actions")}: {name}");
            menu.Clicked += async (_, _) => await UserFeedback.RunAsync(() => ShowActionsAsync(row));
            grid.Add(menu, 1);
            var card = new Border { Content = grid, Padding = new Thickness(12, 10), MinimumHeightRequest = 72 };
            card.SetDynamicResource(BackgroundColorProperty, "Surface");
            Rows.Add(card);
        }
    }
    private async Task ShowActionsAsync(JsonElement row)
    {
        if (busy || editing) return;
        var id = Read(row, "accountId");
        var options = new List<(string Key, string Label)> { ("edit", L("Edit")) };
        if (module == "drivers" && id.Length > 0) options.Add(("permissions", L("Permissions")));
        options.Add(("delete", L("Delete")));
        var navigation = Application.Current!.Windows[0].Page!.Navigation;
        var popup = new FleetActionsPopup(navigation, Read(row, "name"), options);
        editing = true;
        string? selected;
        try
        {
            await navigation.PushModalAsync(popup, false);
            selected = await popup.Result;
        }
        finally { editing = false; }
        if (selected == "edit") await EditAsync(row);
        else if (selected == "delete") await DeleteAsync(row);
        else if (selected == "permissions") await Navigation.PushAsync(new FleetAccountPage(api, id));
    }

    private async Task DeleteAsync(JsonElement row)
    {
        if (busy || editing || snapshot is null) return;
        editing = true;
        try
        {
            finance.Demand(permission, "Delete");
            var id = FinanceService.Id(row);
            if (string.IsNullOrWhiteSpace(id)) return;
            if (!await FeedbackService.ConfirmAsync($"{L("Delete")}: {Read(row, "name")}?", "Delete")) return;
            await SaveSectionAsync(row, null);
            await LoadAsync();
        }
        finally { editing = false; }
    }
    private async Task<IReadOnlyList<(string Title, string Body)>> DriverOverviewAsync(JsonElement driver, WorkspaceEnvelope workspace)
    {
        var sections = new List<(string, string)>();
        var ids = new[] { FinanceService.Id(driver), Read(driver, "trackingId"), Read(driver, "driverId") }
            .Where(id => !string.IsNullOrWhiteSpace(id)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var tripIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var name = Read(driver, "name").Trim();
            var uniqueName = name.Length > 0 && FinanceService.Read(workspace, "drivers", new List<JsonElement>())
                .Count(row => string.Equals(Read(row, "name").Trim(), name, StringComparison.OrdinalIgnoreCase)) == 1;
            var trips = FinanceService.Read(workspace, "trips", new List<Trip>())
                .Where(trip => string.IsNullOrWhiteSpace(trip.DriverId)
                    ? uniqueName && string.Equals(trip.DriverName?.Trim(), name, StringComparison.OrdinalIgnoreCase)
                    : ids.Contains(trip.DriverId)).ToList();
            tripIds.UnionWith(trips.Select(t => t.Id).Where(id => !string.IsNullOrWhiteSpace(id)));
            var timezone = Read(FinanceService.Read(workspace, "settings", default(JsonElement)), "timezone");
            if (!TimeZoneInfo.TryFindSystemTimeZoneById(timezone, out var zone))
                zone = TimeZoneInfo.FindSystemTimeZoneById("America/Chicago");
            var month = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);
            var current = trips.Where(trip => !trip.IsTest && trip.Date.StartsWith(month + "-", StringComparison.Ordinal)).DistinctBy(t => t.Id).ToList();
            sections.Add(($"{L("Monthly summary")} · {month}", LimosDispatch.Mobile.Services.LocalizationService.Format(
                "{0} completed · {1} assigned · {2} active", current.Count(t => t.Status == "Completed"), current.Count, current.Count(t => t.IsActive))));
        }
        catch (Exception ex) when (ex is HttpRequestException or ApiException or InvalidDataException or TimeoutException or OperationCanceledException)
        { sections.Add((L("Monthly summary"), L("Could not load the monthly summary."))); }

        try
        {
            var result = await api.GetAsync<JsonElement>("api/journey/reviews");
            var list = result;
            if (result.ValueKind == JsonValueKind.Object && !result.TryGetProperty("reviews", out list))
                throw new InvalidDataException();
            if (list.ValueKind != JsonValueKind.Array) throw new InvalidDataException();
            var scores = new List<decimal>();
            var count = 0;
            foreach (var review in list.EnumerateArray())
            {
                var reviewDriver = Read(review, "driverId");
                var tripId = Read(review, "tripId");
                if (reviewDriver.Length > 0 ? !ids.Contains(reviewDriver) : !tripIds.Contains(tripId)) continue;
                count++;
                if (decimal.TryParse(Read(review, "rating"), System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture, out var rating) && rating is >= 1 and <= 5)
                    scores.Add(rating);
            }
            sections.Add((L("Customer ratings"), scores.Count > 0
                ? FormattableString.Invariant($"★ {scores.Average():0.00} / 5 · ") + LocalizationService.Format("{0} reviews", count)
                : L("No ratings available for this chauffeur.")));
        }
        catch (Exception ex) when (ex is HttpRequestException or ApiException or InvalidDataException or TimeoutException or OperationCanceledException)
        { sections.Add((L("Customer ratings"), L("Could not load customer ratings."))); }
        return sections;
    }
    private async void AddClicked(object? sender, EventArgs e) => await UserFeedback.RunAsync(() => EditAsync(null));
    private async Task EditAsync(JsonElement? original)
    {
        if (busy || editing || snapshot is null) return;
        editing = true;
        try
        {
            var choices = new Dictionary<string, FleetFieldOption[]>();
            choices["status"] = new[] { "Active", "Inactive" }.Select(value => new FleetFieldOption(value, L(value))).ToArray();
            if (module == "fleet")
            {
                var values = new[] { new FleetFieldOption("", L("Select passenger capacity")) };
                choices["seats"] = values.Concat(Enumerable.Range(1, 60).Select(n =>
                {
                    var value = n.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    return new FleetFieldOption(value, value);
                })).ToArray();
            }
            var workspace = await finance.WorkspaceAsync();
            if (module == "drivers")
            {
                choices["vehicle"] = FinanceService.Read(workspace, "fleet", new List<FleetVehicle>())
                    .Where(v => !string.IsNullOrWhiteSpace(v.Plate))
                    .Select(v => new FleetFieldOption(v.Plate, v.DisplayName)).ToArray();
                choices["paySystemId"] = FinanceService.Read(workspace, "driverPaySystems", new List<JsonElement>())
                    .Where(row => !string.IsNullOrWhiteSpace(FinanceService.Id(row)))
                    .Select(row => new FleetFieldOption(FinanceService.Id(row), Read(row, "name"))).ToArray();
            }
            else
                choices["category"] = FinanceService.Read(workspace, "fleet", new List<FleetVehicle>())
                    .Select(v => v.Category).Where(v => !string.IsNullOrWhiteSpace(v))
                    .Distinct(StringComparer.OrdinalIgnoreCase).Select(v => new FleetFieldOption(v, v)).ToArray();
            var overview = module == "drivers" && original is { } driver ? await DriverOverviewAsync(driver, workspace) : [];
            Func<Task>? manageAccount = module == "drivers" && original is { } chauffeur && Read(chauffeur, "accountId") is { Length: > 0 } chauffeurId
                ? () => Navigation.PushAsync(new FleetAccountPage(api, chauffeurId))
                : null;
            var form = new FleetRecordPage(fields.Select(f => (f.Key, L(f.Label), original is { } row ? Read(row, f.Key) : "")).ToArray(),
                $"{(original is null ? L("Add") : L("Edit"))} · {Title}", choices, overview, manageAccount, module == "fleet", original, async patch =>
                {
                    await SaveSectionAsync(original, patch);
                });
            await Navigation.PushAsync(form);
            await form.Closed;
            await LoadAsync();
        }
        finally { editing = false; }
    }
    private List<JsonElement> ReadSection(WorkspaceEnvelope workspace)
    {
        if (!workspace.Data.TryGetValue(module, out var section) || section.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("The workspace did not return this section.");
        return section.EnumerateArray().ToList();
    }

    private async Task SaveSectionAsync(JsonElement? original, Dictionary<string, object?>? patch)
    {
        var action = original is null ? "Add" : patch is null ? "Delete" : "Edit";
        finance.Demand(permission, action);
        var workspace = await finance.WorkspaceAsync();
        var rows = ReadSection(workspace).Select(row => (object)row).ToList();
        if (original is { } existing)
        {
            var id = FinanceService.Id(existing);
            var index = rows.FindIndex(row => FinanceService.Id((JsonElement)row) == id);
            if (string.IsNullOrWhiteSpace(id) || index < 0 || !FinanceService.Equal((JsonElement)rows[index], existing))
                throw new InvalidOperationException("This record changed. Refresh the list and try again.");
            if (patch is null) rows.RemoveAt(index);
            else
            {
                var merged = existing.EnumerateObject().ToDictionary(field => field.Name, field => (object?)field.Value);
                foreach (var field in patch) merged[field.Key] = field.Value;
                rows[index] = merged;
            }
        }
        else if (patch is not null)
        {
            patch["id"] = Guid.NewGuid().ToString();
            rows.Add(patch);
        }
        await api.PutAsync<JsonElement>("api/workspace", new
        {
            version = workspace.Version,
            data = new Dictionary<string, object> { [module] = rows },
            action = $"{action} {module} record"
        });
        var confirmed = ReadSection(await finance.WorkspaceAsync());
        var recordId = original is { } old ? FinanceService.Id(old) : (string)patch!["id"]!;
        var saved = confirmed.FirstOrDefault(row => FinanceService.Id(row) == recordId);
        if (patch is null ? saved.ValueKind != JsonValueKind.Undefined : saved.ValueKind != JsonValueKind.Object ||
            patch.Any(field => !saved.TryGetProperty(field.Key, out var value) ||
                !FinanceService.Equal(value, JsonSerializer.SerializeToElement(field.Value, ApiRepository.Json))))
            throw new InvalidOperationException("The workspace did not confirm the saved changes. Refresh before retrying.");
    }

    private static ImageSource? VehiclePhoto(JsonElement row)
    {
        foreach (var key in new[] { "photoUrl", "imageUrl", "mainPhotoUrl" })
            if (Uri.TryCreate(Read(row, key), UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps &&
                (uri.Host.Equals("limosdispatch.com", StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith(".limosdispatch.com", StringComparison.OrdinalIgnoreCase)))
                return ImageSource.FromUri(uri);
        return null;
    }

    private static string L(string value) => LocalizationService.Text(value);
    private static string Read(JsonElement row, string key) => row.ValueKind == JsonValueKind.Object && row.TryGetProperty(key, out var v) ? v.ValueKind switch { JsonValueKind.String => v.GetString() ?? "", JsonValueKind.Number => v.GetRawText(), _ => "" } : "";
}
