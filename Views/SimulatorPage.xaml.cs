using System.Globalization;
using BudgetWise.Graphics;
using BudgetWise.Models;
using BudgetWise.Services;

namespace BudgetWise.Views;

public partial class SimulatorPage : ContentPage
{
    private const int HistoryMonths = 3;

    private readonly DatabaseService _database;
    private readonly CultureInfo _culture = CultureInfo.GetCultureInfo("ro-RO");
    private readonly ProjectionChartDrawable _chart = new();
    private readonly List<(Category Category, decimal MonthlyAverage)> _pickerItems = new();

    private decimal _averageIncome;
    private decimal _averageExpenses;
    private decimal _currentBalance;
    private bool _ready;

    public SimulatorPage(DatabaseService database)
    {
        InitializeComponent();

        _database = database;
        ProjectionChart.Drawable = _chart;
        LegendBaseline.Color = ProjectionChartDrawable.Baseline;
        LegendScenario.Color = ProjectionChartDrawable.Scenario;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var transactions = await _database.GetTransactionsAsync();
            var categories = await _database.GetCategoriesAsync();

            DateTime thisMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);
            var window = transactions
                .Where(transaction => transaction.Date >= thisMonth.AddMonths(-HistoryMonths) && transaction.Date < thisMonth)
                .ToList();

            string basis = $"Based on the last {HistoryMonths} full months";
            if (window.Count == 0)
            {
                window = transactions;
                basis = "Based on all your transactions";
            }

            EmptyLabel.IsVisible = window.Count == 0;
            ScenarioContent.IsVisible = window.Count > 0;
            if (window.Count == 0)
                return;

            int monthCount = Math.Max(1, window
                .Select(transaction => (transaction.Date.Year, transaction.Date.Month))
                .Distinct()
                .Count());

            _averageIncome = window
                .Where(transaction => transaction.Type == "Income")
                .Sum(transaction => transaction.AmountRonMinor) / 100m / monthCount;
            _averageExpenses = window
                .Where(transaction => transaction.Type == "Expense")
                .Sum(transaction => transaction.AmountRonMinor) / 100m / monthCount;
            _currentBalance =
                (transactions.Where(transaction => transaction.Type == "Income").Sum(transaction => transaction.AmountRonMinor) -
                 transactions.Where(transaction => transaction.Type == "Expense").Sum(transaction => transaction.AmountRonMinor)) / 100m;

            _pickerItems.Clear();
            foreach (var category in categories.Where(category => category.Type == "Expense"))
            {
                decimal average = window
                    .Where(transaction => transaction.Type == "Expense" && transaction.CategoryId == category.Id)
                    .Sum(transaction => transaction.AmountRonMinor) / 100m / monthCount;

                if (average > 0)
                    _pickerItems.Add((category, average));
            }

            _pickerItems.Sort((left, right) => right.MonthlyAverage.CompareTo(left.MonthlyAverage));

            _ready = false;
            CategoryPicker.ItemsSource = _pickerItems
                .Select(item => $"{item.Category.Name} · {Money(item.MonthlyAverage)}/mo")
                .ToList();
            CategoryPicker.SelectedIndex = _pickerItems.Count > 0 ? 0 : -1;
            _ready = true;

            BasisLabel.Text = basis;
            AvgIncomeLabel.Text = Money(_averageIncome);
            AvgExpensesLabel.Text = Money(_averageExpenses);
            decimal saved = _averageIncome - _averageExpenses;
            AvgSavedLabel.Text = Money(saved);
            AvgSavedLabel.TextColor = saved < 0 ? Palette.ExpenseText : Palette.PrimaryText;

            Recalculate();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Simulator error", ex.Message, "OK");
        }
    }

    private void Recalculate()
    {
        if (!_ready)
            return;

        decimal cutPercent = Math.Round((decimal)CutSlider.Value);
        decimal extra = Math.Round((decimal)ExtraSlider.Value / 10m) * 10m;
        int months = (int)MonthsStepper.Value;

        decimal categoryAverage = CategoryPicker.SelectedIndex >= 0 && CategoryPicker.SelectedIndex < _pickerItems.Count
            ? _pickerItems[CategoryPicker.SelectedIndex].MonthlyAverage
            : 0m;
        string categoryName = CategoryPicker.SelectedIndex >= 0 && CategoryPicker.SelectedIndex < _pickerItems.Count
            ? _pickerItems[CategoryPicker.SelectedIndex].Category.Name
            : "this category";

        decimal cutSaving = categoryAverage * cutPercent / 100m;
        decimal baselineMonthly = _averageIncome - _averageExpenses;
        decimal scenarioMonthly = baselineMonthly + cutSaving + extra;

        CutLabel.Text = $"REDUCE {categoryName.ToUpperInvariant()} BY {cutPercent:0}%  (−{Money(cutSaving)}/mo)";
        ExtraLabel.Text = $"SAVE AN EXTRA {Money(extra)} PER MONTH";
        MonthsLabel.Text = $"LOOK AHEAD: {months} {(months == 1 ? "MONTH" : "MONTHS")}";
        HorizonLabel.Text = $"Starting from your balance today: {Money(_currentBalance)}";

        var baseline = new List<double>(months + 1);
        var scenario = new List<double>(months + 1);
        for (int month = 0; month <= months; month++)
        {
            baseline.Add((double)(_currentBalance + baselineMonthly * month));
            scenario.Add((double)(_currentBalance + scenarioMonthly * month));
        }

        _chart.BaselineSeries = baseline;
        _chart.ScenarioSeries = scenario;
        ProjectionChart.Invalidate();

        decimal baselineEnd = (decimal)baseline[^1];
        decimal scenarioEnd = (decimal)scenario[^1];
        decimal difference = scenarioEnd - baselineEnd;

        BaselineResultLabel.Text = Money(baselineEnd);
        ScenarioResultLabel.Text = Money(scenarioEnd);
        DifferenceLabel.Text = difference >= 0
            ? $"+{Money(difference)} more after {months} {(months == 1 ? "month" : "months")}"
            : $"{Money(difference)} less after {months} {(months == 1 ? "month" : "months")}";
        DifferenceLabel.TextColor = difference >= 0 ? Palette.IncomeText : Palette.ExpenseText;
        MonthlyLabel.Text = $"Saving {Money(scenarioMonthly)} per month instead of {Money(baselineMonthly)}.";
    }

    private string Money(decimal value)
    {
        return value.ToString("N0", _culture) + " RON";
    }

    private void OnValueChanged(object? sender, ValueChangedEventArgs e)
    {
        Recalculate();
    }

    private void OnCategoryChanged(object? sender, EventArgs e)
    {
        Recalculate();
    }

    private void OnResetClicked(object? sender, EventArgs e)
    {
        _ready = false;
        CutSlider.Value = 20;
        ExtraSlider.Value = 300;
        MonthsStepper.Value = 12;
        CategoryPicker.SelectedIndex = _pickerItems.Count > 0 ? 0 : -1;
        _ready = true;
        Recalculate();
    }

    private async void OnBackClicked(object? sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
