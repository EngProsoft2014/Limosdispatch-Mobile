namespace LimosDispatch.Mobile.Controls;

internal static class FilterDialog
{
    public static View Field(string title, View input) => new VerticalStackLayout
    {
        Spacing = 4,
        Children = { new Label { Text = LocalizationService.Text(title) }, FieldBorder.Wrap(input) }
    };

    public static View Pair(View first, View second)
    {
        var row = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Star)], ColumnSpacing = 8 };
        row.Add(first);
        row.Add(second, 1);
        return row;
    }

    public static async Task<bool> ShowAsync(INavigation navigation, params View[] fields)
    {
        var page = new FilterPage(fields);
        await navigation.PushModalAsync(page, false);
        return await page.Result;
    }

    private sealed class FilterPage : ContentPage
    {
        private readonly TaskCompletionSource<bool> result = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool closing;
        public Task<bool> Result => result.Task;

        public FilterPage(IEnumerable<View> fields)
        {
            BackgroundColor = Color.FromArgb("#99071018");
            var cancel = new Button { Text = LocalizationService.Text("Cancel"), Style = (Style)Application.Current!.Resources["Secondary"] };
            var apply = new Button { Text = LocalizationService.Text("Apply") };
            cancel.Clicked += (_, _) => Close(false);
            apply.Clicked += (_, _) => Close(true);
            var form = new VerticalStackLayout { Padding = 20, Spacing = 12 };
            form.Add(new Label { Text = LocalizationService.Text("Filters"), FontSize = 23, FontAttributes = FontAttributes.Bold });
            foreach (var field in fields) form.Add(field);
            form.Add(Pair(cancel, apply));
            var panel = new Border
            {
                StrokeThickness = 0,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 24 },
                MaximumWidthRequest = 460,
                VerticalOptions = LayoutOptions.Center,
                Padding = 0,
                Content = form
            };
            panel.SetDynamicResource(BackgroundColorProperty, "Canvas");
            Content = new ScrollView
            {
                Padding = 20,
                Content = new Grid { Children = { panel } }
            };
        }

        protected override bool OnBackButtonPressed()
        {
            Close(false);
            return true;
        }

        private async void Close(bool applied)
        {
            if (closing) return;
            closing = true;
            try
            {
                await Navigation.PopModalAsync(false);
                result.TrySetResult(applied);
            }
            catch (Exception ex) { result.TrySetException(ex); }
        }
    }
}
