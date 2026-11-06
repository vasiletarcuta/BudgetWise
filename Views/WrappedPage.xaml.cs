using System.Globalization;
using BudgetWise.Models;
using BudgetWise.Services;

namespace BudgetWise.Views;

public partial class WrappedPage : ContentPage
{
    private const int MaxCards = 6;

    private static readonly CultureInfo English = CultureInfo.InvariantCulture;
    private static Color SegmentOff => Palette.WrappedText.WithAlpha(0.25f);

    private readonly DatabaseService _database;
    private readonly CultureInfo _culture = CultureInfo.GetCultureInfo("ro-RO");
    private readonly List<BoxView> _segments = new();
    private readonly List<DateTime> _months = new();

    private List<Transaction> _transactions = new();
    private Dictionary<int, Category> _categories = new();
    private List<WrappedCard> _cards = new();
    private int _index;
    private int _version;
    private bool _loaded;
    private Color _currentBackground = Palette.WrappedIntroBg;

    private sealed record WrappedCard(
        string Kicker,
        string Big,
        string Caption,
        string Detail,
        string Icon,
        Color Background,
        Color Accent,
        double? Value = null,
        Func<double, string>? Format = null,
        double? Progress = null);

    public WrappedPage(DatabaseService database)
    {
        InitializeComponent();
        _database = database;

        for (int index = 0; index < MaxCards; index++)
        {
            Segments.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var segment = new BoxView { HeightRequest = 5, Color = SegmentOff };
            Grid.SetColumn(segment, index);
            Segments.Children.Add(segment);
            _segments.Add(segment);
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        SetDarkSystemBarIcons(false);
        if (_loaded)
            return;

        try
        {
            _transactions = await _database.GetTransactionsAsync();
            var categories = await _database.GetCategoriesAsync();
            _categories = categories.ToDictionary(category => category.Id);

            _months.Clear();
            _months.AddRange(_transactions
                .Select(transaction => new DateTime(transaction.Date.Year, transaction.Date.Month, 1))
                .Distinct()
                .OrderByDescending(month => month));

            MonthPicker.ItemsSource = _months
                .Select(month => month.ToString("MMMM yyyy", English))
                .ToList();

            // Wrapped is a look back, so default to the last full month when there is one.
            DateTime thisMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);
            int start = _months.FindIndex(month => month < thisMonth);
            _loaded = true;
            MonthPicker.SelectedIndex = _months.Count == 0 ? -1 : Math.Max(start, 0);

            if (_months.Count == 0)
            {
                _cards = new List<WrappedCard> { EmptyCard() };
                await ShowCardAsync(0);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Wrapped error", ex.Message, "OK");
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        SetDarkSystemBarIcons(true);
    }

    // The rest of the app is light, so the Android status bar uses dark icons; Wrapped is dark, so it needs light ones.
    private static void SetDarkSystemBarIcons(bool dark)
    {
#if ANDROID
        if (Microsoft.Maui.ApplicationModel.Platform.CurrentActivity?.Window is { } window &&
            AndroidX.Core.View.WindowCompat.GetInsetsController(window, window.DecorView) is { } controller)
        {
            controller.AppearanceLightStatusBars = dark;
            controller.AppearanceLightNavigationBars = dark;
        }
#endif
    }

    private async void OnMonthChanged(object? sender, EventArgs e)
    {
        if (!_loaded || MonthPicker.SelectedIndex < 0 || MonthPicker.SelectedIndex >= _months.Count)
            return;

        _cards = BuildCards(_months[MonthPicker.SelectedIndex]);
        await ShowCardAsync(0);
    }

    private async void OnNextTapped(object? sender, TappedEventArgs e)
    {
        if (_index < _cards.Count - 1)
            await ShowCardAsync(_index + 1);
        else
            await Shell.Current.GoToAsync("..");
    }

    private async void OnPreviousTapped(object? sender, TappedEventArgs e)
    {
        if (_index > 0)
            await ShowCardAsync(_index - 1);
    }

    private async void OnCloseTapped(object? sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private List<WrappedCard> BuildCards(DateTime month)
    {
        DateTime next = month.AddMonths(1);
        var monthTransactions = _transactions
            .Where(transaction => transaction.Date >= month && transaction.Date < next)
            .ToList();

        if (monthTransactions.Count == 0)
            return new List<WrappedCard> { EmptyCard() };

        var expenses = monthTransactions.Where(transaction => transaction.Type == "Expense").ToList();
        long income = monthTransactions
            .Where(transaction => transaction.Type == "Income")
            .Sum(transaction => transaction.AmountRonMinor);
        long spent = expenses.Sum(transaction => transaction.AmountRonMinor);

        string monthName = month.ToString("MMMM", English).ToUpperInvariant();
        var cards = new List<WrappedCard>
        {
            new("BUDGETWISE WRAPPED", monthName, "Your money, month in review",
                "Tap to continue  →", "bw_logo.png",
                Palette.WrappedIntroBg, Palette.WrappedIntroAccent)
        };

        // Top spending category.
        var topCategory = expenses
            .GroupBy(transaction => transaction.CategoryId)
            .Select(group => (CategoryId: group.Key, Total: group.Sum(transaction => transaction.AmountRonMinor)))
            .OrderByDescending(item => item.Total)
            .Cast<(int CategoryId, long Total)?>()
            .FirstOrDefault();

        if (topCategory is { } top)
        {
            _categories.TryGetValue(top.CategoryId, out Category? category);
            decimal share = spent > 0 ? top.Total * 100m / spent : 0;
            cards.Add(new(
                $"YOU SPENT THE MOST ON {(category?.Name ?? "OTHER").ToUpperInvariant()}",
                Money(top.Total), $"{share:0}% of everything you spent",
                $"{expenses.Count(transaction => transaction.CategoryId == top.CategoryId)} purchases in this category",
                CategoryIcons.For(category?.Icon),
                Palette.WrappedTopBg, Palette.WrappedTopAccent,
                top.Total / 100.0, MoneyFromRon));
        }
        else
        {
            cards.Add(new("YOUR TOP CATEGORY", "—", "No expenses this month", "Nothing to rank yet",
                "cat_other.png", Palette.WrappedTopBg, Palette.WrappedTopAccent));
        }

        // Priciest day.
        var priciestDay = expenses
            .GroupBy(transaction => transaction.Date.Date)
            .Select(group => (Day: group.Key, Total: group.Sum(transaction => transaction.AmountRonMinor), Items: group.ToList()))
            .OrderByDescending(item => item.Total)
            .Cast<(DateTime Day, long Total, List<Transaction> Items)?>()
            .FirstOrDefault();

        if (priciestDay is { } day)
        {
            var biggest = day.Items.OrderByDescending(transaction => transaction.AmountRonMinor).First();
            string what = string.IsNullOrWhiteSpace(biggest.Description)
                ? _categories.GetValueOrDefault(biggest.CategoryId)?.Name ?? "a purchase"
                : biggest.Description;
            cards.Add(new(
                $"YOUR PRICIEST DAY WAS {day.Day.ToString("d MMMM", English).ToUpperInvariant()}",
                Money(day.Total), $"Biggest purchase: {what} ({Money(biggest.AmountRonMinor)})",
                $"{day.Items.Count} {(day.Items.Count == 1 ? "purchase" : "purchases")} that day",
                CategoryIcons.For(_categories.GetValueOrDefault(biggest.CategoryId)?.Icon),
                Palette.WrappedDayBg, Palette.WrappedDayAccent,
                day.Total / 100.0, MoneyFromRon));
        }
        else
        {
            cards.Add(new("YOUR PRICIEST DAY", "—", "No expenses this month", "A very quiet month",
                "cat_shopping.png", Palette.WrappedDayBg, Palette.WrappedDayAccent));
        }

        // Savings rate.
        if (income > 0)
        {
            double rate = (income - spent) * 100.0 / income;
            string verdict = rate >= 30 ? "Outstanding. Keep it up!"
                : rate >= 10 ? "Solid — you kept more than you spent."
                : rate >= 0 ? "Close to break-even. A small cut goes a long way."
                : "You spent more than you earned this month.";
            cards.Add(new(
                "YOU SAVED", $"{rate:0}%", $"{Money(income)} in · {Money(spent)} out", verdict,
                "cat_salary.png",
                Palette.WrappedSaveBg, Palette.WrappedSaveAccent,
                rate, value => $"{value:0}%", Math.Clamp(rate / 100.0, 0, 1)));
        }
        else
        {
            cards.Add(new("YOU SAVED", "—", "No income recorded this month", "Add an income to see your savings rate",
                "cat_salary.png", Palette.WrappedSaveBg, Palette.WrappedSaveAccent));
        }

        // Versus the previous month.
        DateTime previous = month.AddMonths(-1);
        var previousExpenses = _transactions
            .Where(transaction => transaction.Type == "Expense" && transaction.Date >= previous && transaction.Date < month)
            .Sum(transaction => transaction.AmountRonMinor);
        string previousName = previous.ToString("MMMM", English);

        if (previousExpenses > 0)
        {
            double change = (spent - previousExpenses) * 100.0 / previousExpenses;
            long difference = Math.Abs(spent - previousExpenses);
            cards.Add(new(
                $"COMPARED TO {previousName.ToUpperInvariant()}",
                $"{(change > 0 ? "+" : string.Empty)}{change:0}%",
                spent <= previousExpenses
                    ? $"You spent {Money(difference)} less than in {previousName}"
                    : $"You spent {Money(difference)} more than in {previousName}",
                spent <= previousExpenses ? "Nice — spending went down." : "Spending went up this month.",
                spent <= previousExpenses ? "trend_down.png" : "cat_income.png",
                Palette.WrappedCompareBg, Palette.WrappedCompareAccent,
                change, value => $"{(value > 0 ? "+" : string.Empty)}{value:0}%"));
        }
        else
        {
            cards.Add(new($"COMPARED TO {previousName.ToUpperInvariant()}", "—",
                $"No expenses recorded in {previousName}", "Nothing to compare with yet",
                "cat_income.png", Palette.WrappedCompareBg, Palette.WrappedCompareAccent));
        }

        // Summary.
        DateTime today = DateTime.Today;
        int days = today.Year == month.Year && today.Month == month.Month
            ? today.Day
            : DateTime.DaysInMonth(month.Year, month.Month);
        string weekday = expenses.Count == 0
            ? "—"
            : expenses
                .GroupBy(transaction => transaction.Date.DayOfWeek)
                .OrderByDescending(group => group.Sum(transaction => transaction.AmountRonMinor))
                .First().Key.ToString();

        cards.Add(new(
            "THE WHOLE MONTH", $"{monthTransactions.Count}", $"transactions · about {Money(spent / days)} spent per day",
            $"Your costliest weekday: {weekday}.  Tap to finish", "bw_logo.png",
            Palette.WrappedIntroBg, Palette.WrappedIntroAccent,
            monthTransactions.Count, value => $"{value:0}"));

        return cards;
    }

    private static WrappedCard EmptyCard() => new(
        "BUDGETWISE WRAPPED", "NO DATA YET", "Add some transactions first",
        "Or load the demo data on the Home page", "bw_logo.png",
        Palette.WrappedIntroBg, Palette.WrappedIntroAccent);

    private async Task ShowCardAsync(int index)
    {
        int version = ++_version;
        _index = index;
        WrappedCard card = _cards[index];

        this.AbortAnimation("count");
        this.AbortAnimation("background");
        Body.CancelAnimations();
        BigLabel.CancelAnimations();
        CardProgress.CancelAnimations();

        KickerLabel.Text = card.Kicker;
        KickerLabel.TextColor = card.Accent;
        BigLabel.TextColor = card.Accent;
        BigLabel.Text = card.Value is null || card.Format is null ? card.Big : card.Format(0);
        CaptionLabel.Text = card.Caption;
        DetailLabel.Text = card.Detail;

        IconImage.Source = card.Icon;
        bool isLogo = card.Icon == "bw_logo.png";
        IconBadge.BackgroundColor = isLogo ? Colors.Transparent : card.Accent;
        IconBadge.StrokeThickness = isLogo ? 0 : 3;
        IconBadge.Padding = isLogo ? 0 : 24;

        CardProgress.IsVisible = card.Progress.HasValue;
        CardProgress.ProgressColor = card.Accent;
        CardProgress.Progress = 0;

        for (int segment = 0; segment < _segments.Count; segment++)
        {
            _segments[segment].IsVisible = segment < _cards.Count;
            _segments[segment].Color = segment <= index ? card.Accent : SegmentOff;
        }

        Color from = _currentBackground;
        _currentBackground = card.Background;
        new Animation(t => Root.BackgroundColor = Blend(from, card.Background, t))
            .Commit(this, "background", 16, 450, Easing.CubicOut);

        Body.Opacity = 0;
        Body.TranslationY = 36;
        BigLabel.Scale = 0.7;

        if (card.Value is { } target && card.Format is { } format)
        {
            new Animation(value => BigLabel.Text = format(value), 0, target)
                .Commit(this, "count", 16, 900, Easing.CubicOut);
        }

        await Task.WhenAll(
            Body.FadeToAsync(1, 320),
            Body.TranslateToAsync(0, 0, 380, Easing.CubicOut),
            BigLabel.ScaleToAsync(1, 520, Easing.SpringOut));

        if (version != _version)
            return;

        if (card.Progress is { } progress)
            await CardProgress.ProgressTo(progress, 700, Easing.CubicOut);
    }

    private static Color Blend(Color from, Color to, double t)
    {
        return new Color(
            (float)(from.Red + (to.Red - from.Red) * t),
            (float)(from.Green + (to.Green - from.Green) * t),
            (float)(from.Blue + (to.Blue - from.Blue) * t));
    }

    private string Money(long minor) => (minor / 100m).ToString("N0", _culture) + " RON";

    private string MoneyFromRon(double ron) => ((decimal)ron).ToString("N0", _culture) + " RON";
}
