namespace LimosDispatch.Mobile.Services;

// Shell tab labels are native controls and do not inherit XAML label styles.
internal static class NativeTabFonts
{
    internal static void Apply()
    {
#if ANDROID
        var root = Platform.CurrentActivity?.Window?.DecorView;
        if (root is null) return;
        var arabic = LocalizationService.Instance.Code == "ar";
        using var font = Android.Graphics.Typeface.CreateFromAsset(root.Context!.Assets!,
            arabic ? "ElMessiri-Bold.ttf" : "OpenSans-Semibold.ttf");
        void Visit(Android.Views.View view, bool tab = false)
        {
            tab |= view is Google.Android.Material.BottomNavigation.BottomNavigationView;
            if (tab && view is Android.Widget.TextView label) label.Typeface = font;
            if (view is Android.Views.ViewGroup group)
                for (var i = 0; i < group.ChildCount; i++)
                    if (group.GetChildAt(i) is { } child) Visit(child, tab);
        }
        Visit(root);
#elif IOS
        var root = Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView as UIKit.UIWindow;
        if (root is null) return;
        var name = LocalizationService.Instance.Code == "ar" ? "ElMessiri-Bold" : "OpenSans-SemiBold";
        using var font = UIKit.UIFont.FromName(name, 12) ?? UIKit.UIFont.SystemFontOfSize(12);
        void Visit(UIKit.UIView view)
        {
            if (view is UIKit.UITabBar bar && bar.Items is { } items)
                foreach (var item in items)
                    foreach (var state in new[] { UIKit.UIControlState.Normal, UIKit.UIControlState.Selected })
                        item.SetTitleTextAttributes(new UIKit.UIStringAttributes { Font = font }, state);
            foreach (var child in view.Subviews) Visit(child);
        }
        Visit(root);
#endif
    }
}
