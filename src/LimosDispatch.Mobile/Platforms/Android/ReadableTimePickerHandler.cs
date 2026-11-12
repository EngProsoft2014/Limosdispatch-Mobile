using Android.App;
using Android.Content;
using Microsoft.Maui.Handlers;

namespace LimosDispatch.Mobile;

internal sealed class ReadableTimePickerHandler : TimePickerHandler
{
    protected override TimePickerDialog CreateTimePickerDialog(int hour, int minute)
    {
        var dialog = base.CreateTimePickerDialog(hour, minute);
        dialog.ShowEvent += (_, _) =>
        {
            using var attributes = dialog.Context!.ObtainStyledAttributes([Android.Resource.Attribute.TextColorPrimary]);
            var color = attributes.GetColorStateList(0);
            if (color is null) return;

            dialog.GetButton((int)DialogButtonType.Positive)?.SetTextColor(color);
            dialog.GetButton((int)DialogButtonType.Negative)?.SetTextColor(color);
        };
        return dialog;
    }
}
