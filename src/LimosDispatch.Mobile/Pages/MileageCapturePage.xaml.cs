using LimosDispatch.Mobile.Controls;
using LimosDispatch.Mobile.ViewModels;

namespace LimosDispatch.Mobile.Pages;

public partial class MileageCapturePage : ContentPage
{
    private readonly MileageCaptureViewModel model;
    private readonly TaskCompletionSource<bool> result = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool saved, closing;
    public Task<bool> Result => result.Task;

    public MileageCapturePage(MileageCaptureViewModel model)
    {
        InitializeComponent();
        BindingContext = this.model = model;
        FlowDirection = LocalizationService.Instance.Code == "ar" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        CloseButton.Text = LocalizationService.Text("Cancel");
        MilesField.Placeholder = LocalizationService.Text("Odometer reading (miles)");
        LateLabel.Text = LocalizationService.Text("Late reading");
        ReasonField.Placeholder = LocalizationService.Text("Reason");
        SaveButton.Text = LocalizationService.Text("Save");
        if (model.Shift is { } shift)
            VehicleField.Content = new Label { Text = shift.Vehicle, VerticalOptions = LayoutOptions.Center };
        else
        {
            var picker = new ChoicePicker { Title = LocalizationService.Text("Choose a vehicle"), DisplayMember = nameof(MileageVehicle.Display), ItemsSource = model.Vehicles };
            picker.SetBinding(ChoicePicker.SelectedItemProperty, nameof(model.SelectedVehicle));
            VehicleField.Content = picker;
        }
        model.Saved += OnSaved;
    }

    private async void OnSaved() => await LimosDispatch.Core.Helpers.UserFeedback.RunAsync(() => CloseAsync(true));
    private async void CloseClicked(object? sender, EventArgs e)
    {
        if (!model.IsBusy) await LimosDispatch.Core.Helpers.UserFeedback.RunAsync(() => CloseAsync(false));
    }
    protected override bool OnBackButtonPressed()
    {
        if (!model.IsBusy) _ = LimosDispatch.Core.Helpers.UserFeedback.RunAsync(() => CloseAsync(false));
        return true;
    }
    private async Task CloseAsync(bool success)
    {
        if (closing) return;
        closing = true;
        saved = success;
        await Navigation.PopModalAsync();
        model.Saved -= OnSaved;
        result.TrySetResult(saved);
    }

    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        base.OnNavigatedFrom(args);
        if (!Navigation.ModalStack.Contains(this))
        {
            model.Saved -= OnSaved;
            result.TrySetResult(saved);
        }
    }
}
