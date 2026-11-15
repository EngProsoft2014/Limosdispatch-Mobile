using System.Text.Json;
using System.Text.Json.Serialization;
using LimosDispatch.Core.Helpers;

namespace LimosDispatch.Mobile.Pages;

internal static class FleetMedia
{
    private const int MaxBytes = 4 * 1024 * 1024;
    private sealed record Attachment(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("data")] string Data);

    public static ImageSource? Preview(JsonElement row, string key) =>
        Read(row, key).FirstOrDefault() is { } item ? ImageFor(item) : null;

    public static void AddPicker(VerticalStackLayout form, IDictionary<string, object?> changes,
        JsonElement? original, string key, string title, bool multiple = false, bool document = false)
    {
        var items = Read(original, key);
        var section = new VerticalStackLayout { Spacing = 8 };
        section.Add(new Label { Text = LocalizationService.Text(title), FontAttributes = FontAttributes.Bold });
        var files = new VerticalStackLayout { Spacing = 6 };
        section.Add(files);
        var pick = AttachmentButton(document ? "Files" : "Photos", document ? "receipt_document.png" : "receipt_photos.png");
        var camera = AttachmentButton("Camera", "receipt_camera.png");
        var actions = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Star)], ColumnSpacing = 8 };
        actions.Add(camera); actions.Add(pick, 1);
        section.Add(actions);
        form.Add(new Border { Content = section, Padding = 12 });
        Render();

        pick.Clicked += async (_, _) => await SelectAsync(false);
        camera.Clicked += async (_, _) => await SelectAsync(true);

        async Task SelectAsync(bool capture)
        {
            if (!pick.IsEnabled) return;
            camera.IsEnabled = pick.IsEnabled = false;
            try
            {
                await UserFeedback.RunAsync(async () =>
                {
                    if (capture && (!MediaPicker.Default.IsCaptureSupported ||
                        await Permissions.RequestAsync<Permissions.Camera>() != PermissionStatus.Granted))
                        throw new InvalidOperationException(LocalizationService.Text("Camera is unavailable or permission was denied."));
                    var selected = capture
                        ? await MediaPicker.Default.CapturePhotoAsync(new MediaPickerOptions { MaximumWidth = 1600, MaximumHeight = 1600, CompressionQuality = 80 })
                        : await FilePicker.Default.PickAsync();
                    if (selected is null) return;
                    var extension = Path.GetExtension(selected.FileName).ToLowerInvariant();
                    var mime = extension switch
                    {
                        ".jpg" or ".jpeg" => "image/jpeg",
                        ".png" => "image/png",
                        ".webp" => "image/webp",
                        ".pdf" when document => "application/pdf",
                        _ => throw new InvalidOperationException("Use a JPG, PNG, WebP or supported PDF file.")
                    };
                    await using var stream = await selected.OpenReadAsync();
                    using var bytes = new MemoryStream();
                    var buffer = new byte[8192];
                    int read;
                    while ((read = await stream.ReadAsync(buffer)) > 0)
                    {
                        if (bytes.Length + read > MaxBytes) throw new InvalidOperationException("Choose a file under 4 MB.");
                        bytes.Write(buffer, 0, read);
                    }
                    if (bytes.Length == 0) throw new InvalidOperationException("The selected file is empty.");
                    if (multiple && items.Count >= 6) throw new InvalidOperationException("Remove a photo before adding another.");
                    var file = new Attachment(Path.GetFileName(selected.FileName),
                        $"data:{mime};base64,{Convert.ToBase64String(bytes.ToArray())}");
                    if (!multiple) items.Clear();
                    items.Add(file);
                    Commit();
                });
            }
            finally { camera.IsEnabled = pick.IsEnabled = true; }
        }

        void Commit()
        {
            changes[key] = multiple ? items.ToArray() : items.FirstOrDefault();
            Render();
        }

        void Render()
        {
            files.Clear();
            foreach (var item in items)
            {
                var row = new Grid { ColumnDefinitions = [new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto)], ColumnSpacing = 8 };
                if (ImageFor(item) is { } preview)
                {
                    var thumbnail = new Image { Source = preview, WidthRequest = 84, HeightRequest = 60, Aspect = Aspect.AspectFill };
                    thumbnail.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(async () => await UserFeedback.RunAsync(() => OpenPhotoAsync(preview))) });
                    row.Add(thumbnail);
                }
                row.Add(new Label { Text = item.Name, VerticalOptions = LayoutOptions.Center, LineBreakMode = LineBreakMode.TailTruncation }, 1);
                var remove = new Button { Text = LocalizationService.Text("Remove"), HeightRequest = 34, MinimumHeightRequest = 0, FontSize = 12, Padding = new Thickness(8, 0),
                    Style = (Style)Application.Current!.Resources["Secondary"] };
                remove.SetAppThemeColor(Button.TextColorProperty, Color.FromArgb("#B42318"), Color.FromArgb("#FFB4AB"));
                remove.SetDynamicResource(Button.BackgroundColorProperty, "FieldBackground");
                remove.Clicked += (_, _) => { items.Remove(item); Commit(); };
                row.Add(remove, 2);
                files.Add(row);
            }
        }
    }

    private static Button AttachmentButton(string label, string icon)
    {
        var button = new Button
        {
            Text = LocalizationService.Text(label),
            ImageSource = icon,
            ContentLayout = new Button.ButtonContentLayout(Button.ButtonContentLayout.ImagePosition.Top, 4),
            HeightRequest = 68,
            Padding = new Thickness(8),
            FontSize = 12,
            Style = (Style)Application.Current!.Resources["Secondary"]
        };
        return button;
    }

    public static async Task OpenPhotoAsync(ImageSource source)
    {
        var navigation = Application.Current!.Windows[0].Page!.Navigation;
        var close = new Button { Text = "×", WidthRequest = 44, HeightRequest = 44, HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Start, Margin = 12, BackgroundColor = Colors.White, TextColor = Colors.Black };
        SemanticProperties.SetDescription(close, LocalizationService.Text("Close"));
        close.Clicked += async (_, _) => await navigation.PopModalAsync();
        var layout = new Grid();
        layout.Add(new Image { Source = source, Aspect = Aspect.AspectFit, Margin = new Thickness(10, 58, 10, 10) });
        layout.Add(close);
        await navigation.PushModalAsync(new ContentPage { BackgroundColor = Colors.Black, Content = layout });
    }

    private static List<Attachment> Read(JsonElement? row, string key)
    {
        if (row is not { ValueKind: JsonValueKind.Object } record || !record.TryGetProperty(key, out var value)) return [];
        IEnumerable<JsonElement> values = value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().ToArray() : [value];
        return values.Where(item => item.ValueKind == JsonValueKind.Object &&
                item.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String &&
                item.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.String)
            .Select(item => new Attachment(item.GetProperty("name").GetString()!, item.GetProperty("data").GetString()!)).ToList();
    }

    private static ImageSource? ImageFor(Attachment file)
    {
        var comma = file.Data.IndexOf(',');
        if (comma < 0 || file.Data.Length - comma > (MaxBytes * 4 / 3) + 8 ||
            file.Data[..comma] is not ("data:image/jpeg;base64" or "data:image/png;base64" or "data:image/webp;base64" or "data:image/gif;base64"))
            return null;
        return ImageSource.FromStream(() =>
        {
            try { return new MemoryStream(Convert.FromBase64String(file.Data[(comma + 1)..]), writable: false); }
            catch (FormatException) { return Stream.Null; }
        });
    }
}
