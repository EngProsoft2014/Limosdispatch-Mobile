using System.Net.Mail;
using System.Text.Json.Nodes;
using LimosDispatch.Core.GenericRepository;
using LimosDispatch.Core.Helpers;

namespace LimosDispatch.Mobile.Pages;

internal sealed class FleetAccountPage : ContentPage
{
    private readonly IGenericRepository api;
    private readonly string route, chauffeurId;
    private readonly VerticalStackLayout form = new() { Padding = 16, Spacing = 10 };
    private bool loaded;

    public FleetAccountPage(IGenericRepository api, string id)
    {
        this.api = api; chauffeurId = id;
        route = "api/accounts?id=" + Uri.EscapeDataString(id);
        Title = L("Account permissions");
        SetDynamicResource(BackgroundColorProperty, "Canvas");
        Shell.SetTitleView(this, new Controls.CenteredBrandHeader());
        form.Add(new Label { Text = Title, Style = (Style)Application.Current!.Resources["Title"] });
        Content = new ScrollView { Content = form };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (loaded) return;
        await UserFeedback.RunAsync(async () =>
        {
            var account = await ReadAccountAsync();
            if (account["permissions"] is not JsonObject permissions ||
                account["accountType"] is not JsonValue || account["status"] is not JsonValue)
                throw new InvalidOperationException("The server did not return editable account permissions.");
            Build(account, permissions);
            loaded = true;
        });
    }

    private async Task<JsonObject> ReadAccountAsync()
    {
        var response = await api.GetAsync<JsonNode>(route);
        if (response is JsonObject root && root["account"] is JsonObject account) return account;
        if (response is JsonObject direct && direct.ContainsKey("permissions")) return direct;
        var rows = response is JsonObject wrapper ? wrapper["accounts"] as JsonArray : response as JsonArray;
        if (rows is not null)
        {
            // Do not use another chauffeur's account as an editing fallback.
            var matched = rows.OfType<JsonObject>().Where(item =>
                string.Equals(Read(item, "driverId"), chauffeurId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Read(item, "id"), chauffeurId, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (matched.Count == 1) return matched[0];
        }
        throw new InvalidOperationException("The server did not return an account linked to this chauffeur.");
    }

    private void Build(JsonObject account, JsonObject permissions)
    {
        form.Clear();
        form.Add(new Label { Text = Title, Style = (Style)Application.Current!.Resources["Title"] });
        var name = Read(account, "name");
        form.Add(new Label { Text = name, FontAttributes = FontAttributes.Bold });
        var email = new Entry { Text = Read(account, "email"), Keyboard = Keyboard.Email, HeightRequest = 44 };
        var password = new Entry { IsPassword = true, HeightRequest = 44, Placeholder = L("Leave blank to keep current password") };
        Field("Login email", email);
        var role = Choice("Account type", Read(account, "accountType"), ["Driver", "Dispatch", "Admin"]);
        var status = Choice("Account status", Read(account, "status"), ["Active", "Inactive"]);
        Field("Temporary password", password);
        form.Add(new Label { Text = L("Screen permissions"), FontSize = 18, FontAttributes = FontAttributes.Bold });
        var draft = (JsonObject)permissions.DeepClone();
        var actions = new[] { "View", "Add", "Edit", "Delete" };
        foreach (var module in draft.ToArray())
        {
            if (module.Value is not JsonObject flags) continue;
            var items = new VerticalStackLayout { Spacing = 4 };
            items.Add(new Label { Text = L(module.Key), FontAttributes = FontAttributes.Bold });
            var grid = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Star), new(GridLength.Star), new(GridLength.Star)] };
            for (var i = 0; i < actions.Length; i++)
            {
                var action = actions[i];
                var key = flags.Select(x => x.Key).FirstOrDefault(k => k.Equals(action, StringComparison.OrdinalIgnoreCase));
                if (key is null || flags[key] is not JsonValue value || !value.TryGetValue<bool>(out var enabled)) continue;
                var box = new CheckBox { IsChecked = enabled, HorizontalOptions = LayoutOptions.Center };
                SemanticProperties.SetDescription(box, $"{L(action)} {L(module.Key)}");
                box.CheckedChanged += (_, e) => flags[key] = e.Value;
                var column = new VerticalStackLayout();
                column.Add(new Label { Text = L(action), HorizontalTextAlignment = TextAlignment.Center, FontSize = 11 });
                column.Add(box);
                grid.Add(column, i);
            }
            items.Add(grid);
            form.Add(new Border { Content = items, Padding = 12 });
        }
        var save = new Button { Text = L("Save changes"), HeightRequest = 44 };
        save.Clicked += async (_, _) =>
        {
            if (!save.IsEnabled) return;
            save.IsEnabled = false;
            try
            {
                await UserFeedback.RunAsync(async () =>
                {
                    var address = email.Text?.Trim() ?? "";
                    if (!MailAddress.TryCreate(address, out var parsed) || parsed.Address != address)
                        throw new InvalidOperationException("Enter a valid email address.");
                    var secret = password.Text ?? "";
                    if (secret.Length is > 0 and < 12) throw new InvalidOperationException("Use at least 12 characters for the temporary password.");
                    if (!await FeedbackService.ConfirmAsync(L("Save changes"), "Confirm")) return;
                    var current = await ReadAccountAsync();
                    foreach (var key in new[] { "name", "email", "accountType", "status", "kind", "permissions" })
                        if (!JsonNode.DeepEquals(account[key], current[key]))
                            throw new InvalidOperationException("Account settings have changed. Reopen the screen and review them before saving.");
                    var payload = new JsonObject
                    {
                        ["name"] = name, ["email"] = address,
                        ["accountType"] = (string?)role.SelectedItem, ["status"] = (string?)status.SelectedItem,
                        ["permissions"] = draft.DeepClone()
                    };
                    if (account["kind"] is JsonValue kind) payload["kind"] = kind.DeepClone();
                    payload["tempPassword"] = secret.Length > 0 ? secret : null;
                    await api.PutAsync<System.Text.Json.JsonElement>(route, payload);
                    password.Text = "";
                    await Navigation.PopAsync();
                });
            }
            finally { save.IsEnabled = true; }
        };
        form.Add(save);
    }

    private Controls.ChoicePicker Choice(string label, string value, string[] choices)
    {
        var picker = new Controls.ChoicePicker { Title = L(label), ItemsSource = choices.Append(value).Distinct().ToArray(), SelectedItem = value };
        Field(label, picker);
        return picker;
    }

    private void Field(string label, View view)
    {
        form.Add(new Label { Text = L(label), FontAttributes = FontAttributes.Bold });
        if (view is Entry entry)
        {
            entry.BackgroundColor = Colors.Transparent;
            entry.SetDynamicResource(Entry.TextColorProperty, "Ink");
        }
        var border = new Border { Content = view, Padding = new Thickness(12, 0), StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(12) } };
        border.SetDynamicResource(BackgroundColorProperty, "FieldBackground");
        border.SetDynamicResource(Border.StrokeProperty, "FieldOutline");
        form.Add(border);
    }

    private static string Read(JsonObject row, string key) => row[key]?.GetValue<string>() ?? "";
    private static string L(string text) => LocalizationService.Text(text);
}
