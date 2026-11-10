using LimosDispatch.Mobile.Controls;
using LimosDispatch.Core.Helpers;
namespace LimosDispatch.Mobile.Pages;
public sealed partial class NotificationsPage : ContentPage
{
    private readonly RideNotifications inbox;
    private VerticalStackLayout rows => NotificationRows;
    private readonly RideLinkService links;
    private readonly PushService push;
    public NotificationsPage(RideNotifications inbox, RideLinkService links, PushService push)
    {
        InitializeComponent();
        this.inbox = inbox; this.links = links; this.push = push; Title = LocalizationService.Text("Notifications");
        SetDynamicResource(BackgroundColorProperty, "Canvas");
        Shell.SetTitleView(this, new CenteredBrandHeader { ShowNotifications = false });
        InboxRefresh.Command = new AsyncCommand(Refresh);
    }
    protected override async void OnAppearing() { base.OnAppearing(); inbox.Changed += Render; push.Changed += Render; Render(); await UserFeedback.RunAsync(Refresh); }
    protected override void OnDisappearing() { inbox.Changed -= Render; push.Changed -= Render; base.OnDisappearing(); }
    private async Task Refresh() { try { await inbox.RefreshAsync(); Render(); } finally { ((RefreshView)Content).IsRefreshing = false; } }
    private void Render()
    {
        rows.Clear();
        rows.Add(new Label { Text = LocalizationService.Text("Notifications"), FontSize = 22, FontFamily = LocalizationService.Instance.Code == "ar" ? "ElMessiriBold" : "OpenSansSemibold" });
        rows.Add(new Label { Text = LocalizationService.Text("Tap a notification to view the journey details."), FontSize = 13 });
        rows.Add(new Label { Text = LocalizationService.Text(push.Status), FontSize = 12, Style = (Style)Application.Current!.Resources["MutedText"] });
        if (!push.Registered) rows.Add(new Button { Text = LocalizationService.Text("Enable notifications"), Command = new AsyncCommand(push.StartSessionAsync), Style = (Style)Application.Current!.Resources["Secondary"] });
        if (inbox.Notices.Count == 0) rows.Add(new Label { Text = LocalizationService.Text("You’re all caught up."), Margin = new Thickness(0, 28), FontSize = 15 });
        if (inbox.UnreadCount > 0) rows.Add(new Button { Text = LocalizationService.Text("Mark all as read"), Command = new AsyncCommand(inbox.MarkAllReadAsync), Style = (Style)Application.Current!.Resources["Secondary"] });
        foreach (var notice in inbox.Notices)
        {
            var target = PushTarget.Create(notice.Company, notice.TripId, notice.Id);
            if (target is null) continue;
            var text = new VerticalStackLayout { Spacing = 7 };
            var heading = new Label { Text = (inbox.IsUnread(notice) ? "●  " : "") + LocalizationService.Text(notice.Type switch { "ride_assigned" => "New ride assigned", "website_booking_created" => "New website booking", "trip_extra_requested" => "New extra request", _ => notice.Title }), FontSize = 16, FontFamily = LocalizationService.Instance.Code == "ar" ? "ElMessiriBold" : "OpenSansSemibold" };
            heading.SetDynamicResource(Label.TextColorProperty, inbox.IsUnread(notice) ? "AccentText" : "Ink");
            text.Add(heading); text.Add(new Label { Text = LocalizationService.Text(notice.Body), FontSize = 14, MaxLines = 3 });
            text.Add(new Label { Text = notice.CreatedAt.ToLocalTime().ToString("g"), FontSize = 12 });
            text.InputTransparent = true;
            var card = new Border { Content = text };
            card.GestureRecognizers.Add(new TapGestureRecognizer { Command = new AsyncCommand(async () => { await links.OpenAsync(target); }) });
            rows.Add(card);
        }
        if (inbox.NextCursor is not null) rows.Add(new Button { Text = LocalizationService.Text("Load more"), Command = new AsyncCommand(inbox.LoadMoreAsync) });
    }
}
