using LimosDispatch.Mobile.Controls;
namespace LimosDispatch.Mobile.Pages;

public sealed partial class MoreMenuPage : ContentPage
{
    private readonly NavigationService navigation;
    private readonly ViewModels.MoreViewModel viewModel;
    public MoreMenuPage(NavigationService nav, ViewModels.MoreViewModel model)
    {
        InitializeComponent();
        navigation = nav; viewModel = model; BuildMenu();
    }
    private void BuildMenu()
    {
        var nav = navigation; var model = viewModel; MenuRows.Clear();
        Title = LocalizationService.Text("More");
        SetDynamicResource(BackgroundColorProperty, "Canvas");
        Shell.SetTitleView(this, new CenteredBrandHeader { Title = LocalizationService.Text("More") });
        var stack = MenuRows;
        var caption = new Label { Text = LocalizationService.Text("YOUR WORKSPACE"), FontSize = 11, Margin = new Thickness(0, 8, 0, 4) };
        caption.SetDynamicResource(Label.TextColorProperty, "AccentText"); stack.Add(caption);
        AddRow("account_menu.png", "Account", "Your profile, preferences and sign out", () => nav.PushAsync<MorePage>());
        var themePicker = new ChoicePicker { Title = LocalizationService.Text("App theme"), BindingContext = model, ItemsSource = model.Themes };
        themePicker.SetBinding(ChoicePicker.SelectedItemProperty, nameof(model.SelectedTheme));
        AddRow("theme_menu.png", "Theme", "Choose the appearance that suits you", themePicker.OpenAsync);
        var languagePicker = new ChoicePicker { Title = LocalizationService.Text("Language"), ItemsSource = model.Languages, DisplayMember = "Name", BindingContext = model };
        languagePicker.SetBinding(ChoicePicker.SelectedItemProperty, nameof(model.SelectedLanguage));
        AddRow("language.png", "Language", "Choose your preferred language", async () => { await languagePicker.OpenAsync(); BuildMenu(); });
        if(model.HasDriverSettings)AddRow("sync_menu.png", "Synchronization", "Pending journeys and location uploads", () => nav.PushAsync<SynchronizationPage>());
        var signOut = new Button { Text = LocalizationService.Text("Sign out"), Command = model.LogoutCommand, Margin = new Thickness(0, 8, 0, 0), HeightRequest = 44 };
        signOut.SetDynamicResource(Button.BackgroundColorProperty, "Surface");
        signOut.SetDynamicResource(Button.TextColorProperty, "AccentText");
        stack.Add(signOut);
        stack.Add(new Label { Text = $"LimosDispatch · {AppInfo.Current.VersionString}", FontSize = 12, Margin = new Thickness(4, 12) });
        void AddRow(string icon, string title, string subtitle, Func<Task> action)
        {
            var grid = new Grid { ColumnDefinitions = [new ColumnDefinition(44), new ColumnDefinition(GridLength.Star)], ColumnSpacing = 12, Padding = 4 };
            grid.Add(new Image { Source = icon, HeightRequest = 25, WidthRequest = 25 });
            var texts = new VerticalStackLayout { Spacing = 5, VerticalOptions = LayoutOptions.Center };
            texts.Add(new Label { Text = LocalizationService.Text(title), FontSize = 16, FontFamily = LocalizationService.Instance.Code == "ar" ? "ElMessiriBold" : "OpenSansSemibold" });
            var detail = new Label { Text = LocalizationService.Text(subtitle), FontSize = 12 }; detail.SetDynamicResource(Label.TextColorProperty, "Muted"); texts.Add(detail);
            grid.Add(texts, 1);
            grid.InputTransparent = true;
            var card = new Border { Content = grid, MinimumHeightRequest = 80 };
            var tap = new TapGestureRecognizer { Command = new AsyncCommand(action) }; card.GestureRecognizers.Add(tap);
            SemanticProperties.SetDescription(card, title); stack.Add(card);
        }
    }
}
