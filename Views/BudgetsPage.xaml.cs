using System.Collections.ObjectModel;
using System.Globalization;
using BudgetWise.Models;
using BudgetWise.Services;

namespace BudgetWise.Views;

public partial class BudgetsPage : ContentPage
{
    private readonly DatabaseService _database;
    private readonly CultureInfo _culture = CultureInfo.GetCultureInfo("ro-RO");
    private bool _loading;

    public ObservableCollection<BudgetListItem> Budgets { get; } = new();

    public BudgetsPage(DatabaseService database)
    {
        _database = database;
        InitializeComponent();
        BindingContext = this;
        PeriodPicker.Date = DateTime.Today;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadBudgetsAsync();
    }

    private async Task LoadBudgetsAsync()
    {
        if (_loading)
            return;

        _loading = true;
        try
        {
            DateTime period = PeriodPicker.Date ?? DateTime.Today;
            var budgets = await _database.GetBudgetsAsync(period.Month, period.Year);
            var categories = await _database.GetCategoriesAsync();
            var transactions = await _database.GetTransactionsAsync();
            var categoryNames = categories.ToDictionary(category => category.Id, category => category.Name);
            var categoryIcons = categories.ToDictionary(category => category.Id, category => category.Icon);
            var monthlyExpenses = transactions
                .Where(transaction =>
                    transaction.Type == "Expense" &&
                    transaction.Date.Month == period.Month &&
                    transaction.Date.Year == period.Year)
                .ToList();

            Budgets.Clear();
            long totalLimit = 0;
            long totalSpent = 0;

            foreach (var budget in budgets)
            {
                long spent = monthlyExpenses
                    .Where(transaction => transaction.CategoryId == budget.CategoryId)
                    .Sum(transaction => transaction.AmountRonMinor);
                long remaining = budget.LimitMinor - spent;
                decimal usage = budget.LimitMinor > 0 ? (decimal)spent / budget.LimitMinor : 0m;
                bool exceeded = remaining < 0;
                totalLimit += budget.LimitMinor;
                totalSpent += spent;

                Budgets.Add(new BudgetListItem
                {
                    Id = budget.Id,
                    CategoryName = categoryNames.GetValueOrDefault(budget.CategoryId, "Other"),
                    IconSource = CategoryIcons.For(categoryIcons.GetValueOrDefault(budget.CategoryId)),
                    SpentText = $"{FormatMoney(spent)} spent / {FormatMoney(budget.LimitMinor)} limit",
                    RemainingText = exceeded
                        ? $"{FormatMoney(-remaining)} over budget"
                        : $"{FormatMoney(remaining)} available to spend",
                    PercentageText = usage.ToString("P0", _culture),
                    Progress = Math.Clamp((double)usage, 0.0, 1.0),
                    StatusText = exceeded ? "BUDGET EXCEEDED" : "ON TRACK",
                    StatusColor = exceeded ? Palette.ExpenseText : Palette.PrimaryText
                });
            }

            TotalBudgetLabel.Text = FormatMoney(totalLimit);
            TotalSpentLabel.Text = FormatMoney(totalSpent);
            BudgetCountLabel.Text = $"{Budgets.Count} BUDGETS";

            if (totalLimit > 0)
            {
                OverallProgress.Progress = Math.Clamp((double)totalSpent / totalLimit, 0.0, 1.0);
                long remaining = totalLimit - totalSpent;
                RemainingLabel.Text = remaining >= 0
                    ? $"{FormatMoney(remaining)} available to spend"
                    : $"{FormatMoney(-remaining)} over budget";
                OverallProgress.ProgressColor = remaining < 0 ? Palette.ExpenseText : Palette.PrimaryText;
            }
            else
            {
                OverallProgress.Progress = 0;
                RemainingLabel.Text = "No budgets configured for this month";
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Budget error", ex.Message, "OK");
        }
        finally
        {
            _loading = false;
        }
    }

    private string FormatMoney(long minor)
    {
        return (minor / 100m).ToString("N2", _culture) + " RON";
    }

    private async void OnPeriodChanged(object? sender, DateChangedEventArgs e)
    {
        await LoadBudgetsAsync();
    }

    private string SelectedPeriodQuery()
    {
        DateTime date = PeriodPicker.Date ?? DateTime.Today;
        return date.ToString("yyyy-MM", CultureInfo.InvariantCulture);
    }

    private async void OnCreateBudgetClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(
            $"{nameof(BudgetFormPage)}?period={SelectedPeriodQuery()}");
    }

    private async void OnEditBudgetClicked(object? sender, EventArgs e)
    {
        if (sender is not Button button || !TryGetBudgetId(button, out int id))
            return;

        await Shell.Current.GoToAsync($"{nameof(BudgetFormPage)}?id={id}");
    }

    private async void OnDeleteBudgetClicked(object? sender, EventArgs e)
    {
        if (sender is not Button button || !TryGetBudgetId(button, out int id))
            return;

        bool confirmed = await DisplayAlertAsync(
            "Delete budget",
            "Are you sure you want to delete this budget?",
            "Delete",
            "Cancel");
        if (!confirmed)
            return;

        try
        {
            DateTime period = PeriodPicker.Date ?? DateTime.Today;
            var budgets = await _database.GetBudgetsAsync(period.Month, period.Year);
            var budget = budgets.FirstOrDefault(item => item.Id == id);
            if (budget is null)
                return;

            await _database.DeleteBudgetAsync(budget);
            await LoadBudgetsAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Delete error", ex.Message, "OK");
        }
    }

    private static bool TryGetBudgetId(Button button, out int id)
    {
        if (button.CommandParameter is int parameterId)
        {
            id = parameterId;
            return true;
        }

        return int.TryParse(button.CommandParameter?.ToString(), out id);
    }
}

public class BudgetListItem
{
    public int Id { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string IconSource { get; set; } = CategoryIcons.Fallback;
    public string SpentText { get; set; } = string.Empty;
    public string RemainingText { get; set; } = string.Empty;
    public string PercentageText { get; set; } = string.Empty;
    public double Progress { get; set; }
    public string StatusText { get; set; } = string.Empty;
    public Color StatusColor { get; set; } = Palette.PrimaryText;
}
