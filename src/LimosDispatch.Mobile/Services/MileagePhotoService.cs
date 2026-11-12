namespace LimosDispatch.Mobile.Services;

public sealed class MileagePhotoService
{
    public async Task<byte[]?> PickAsync(bool camera)
    {
        if (camera && (await Permissions.RequestAsync<Permissions.Camera>()) != PermissionStatus.Granted)
            throw new InvalidOperationException("Allow camera access or choose an odometer photo from your gallery.");

        var options = new MediaPickerOptions { SelectionLimit = 1, MaximumWidth = 1600, MaximumHeight = 1600, CompressionQuality = 80, RotateImage = true };
        var file = camera ? await MediaPicker.Default.CapturePhotoAsync(options) : (await MediaPicker.Default.PickPhotosAsync(options)).FirstOrDefault();
        if (file is null) return null;
        await using var input = await file.OpenReadAsync();
        using var memory = new MemoryStream();
        await input.CopyToAsync(memory);
        var bytes = memory.ToArray();
        if (bytes.Length is > 2 and <= 2_000_000 && bytes[0] == 255 && bytes[1] == 216) return bytes;

#if ANDROID
        using var image = Android.Graphics.BitmapFactory.DecodeByteArray(bytes, 0, bytes.Length);
        if (image is not null)
        {
            using var jpeg = new MemoryStream();
            if (image.Compress(Android.Graphics.Bitmap.CompressFormat.Jpeg!, 70, jpeg) && jpeg.Length is > 0 and <= 2_000_000)
                return jpeg.ToArray();
        }
#elif IOS
        using var data = Foundation.NSData.FromArray(bytes);
        using var image = UIKit.UIImage.LoadFromData(data);
        using var jpeg = image?.AsJPEG(0.7f);
        if (jpeg is not null && jpeg.Length is > 0 and <= 2_000_000) return jpeg.ToArray();
#endif
        throw new InvalidOperationException("Choose a readable odometer photo under 2 MB.");
    }
}
