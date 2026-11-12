namespace LimosDispatch.Mobile.Controls;

internal static class FieldBorder
{
    public static Border Wrap(View content) => new()
    {
        Style = (Style)Application.Current!.Resources["Field"],
        Content = content
    };
}
