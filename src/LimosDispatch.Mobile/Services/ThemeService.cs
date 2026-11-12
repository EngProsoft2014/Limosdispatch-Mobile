namespace LimosDispatch.Mobile.Services;

public sealed class ThemeService
{
    public IReadOnlyList<string> Themes { get; } = ["Light", "Dark", "Ocean"];
    public string Selected => Preferences.Default.Get("app-theme", "Light");
    public void Apply(string theme)
    {
        if (!Themes.Contains(theme)) theme = "Light";
        Preferences.Default.Set("app-theme", theme);
        ApplyResources(Application.Current!.Resources, theme);
        Application.Current.UserAppTheme = theme == "Dark" ? AppTheme.Dark : AppTheme.Light;
    }
    public void Restore(ResourceDictionary resources) => ApplyResources(resources, Selected);
    private static void ApplyResources(ResourceDictionary resources, string theme)
    {
        var dark = theme == "Dark";
        var ocean = theme == "Ocean";
        var colors = new Dictionary<string, string>
        {
            ["Canvas"] = dark ? "#0D1720" : ocean ? "#ECF3F8" : "#F3F5F7",
            ["Surface"] = dark ? "#192936" : "#FFFFFF",
            ["FieldBackground"] = dark ? "#213541" : "#FFFFFF",
            ["FieldOutline"] = dark ? "#526C7E" : ocean ? "#A6BBCB" : "#B3BFC9",
            ["SoftSurface"] = dark ? "#243A48" : "#EDF2F5",
            ["Ink"] = dark ? "#EDF3F7" : "#152936",
            ["Muted"] = dark ? "#A9BBC7" : "#687B88",
            ["Line"] = dark ? "#344B5A" : "#DEE6EC",
            ["Gold"] = ocean ? "#9BCDDC" : "#B99B62",
            ["AccentText"] = dark ? "#DDC28D" : "#8C703A",
            ["TabBackground"] = dark ? "#1C303F" : ocean ? "#DAE8F1" : "#E7EDF2",
            ["SecondaryText"] = dark ? "#E8EFF4" : "#152936"
        };
        foreach (var (key, value) in colors) resources[key] = Color.FromArgb(value);
    }
}
