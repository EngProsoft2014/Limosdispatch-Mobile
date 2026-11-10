namespace LimosDispatch.Mobile.Services;
public sealed class ReceiptService
{
    public async Task<Receipt?> CaptureAsync()
    {
        var permission = await Permissions.CheckStatusAsync<Permissions.Camera>();
        if (permission != PermissionStatus.Granted) permission = await Permissions.RequestAsync<Permissions.Camera>();
        if (permission != PermissionStatus.Granted)
            throw new InvalidOperationException("Camera permission is required to photograph the receipt. Enable Camera for LimosDispatch in your phone's app settings, then try again.");
        if (!MediaPicker.Default.IsCaptureSupported) throw new InvalidOperationException("This device does not have an available camera.");
        var photo = await MediaPicker.Default.CapturePhotoAsync(new MediaPickerOptions { Title = LocalizationService.Text("Photograph expense receipt"), MaximumWidth = 1600, MaximumHeight = 1600, CompressionQuality = 70 });
        return await ReadPhotoAsync(photo);
    }
    public async Task<Receipt?> PickAsync()
    {
        var photos = await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions { Title = LocalizationService.Text("Choose expense receipt"), SelectionLimit = 1, MaximumWidth = 1600, MaximumHeight = 1600, CompressionQuality = 70 });
        return await ReadPhotoAsync(photos.FirstOrDefault());
    }
    public async Task<Receipt?> PickFileAsync()
    {
        var file = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Choose receipt photo or PDF",
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                [DevicePlatform.Android] = ["application/pdf", "image/jpeg", "image/png", "image/webp"],
                [DevicePlatform.iOS] = ["com.adobe.pdf", "public.jpeg", "public.png", "org.webmproject.webp"],
                [DevicePlatform.WinUI] = [".pdf", ".jpg", ".jpeg", ".png", ".webp"]
            })
        });
        return await ReadPhotoAsync(file);
    }
    public static async Task OpenAsync(Receipt receipt)
    {
        var bytes = LimosDispatch.Core.Helpers.ReceiptFiles.Read(receipt);
        var mime = LimosDispatch.Core.Helpers.ReceiptFiles.DetectMime(bytes);
        var extension = mime == "application/pdf" ? ".pdf" : mime == "image/png" ? ".png" : mime == "image/webp" ? ".webp" : ".jpg";
        var path = Path.Combine(FileSystem.CacheDirectory, "receipt-" + Guid.NewGuid().ToString("N") + extension);
        await File.WriteAllBytesAsync(path, bytes);
        if (!await Launcher.Default.OpenAsync(new OpenFileRequest(receipt.Name, new ReadOnlyFile(path, mime))))
            throw new InvalidOperationException("No viewer is available for this file. Install a PDF viewer to open PDF receipts.");
    }
    private static async Task<Receipt?> ReadPhotoAsync(FileResult? photo)
    {
        if (photo is null) return null;
        await using var stream = await photo.OpenReadAsync();
        using var memory = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(buffer)) > 0)
        { if (memory.Length + read > LimosDispatch.Core.Helpers.ReceiptFiles.MaximumBytes) throw new InvalidOperationException("The receipt file exceeds 1.5 MB. Choose a smaller file."); await memory.WriteAsync(buffer.AsMemory(0, read)); }
        var bytes = memory.ToArray();
        return LimosDispatch.Core.Helpers.ReceiptFiles.Create(photo.FileName, bytes);
    }
}
