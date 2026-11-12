using LimosDispatch.Mobile.Controls;
using LimosDispatch.Mobile.Pages;
using LimosDispatch.Mobile.ViewModels;

namespace LimosDispatch.Mobile.Services;

public sealed class MileageWorkflow(MileageApi api, MileagePhotoService photos)
{
    private bool busy;

    public async Task ShowReminderAsync(MileageReminder? reminder, Func<bool> stillValid)
    {
        if (reminder is not { Action: "open_mileage", Message: { } message } ||
            string.IsNullOrWhiteSpace(message) || !stillValid()) return;
        if (await FeedbackService.ConfirmAsync(message, "Mileage check-out", stillValid) && stillValid())
            await OpenAsync(checkout: true);
    }

    public async Task OpenAsync(bool checkout)
    {
        if (busy) return;
        busy = true;
        try
        {
            var report = await api.ListAsync();
            MileageShift? shift = null;
            if (checkout)
            {
                var shifts = report.Open.OrderByDescending(x => x.Day).ToArray();
                if (shifts.Length == 0) throw new InvalidOperationException("No open mileage shift.");
                shift = shifts[0];
                if (shifts.Length > 1)
                {
                    var options = shifts.Select(x => new ShiftOption(x, $"{x.Day} · {x.Vehicle}")).ToArray();
                    var picker = new ChoicePicker
                    {
                        Title = LocalizationService.Text("Mileage check-out"),
                        ItemsSource = options,
                        DisplayMember = nameof(ShiftOption.Label)
                    };
                    await picker.OpenAsync();
                    if (picker.SelectedItem is not ShiftOption selected) return;
                    shift = selected.Shift;
                }
            }
            else if (report.Vehicles.Count == 0)
                throw new InvalidOperationException("No active vehicle is available.");

            var page = new MileageCapturePage(new MileageCaptureViewModel(api, photos, report, shift, null));
            await Application.Current!.Windows[0].Page!.Navigation.PushModalAsync(page);
            await page.Result;
        }
        finally { busy = false; }
    }

    private sealed record ShiftOption(MileageShift Shift, string Label);
}
