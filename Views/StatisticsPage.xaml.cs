using System.Collections.ObjectModel;
using System.Globalization;
using BudgetWise.Graphics;
using BudgetWise.Models;
using BudgetWise.Services;

namespace BudgetWise.Views;

public partial class StatisticsPage : ContentPage
{
    private static readonly Color[] ChartColors = Palette.Chart;

    private readonly DatabaseService _database;
    private readonly CultureInfo _culture = CultureInfo.GetCultureInfo("ro-RO");
    private readonly CategoryDonutDrawable _donutDrawable = new();
    private readonly MonthlyBarsDrawable _barsDrawable = new();
    private readonly SpendingHeatmapDrawable _heatmapDrawable = new();
    private List<Transaction> _monthTransactions = new();
    private Dictionary<int, Category> _categoriesById = new();
    private bool _loading;
    private bool _refreshPending;

    public ObservableCollection<CategoryLegendItem> CategoryLegend { get; } = new();
    public ObservableCollection<DayTransactionItem> DayTransactions { get; } = new();

    public StatisticsPage(DatabaseService database)
    {
        InitializeComponent();

        _database = database;
        BindingContext = this;
        DonutChart.Drawable = _donutDrawable;
        MonthlyBarsChart.Drawable = _barsDrawable;
        HeatmapChart.Drawable = _heatmapDrawable;
        LegendEmpty.Color = SpendingHeatmapDrawable.EmptyDayColor;
        LegendLow.Color = SpendingHeatmapDrawable.ColorForIntensity(0.1f);
        LegendMid.Color = SpendingHeatmapDrawable.ColorForIntensity(0.5f);
        LegendHigh.Color = SpendingHeatmapDrawable.ColorForIntensity(1f);

        _loading = true;
        PeriodPicker.Date = DateTime.Today;
        ChartTypePicker.SelectedIndex = 0;
        _loading = false;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadStatisticsAsync();
    }

    private async void OnPeriodChanged(object? sender, DateChangedEventArgs e)
    {
        await LoadStatisticsAsync();
    }

    private async void OnChartTypeChanged(object? sender, EventArgs e)
    {
        await LoadStatisticsAsync();
    }

    private void OnHeatmapTapped(object? sender, TouchEventArgs e)
    {
        if (e.Touches.Length == 0)
            return;

        int day = _heatmapDrawable.HitTest(
            e.Touches[0].X, e.Touches[0].Y,
            (float)HeatmapChart.Width, (float)HeatmapChart.Height);

        if (day == 0)
            return;

        ShowDay(day == _heatmapDrawable.SelectedDay ? 0 : day);
    }

    private void ShowDay(int day)
    {
        _heatmapDrawable.SelectedDay = day;
        DayTransactions.Clear();

        if (day == 0)
        {
            HeatmapDetailLabel.Text = "Tap a day to see its transactions";
            HeatmapChart.Invalidate();
            return;
        }

        var items = _monthTransactions
            .Where(transaction => transaction.Date.Day == day)
            .OrderByDescending(transaction => transaction.Id)
            .ToList();

        DateTime date = new(_heatmapDrawable.Month.Year, _heatmapDrawable.Month.Month, day);
        string dateText = date.ToString("dd MMM", _culture);
        long spent = items
            .Where(transaction => transaction.Type == "Expense")
            .Sum(transaction => transaction.AmountRonMinor);

        HeatmapDetailLabel.Text = items.Count == 0
            ? $"{dateText} · no transactions"
            : $"{dateText} · {items.Count} {(items.Count == 1 ? "transaction" : "transactions")} · spent {FormatMoney(spent)}";

        foreach (var transaction in items)
        {
            bool isIncome = transaction.Type == "Income";
            _categoriesById.TryGetValue(transaction.CategoryId, out Category? category);

            DayTransactions.Add(new DayTransactionItem
            {
                Title = string.IsNullOrWhiteSpace(transaction.Description)
                    ? category?.Name ?? "Other"
                    : transaction.Description,
                AmountText = (isIncome ? "+" : "-") + FormatMoney(transaction.AmountRonMinor),
                AmountColor = isIncome ? Palette.IncomeText : Palette.ExpenseText,
                IconSource = CategoryIcons.For(category?.Icon)
            });
        }

        HeatmapChart.Invalidate();
    }

    private async Task LoadStatisticsAsync()
    {
        if (_loading)
        {
            _refreshPending = true;
            return;
        }

        _loading = true;
        try
        {
            do
            {
                _refreshPending = false;
                await RefreshStatisticsAsync();
            }
            while (_refreshPending);
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task RefreshStatisticsAsync()
    {
        try
        {
            DateTime selectedDate = PeriodPicker.Date ?? DateTime.Today;
            DateTime monthStart = new(selectedDate.Year, selectedDate.Month, 1);
            var transactions = await _database.GetTransactionsAsync();
            var categories = await _database.GetCategoriesAsync();
            var categoryLookup = categories.ToDictionary(category => category.Id, category => category.Name);

            var monthlyTransactions = transactions
                .Where(transaction => transaction.Date >= monthStart && transaction.Date < monthStart.AddMonths(1))
                .ToList();

            _monthTransactions = monthlyTransactions;
            _categoriesById = categories.ToDictionary(category => category.Id);
            if (_heatmapDrawable.Month != monthStart)
                _heatmapDrawable.SelectedDay = 0;
            _heatmapDrawable.Month = monthStart;
            _heatmapDrawable.DailyExpenses = monthlyTransactions
                .Where(transaction => transaction.Type == "Expense")
                .GroupBy(transaction => transaction.Date.Day)
                .ToDictionary(
                    group => group.Key,
                    group => group.Sum(transaction => transaction.AmountRonMinor));
            ShowDay(_heatmapDrawable.SelectedDay);

            long income = monthlyTransactions
                .Where(transaction => transaction.Type == "Income")
                .Sum(transaction => transaction.AmountRonMinor);
            long expenses = monthlyTransactions
                .Where(transaction => transaction.Type == "Expense")
                .Sum(transaction => transaction.AmountRonMinor);
            long net = income - expenses;

            IncomeLabel.Text = FormatMoney(income);
            ExpensesLabel.Text = FormatMoney(expenses);
            NetLabel.Text = FormatMoney(net);
            NetLabel.TextColor = net < 0 ? Palette.ExpenseText : Palette.IncomeText;

            bool showIncome = ChartTypePicker.SelectedIndex == 1;
            string chartType = showIncome ? "Income" : "Expense";
            long chartTotal = showIncome ? income : expenses;
            CategoryTitleLabel.Text = showIncome ? "INCOME BY CATEGORY" : "SPENDING BY CATEGORY";
            CategoryEmptyLabel.Text = showIncome
                ? "NO INCOME FOR THIS MONTH"
                : "NO EXPENSES FOR THIS MONTH";

            var categoryGroups = monthlyTransactions
                .Where(transaction => transaction.Type == chartType)
                .GroupBy(transaction => transaction.CategoryId)
                .Select(group => new
                {
                    CategoryId = group.Key,
                    AmountMinor = group.Sum(transaction => transaction.AmountRonMinor)
                })
                .Where(group => group.AmountMinor > 0)
                .OrderByDescending(group => group.AmountMinor)
                .ToList();

            CategoryLegend.Clear();
            var slices = new List<CategorySlice>(categoryGroups.Count);
            for (int index = 0; index < categoryGroups.Count; index++)
            {
                var group = categoryGroups[index];
                string name = categoryLookup.GetValueOrDefault(group.CategoryId, "Unknown category");
                Color color = ChartColors[index % ChartColors.Length];
                slices.Add(new CategorySlice(name, group.AmountMinor, color));

                decimal percentage = chartTotal > 0
                    ? group.AmountMinor * 100m / chartTotal
                    : 0;

                CategoryLegend.Add(new CategoryLegendItem
                {
                    Name = name,
                    DotColor = color,
                    AmountText = FormatMoney(group.AmountMinor),
                    PercentageText = percentage.ToString("0.#", _culture) + "%"
                });
            }

            _donutDrawable.CenterCaption = showIncome ? "TOTAL INCOME" : "TOTAL EXPENSES";
            _donutDrawable.Slices = slices;
            DonutChart.IsVisible = slices.Count > 0;
            CategoryEmptyLabel.IsVisible = slices.Count == 0;
            DonutChart.Invalidate();

            var monthlyPoints = new List<MonthlyChartPoint>(6);
            for (int monthsAgo = 5; monthsAgo >= 0; monthsAgo--)
            {
                DateTime start = monthStart.AddMonths(-monthsAgo);
                DateTime end = start.AddMonths(1);
                var monthTransactions = transactions
                    .Where(transaction => transaction.Date >= start && transaction.Date < end)
                    .ToList();

                long monthIncome = monthTransactions
                    .Where(transaction => transaction.Type == "Income")
                    .Sum(transaction => transaction.AmountRonMinor);
                long monthExpenses = monthTransactions
                    .Where(transaction => transaction.Type == "Expense")
                    .Sum(transaction => transaction.AmountRonMinor);

                monthlyPoints.Add(new MonthlyChartPoint(
                    start.ToString("MMM", _culture), monthIncome, monthExpenses));
            }

            _barsDrawable.Points = monthlyPoints;
            MonthlyBarsChart.Invalidate();

            long sixMonthIncome = monthlyPoints.Sum(point => point.IncomeMinor);
            long sixMonthExpenses = monthlyPoints.Sum(point => point.ExpenseMinor);
            TrendSummaryLabel.Text = sixMonthIncome == 0 && sixMonthExpenses == 0
                ? "No transactions in this period"
                : $"6-month net: {FormatMoney(sixMonthIncome - sixMonthExpenses)}";
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Statistics error", ex.Message, "OK");
        }
    }

    private string FormatMoney(long amountMinor)
    {
        return (amountMinor / 100m).ToString("N2", _culture) + " RON";
    }
}

public sealed class DayTransactionItem
{
    public string Title { get; set; } = string.Empty;
    public string AmountText { get; set; } = string.Empty;
    public Color AmountColor { get; set; } = Colors.White;
    public string IconSource { get; set; } = CategoryIcons.Fallback;
}

public sealed class CategoryLegendItem
{
    public string Name { get; set; } = string.Empty;
    public string AmountText { get; set; } = string.Empty;
    public string PercentageText { get; set; } = string.Empty;
    public Color DotColor { get; set; } = Colors.Gray;
}
