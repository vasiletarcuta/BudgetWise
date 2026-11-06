using BudgetWise.Services;
using MauiPath = Microsoft.Maui.Controls.Shapes.Path;

namespace BudgetWise.Controls;

public partial class BottomNavigation : ContentView
{
    public static readonly BindableProperty ActiveTabProperty =
        BindableProperty.Create(
            nameof(ActiveTab), typeof(string), typeof(BottomNavigation), "home",
            propertyChanged: (bindable, _, _) =>
                ((BottomNavigation)bindable).UpdateActiveTab());

    public string ActiveTab
    {
        get => (string)GetValue(ActiveTabProperty);
        set => SetValue(ActiveTabProperty, value);
    }

    public BottomNavigation()
    {
        InitializeComponent();
        UpdateActiveTab();
    }

    private void UpdateActiveTab()
    {
        if (HomeTab is null) return;

        SetTabStyle(HomeTab, HomeIcon, HomeText, ActiveTab == "home");
        SetTabStyle(ActivityTab, ActivityIcon, ActivityText, ActiveTab == "activity");
        SetTabStyle(BudgetsTab, BudgetsIcon, BudgetsText, ActiveTab == "budgets");
        SetTabStyle(StatsTab, StatsIcon, StatsText, ActiveTab == "stats");
    }

    private static void SetTabStyle(Border tab, MauiPath icon, Label text, bool selected)
    {
        var tint = selected ? Palette.PrimaryText : Palette.Muted;
        tab.BackgroundColor = Colors.Transparent;
        tab.Stroke = new SolidColorBrush(selected ? Palette.PrimaryText : Palette.CardAlt);
        tab.StrokeThickness = selected ? 2 : 0;
        icon.Stroke = new SolidColorBrush(tint);
        text.TextColor = tint;
        text.Text = text.Text.ToUpperInvariant();
        text.FontAttributes = FontAttributes.Bold;
    }

    private async void OnHomeTapped(object? sender, TappedEventArgs e)
    {
        await NavigateToAsync("home");
    }

    private async void OnActivityTapped(object? sender, TappedEventArgs e)
    {
        await NavigateToAsync("activity");
    }

    private async void OnBudgetsTapped(object? sender, TappedEventArgs e)
    {
        await NavigateToAsync("budgets");
    }

    private async void OnStatsTapped(object? sender, TappedEventArgs e)
    {
        await NavigateToAsync("stats");
    }

    private async Task NavigateToAsync(string route)
    {
        if (route == ActiveTab)
            return;

        try
        {
            await Shell.Current.GoToAsync($"//{route}");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync(
                "Navigation error",
                ex.Message,
                "OK");
        }
    }
}
