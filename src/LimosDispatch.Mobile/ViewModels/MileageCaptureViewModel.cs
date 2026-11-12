using System.Globalization;
using LimosDispatch.Core.Helpers;

namespace LimosDispatch.Mobile.ViewModels;

public sealed class MileageCaptureViewModel : BaseViewModel
{
    private readonly MileageApi api;
    private readonly MileagePhotoService photos;
    private byte[]? photo;
    private string miles = "", readingStatus = "";
    private MileageVehicle? selectedVehicle;
    private (Guid Id, decimal Miles, string VehicleId, byte[] Photo, bool IsLate, string Reason)? request;
    private bool isLate;
    private string reason = "";
    public bool IsLate { get => isLate; set => Set(ref isLate, value); }
    public string Reason { get => reason; set => Set(ref reason, value); }

    public MileageShift? Shift { get; }
    public string Day { get; }
    public string Heading => LocalizationService.Text(Shift is null ? "Mileage check-in" : "Mileage check-out");
    public IReadOnlyList<MileageVehicle> Vehicles { get; }
    public MileageVehicle? SelectedVehicle { get => selectedVehicle; set => Set(ref selectedVehicle, value); }
    public string Miles { get => miles; set => Set(ref miles, value); }
    public string ReadingStatus { get => readingStatus; private set => Set(ref readingStatus, value); }
    public bool HasPhoto => photo is not null;
    public ImageSource? Preview => photo is { } image ? ImageSource.FromStream(() => new MemoryStream(image, writable: false)) : null;
    public AsyncCommand CameraCommand { get; }
    public AsyncCommand GalleryCommand { get; }
    public AsyncCommand SaveCommand { get; }
    public event Action? Saved;

    public MileageCaptureViewModel(MileageApi api, MileagePhotoService photos, MileageReport report, MileageShift? shift, string? vehicle)
    {
        this.api = api;
        this.photos = photos;
        Shift = shift;
        Day = shift?.Day ?? report.Today;
        Vehicles = report.Vehicles;
        SelectedVehicle = Vehicles.Count == 1 ? Vehicles[0] : Vehicles.FirstOrDefault(x =>
            !string.IsNullOrWhiteSpace(vehicle) && (string.Equals(x.Display, vehicle, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(x.Plate) && vehicle.EndsWith(x.Plate, StringComparison.OrdinalIgnoreCase))));
        CameraCommand = new(() => Run(() => PickAsync(true)));
        GalleryCommand = new(() => Run(() => PickAsync(false)));
        SaveCommand = new(() => Run(SaveAsync));
    }

    private async Task PickAsync(bool camera)
    {
        var image = await photos.PickAsync(camera);
        if (image is null) return;
        photo = image;
        Raise(nameof(Preview));
        Raise(nameof(HasPhoto));
        Miles = "";
        ReadingStatus = LocalizationService.Text("Reading the odometer…");
        try
        {
            var result = await api.ReadAsync(DataUrl(image));
            if (result.Miles is { } value)
            {
                Miles = value.ToString(CultureInfo.InvariantCulture);
                ReadingStatus = LocalizationService.Text("Verify the reading before saving.");
                return;
            }
        }
        catch (Exception ex) when (ex is LimosDispatch.Core.GenericRepository.ApiException or HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            ReadingStatus = LocalizationService.Text("Automatic reading is unavailable. Enter the odometer reading manually.");
            return;
        }
        ReadingStatus = LocalizationService.Text("Automatic reading is unavailable. Enter the odometer reading manually.");
    }

    private async Task SaveAsync()
    {
        if (photo is null) throw new InvalidOperationException("Add a photo of the odometer.");
        var vehicleId = Shift?.VehicleId ?? SelectedVehicle?.Id;
        if (string.IsNullOrWhiteSpace(vehicleId)) throw new InvalidOperationException("Choose a vehicle.");
        if (!InputValidation.TryDecimal(Miles, LocalizationService.Culture, out var value) || value < (Shift?.Start.Miles ?? 0))
            throw new InvalidOperationException("Enter a valid odometer reading.");
        var explanation = IsLate ? Reason.Trim() : string.Empty;
        if (IsLate && explanation.Length == 0)
            throw new InvalidOperationException("Enter a reason for the late reading.");
        if (request is not { } previous || previous.Miles != value || previous.VehicleId != vehicleId || previous.Photo != photo || previous.IsLate != IsLate || previous.Reason != explanation)
            request = (Guid.NewGuid(), value, vehicleId, photo, IsLate, explanation);
        await api.SaveAsync(request.Value.Id, Shift, vehicleId, value, DataUrl(photo), IsLate, explanation);
        Saved?.Invoke();
    }

    private static string DataUrl(byte[] image) => "data:image/jpeg;base64," + Convert.ToBase64String(image);
}
