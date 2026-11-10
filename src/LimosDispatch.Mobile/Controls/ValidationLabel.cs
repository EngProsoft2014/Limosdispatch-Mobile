namespace LimosDispatch.Mobile.Controls;
public sealed class ValidationLabel : Label
{
    public ValidationLabel()
    {
        FontSize = 11; IsVisible = false; LineBreakMode = LineBreakMode.WordWrap;
        this.SetAppThemeColor(TextColorProperty, Color.FromArgb("#B3261E"), Color.FromArgb("#FFB4AB"));
    }
    protected override void OnPropertyChanged(string? name = null) { base.OnPropertyChanged(name); if (name == nameof(Text)) IsVisible = !string.IsNullOrWhiteSpace(Text); }
}
