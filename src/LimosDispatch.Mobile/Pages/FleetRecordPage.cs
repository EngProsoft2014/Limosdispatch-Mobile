using System.Text.Json;
using System.Net.Mail;
using LimosDispatch.Core.Helpers;
namespace LimosDispatch.Mobile.Pages;

internal sealed record FleetFieldOption(string Value, string Label);

internal sealed class FleetRecordPage : ContentPage
{
    private readonly TaskCompletionSource closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task Closed => closed.Task;

    public FleetRecordPage((string Key, string Label, string Value)[] fields, string title, IReadOnlyDictionary<string, FleetFieldOption[]> choices, IReadOnlyList<(string Title, string Body)> overview, Func<Task>? manageAccount, bool vehicle, JsonElement? original, Func<Dictionary<string, object?>, Task> save)
    {
        Title = title;
        SetDynamicResource(BackgroundColorProperty, "Canvas");
        Shell.SetTitleView(this, new Controls.CenteredBrandHeader());
        var stack = new VerticalStackLayout { Padding = 16, Spacing = 12 };
        var heading = new Label { Text = title, Style = (Style)Application.Current!.Resources["Title"], Margin = new Thickness(16, 12) };
        foreach (var section in overview)
        {
            var card = new VerticalStackLayout { Spacing = 6 };
            card.Add(new Label { Text = section.Title, FontAttributes = FontAttributes.Bold, FontSize = 16 });
            card.Add(new Label { Text = section.Body, Style = (Style)Application.Current!.Resources["MutedText"] });
            var panel = new Border { Content = card, Padding = 12 };
            panel.SetDynamicResource(BackgroundColorProperty, "Surface");
            stack.Add(panel);
        }
        VerticalStackLayout? sectionFields = null;
        void StartSection(string title)
        {
            sectionFields = new VerticalStackLayout { Spacing = 10 };
            var sectionLabel = new Label { Text = LocalizationService.Text(title), FontSize = 16, FontAttributes = FontAttributes.Bold };
            sectionLabel.SetDynamicResource(Label.TextColorProperty, "Ink");
            sectionFields.Add(sectionLabel);
            var panel = new Border { Content = sectionFields, Padding = 14 };
            panel.SetDynamicResource(BackgroundColorProperty, "Surface");
            stack.Add(panel);
        }

        var readers = new Dictionary<string, Func<object?>>();
        foreach (var field in fields)
        {
            switch (field.Key)
            {
                case "name": StartSection(vehicle ? "Vehicle details" : "Contact details"); break;
                case "license": StartSection("Driver and vehicle"); break;
                case "insurance": StartSection("Insurance and availability"); break;
                case "limoLicenseNumber": StartSection("Limo license"); break;
                case "paySystemId": StartSection("Trip compensation"); break;
            }
            var label = new Label { Text = field.Label, FontAttributes = FontAttributes.Bold, FontSize = 14 };
            label.SetDynamicResource(Label.TextColorProperty, "Ink");
            var group = new VerticalStackLayout { Spacing = 6 };
            group.Add(label);
            if (field.Key is "insurance" or "limoLicenseExpiry")
            {
                var chosen = field.Value;
                var dateText = new Label { VerticalOptions = LayoutOptions.Center, FontSize = 13 };
                void UpdateDate()
                {
                    var parsed = DateTime.TryParseExact(chosen, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out var date);
                    dateText.Text = parsed ? date.ToString("dd MMM yyyy", LocalizationService.Culture)
                        : chosen.Length > 0 ? chosen : LocalizationService.Text("Select date");
                    dateText.SetDynamicResource(Label.TextColorProperty, chosen.Length > 0 ? "Ink" : "Muted");
                }
                UpdateDate();
                var dateRow = new Grid { HeightRequest = 44, ColumnDefinitions = [new(GridLength.Star), new(GridLength.Auto)], ColumnSpacing = 8 };
                dateRow.Add(dateText);
                dateRow.Add(new Image { Source = "calendar.png", WidthRequest = 18, HeightRequest = 18, VerticalOptions = LayoutOptions.Center }, 1);
                var opening = false;
                dateRow.GestureRecognizers.Add(new TapGestureRecognizer { Command = new AsyncCommand(() => UserFeedback.RunAsync(async () =>
                {
                    if (opening) return;
                    opening = true;
                    try
                    {
                        var initial = DateTime.TryParseExact(chosen, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.None, out var date) ? date : DateTime.Today;
                        var popup = new Controls.CalendarPopup(initial, new DateTime(1900, 1, 1), new DateTime(2100, 12, 31));
                        await Navigation.PushModalAsync(popup, false);
                        if (await popup.Result is { } selected)
                        {
                            chosen = selected.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                            UpdateDate();
                        }
                    }
                    finally { opening = false; }
                })) });
                var dateField = InputShell(dateRow);
                SemanticProperties.SetDescription(dateField, field.Label);
                group.Add(dateField); (sectionFields ?? stack).Add(group);
                readers[field.Key] = () => chosen;
                continue;
            }
            if (choices.TryGetValue(field.Key, out var options) && options.Length > 0)
            {
                var available = options.ToList();
                if (field.Value.Length > 0 && !available.Any(o => o.Value == field.Value))
                    available.Insert(0, new FleetFieldOption(field.Value, field.Value));
                if (field.Key is "vehicle" or "paySystemId")
                    available.Insert(0, new FleetFieldOption("", LocalizationService.Text("Unassigned")));
                var picker = new Controls.ChoicePicker { Title = field.Label, ItemsSource = available, DisplayMember = nameof(FleetFieldOption.Label),
                    SelectedItem = available.FirstOrDefault(o => o.Value == field.Value) ?? (field.Key == "status" ? available[0] : null) };
                group.Add(InputShell(picker)); (sectionFields ?? stack).Add(group);
                readers[field.Key] = () => (picker.SelectedItem as FleetFieldOption)?.Value ?? field.Value;
                continue;
            }
            var entry = new Entry { Text = field.Value, HeightRequest = 44,
                Keyboard = field.Key == "email" ? Keyboard.Email : field.Key == "phone" ? Keyboard.Telephone : field.Key is "odometer" or "seats" ? Keyboard.Numeric : Keyboard.Default };
            SemanticProperties.SetDescription(entry, field.Label);
            entry.BackgroundColor = Colors.Transparent;
            entry.SetDynamicResource(Entry.TextColorProperty, "Ink");
            var border = InputShell(entry);
            entry.Focused += (_, _) => border.SetDynamicResource(Border.StrokeProperty, "AccentText");
            entry.Unfocused += (_, _) => border.SetDynamicResource(Border.StrokeProperty, "FieldOutline");
            group.Add(border);
            (sectionFields ?? stack).Add(group);
            readers[field.Key] = () => (entry.Text ?? "").Trim();
        }
        if (manageAccount is not null)
        {
            var account = new VerticalStackLayout { Spacing = 8 };
            account.Add(new Label { Text = LocalizationService.Text("Permissions & account"), FontSize = 17, FontAttributes = FontAttributes.Bold });
            var action = new Button { Text = LocalizationService.Text("Manage permissions"), HeightRequest = 44,
                Style = (Style)Application.Current!.Resources["Secondary"] };
            action.Clicked += async (_, _) => await UserFeedback.RunAsync(manageAccount);
            account.Add(action);
            stack.Add(new Border { Content = account, Padding = 12, Margin = new Thickness(0, 6) });
        }
        var media = new Dictionary<string, object?>();
        FleetMedia.AddPicker(stack, media, original, vehicle ? "main" : "photo", vehicle ? "Main vehicle photo" : "Driver photo");
        FleetMedia.AddPicker(stack, media, original, vehicle ? "gallery" : "limoLicense", vehicle ? "Additional vehicle photos" : "Limo license document",
            multiple: vehicle, document: !vehicle);
        var submit = new Button { Text = LocalizationService.Text("Save"), HeightRequest = 44 };
        var progress = new ActivityIndicator { IsVisible = false };
        submit.Clicked += async (_, _) =>
        {
            if (!submit.IsEnabled) return;
            submit.IsEnabled = false; progress.IsVisible = progress.IsRunning = true;
            try
            {
                await UserFeedback.RunAsync(async () =>
                {
                    var patch = readers.ToDictionary(e => e.Key, e => e.Value());
                    foreach (var key in new[] { "seats", "odometer" })
                        if (patch.TryGetValue(key, out var raw) && raw is string digits)
                        {
                            if (digits.Length == 0) { patch.Remove(key); continue; }
                            if (!int.TryParse(digits, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var numberValue) || (key == "seats" && numberValue == 0))
                                throw new InvalidOperationException(LocalizationService.Text("Enter a valid passenger capacity or odometer reading."));
                            patch[key] = key == "seats" ? digits : (object)numberValue;
                        }
                    foreach (var key in new[] { "insurance", "limoLicenseExpiry" })
                        if (patch.TryGetValue(key, out var rawDate) && rawDate is string date && date.Length > 0 &&
                            !DateTime.TryParseExact(date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                            throw new InvalidOperationException(LocalizationService.Text("Enter the expiry date as YYYY-MM-DD."));
                    if ((string)patch["name"]! == "") throw new InvalidOperationException("Enter a name.");
                    if (patch.TryGetValue("email", out var email) && email is string address && address.Length > 0 && (!MailAddress.TryCreate(address, out var parsed) || parsed.Address != address))
                        throw new InvalidOperationException("Enter a valid email address.");
                    if (patch.TryGetValue("phone", out var phone) && phone is string number && number.Length > 0 && (number.Count(char.IsDigit) is < 7 or > 15 || number.Any(c => !char.IsDigit(c) && !"+()- .".Contains(c))))
                        throw new InvalidOperationException("Enter a valid phone number.");
                    foreach (var file in media) patch[file.Key] = file.Value;
                    await save(patch);
                    await Navigation.PopAsync();
                });
            }
            finally { submit.IsEnabled = true; progress.IsVisible = progress.IsRunning = false; }
        };
        var cancel = new Button { Text = LocalizationService.Text("Cancel"), HeightRequest = 44, Style = (Style)Application.Current!.Resources["Secondary"] };
        cancel.Clicked += async (_, _) => { if (submit.IsEnabled) await Navigation.PopAsync(); };
        var actions = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Star)], ColumnSpacing = 8 };
        actions.Add(cancel); actions.Add(submit, 1);
        var footer = new VerticalStackLayout { Padding = new Thickness(16, 8), Spacing = 4 };
        footer.SetDynamicResource(BackgroundColorProperty, "Surface");
        footer.Add(progress); footer.Add(actions);
        var layout = new Grid { RowDefinitions = [new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto)] };
        layout.Add(heading); layout.Add(new ScrollView { Content = stack }, 0, 1); layout.Add(footer, 0, 2);
        Content = layout;
    }
    private static Border InputShell(View input)
    {
        var border = new Border { Content = input, Padding = new Thickness(12, 0), StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(12) } };
        border.SetDynamicResource(BackgroundColorProperty, "FieldBackground");
        border.SetDynamicResource(Border.StrokeProperty, "FieldOutline");
        return border;
    }
    protected override void OnDisappearing() { base.OnDisappearing(); closed.TrySetResult(); }
}
