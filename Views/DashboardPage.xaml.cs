using System.Collections.ObjectModel;
using System.Globalization;
using BudgetWise.Services;

namespace BudgetWise.Views;

public partial class DashboardPage : ContentPage
{
    private readonly DatabaseService _database;
    private readonly CultureInfo _culture = CultureInfo.GetCultureInfo("ro-RO");
    private bool _loading;
    private bool _hideAmounts;

    public ObservableCollection<DashboardRecentItem> RecentTransactions { get; } = new();

    public DashboardPage(DatabaseService database)
    {
        InitializeComponent();

        _database = database;
        BindingContext = this;

        _hideAmounts = Preferences.Default.Get("hide_amounts", false);
        HideAmountsSwitch.IsToggled = _hideAmounts;
    }

    private async void OnHideAmountsToggled(object? sender, ToggledEventArgs e)
    {
        if (_hideAmounts == e.Value)
            return;

        _hideAmounts = e.Value;
        Preferences.Default.Set("hide_amounts", _hideAmounts);
        await LoadDashboardAsync();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadDashboardAsync();
    }

    private async Task LoadDashboardAsync()
    {
        if (_loading)
            return;

        _loading = true;
        try
        {
            var transactions = await _database.GetTransactionsAsync();
            var categories = await _database.GetCategoriesAsync();
            DateTime now = DateTime.Today;

            long totalIncome = transactions
                .Where(transaction => transaction.Type == "Income")
                .Sum(transaction => transaction.AmountRonMinor);
            long totalExpenses = transactions
                .Where(transaction => transaction.Type == "Expense")
                .Sum(transaction => transaction.AmountRonMinor);
            long balance = totalIncome - totalExpenses;

            BalanceLabel.Text = Visible(FormatMoney(balance));
            BalanceLabel.TextColor = balance < 0 ? Palette.ExpenseText : Palette.Text;

            var monthlyTransactions = transactions
                .Where(transaction =>
                    transaction.Date.Year == now.Year &&
                    transaction.Date.Month == now.Month)
                .ToList();

            long monthlyIncome = monthlyTransactions
                .Where(transaction => transaction.Type == "Income")
                .Sum(transaction => transaction.AmountRonMinor);
            long monthlyExpenses = monthlyTransactions
                .Where(transaction => transaction.Type == "Expense")
                .Sum(transaction => transaction.AmountRonMinor);

            IncomeLabel.Text = Visible("+" + FormatMoney(monthlyIncome));
            ExpensesLabel.Text = Visible("-" + FormatMoney(monthlyExpenses));

            if (monthlyIncome > 0)
            {
                decimal savingsRate = (monthlyIncome - monthlyExpenses) * 100m / monthlyIncome;
                SavingsRateLabel.Text = savingsRate.ToString("0.#", _culture) + "%";
                SavingsRateLabel.TextColor = savingsRate >= 0 ? Palette.IncomeText : Palette.ExpenseText;
            }
            else
            {
                SavingsRateLabel.Text = "—";
                SavingsRateLabel.TextColor = Palette.IncomeText;
            }

            var budgets = await _database.GetBudgetsAsync(now.Month, now.Year);
            long totalBudget = budgets.Sum(budget => budget.LimitMinor);
            if (totalBudget > 0)
            {
                var budgetCategoryIds = budgets.Select(budget => budget.CategoryId).ToHashSet();
                long budgetSpent = monthlyTransactions
                    .Where(transaction =>
                        transaction.Type == "Expense" &&
                        budgetCategoryIds.Contains(transaction.CategoryId))
                    .Sum(transaction => transaction.AmountRonMinor);

                BudgetAmountLabel.Text = $"{FormatMoney(budgetSpent)} / {FormatMoney(totalBudget)}";
                decimal usage = (decimal)budgetSpent / totalBudget;
                BudgetPercentLabel.Text = usage.ToString("P0", _culture);
                BudgetProgress.Progress = Math.Clamp((double)usage, 0.0, 1.0);

                long remaining = totalBudget - budgetSpent;
                BudgetRemainingLabel.Text = remaining >= 0
                    ? $"{FormatMoney(remaining)} remaining"
                    : $"{FormatMoney(-remaining)} over budget";
            }
            else
            {
                BudgetAmountLabel.Text = "No budget configured";
                BudgetPercentLabel.Text = "—";
                BudgetProgress.Progress = 0;
                BudgetRemainingLabel.Text = "Create a budget to track your spending";
            }

            var categoryLookup = categories.ToDictionary(category => category.Id, category => category.Name);
            var iconLookup = categories.ToDictionary(category => category.Id, category => category.Icon);
            RecentTransactions.Clear();

            foreach (var transaction in transactions
                .OrderByDescending(item => item.Date)
                .ThenByDescending(item => item.Id)
                .Take(5))
            {
                bool isIncome = transaction.Type == "Income";
                string categoryName = categoryLookup.GetValueOrDefault(transaction.CategoryId, "Other");

                RecentTransactions.Add(new DashboardRecentItem
                {
                    Title = string.IsNullOrWhiteSpace(transaction.Description)
                        ? categoryName
                        : transaction.Description,
                    Subtitle = $"{categoryName} · {transaction.Date.ToString("dd MMM yyyy", _culture)}",
                    AmountText = (isIncome ? "+" : "-") + FormatMoney(transaction.AmountRonMinor),
                    AmountColor = isIncome ? Palette.IncomeText : Palette.ExpenseText,
                    TypeColor = isIncome ? Palette.Income : Palette.Expense,
                    IconSource = CategoryIcons.For(iconLookup.GetValueOrDefault(transaction.CategoryId))
                });
            }

            NoTransactionsLabel.IsVisible = RecentTransactions.Count == 0;
            DemoDataButton.IsVisible = RecentTransactions.Count == 0;
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Dashboard error", ex.Message, "OK");
        }
        finally
        {
            _loading = false;
        }
    }

    private string Visible(string text) => _hideAmounts ? "•••••" : text;

    private string FormatMoney(long amountMinor)
    {
        return (amountMinor / 100m).ToString("N2", _culture) + " RON";
    }

    private async void OnLoadDemoDataClicked(object? sender, EventArgs e)
    {
        try
        {
            await _database.SeedDemoDataAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Demo data error", ex.Message, "OK");
            return;
        }

        await LoadDashboardAsync();
    }

    private async void OnSeeAllTapped(object? sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("//activity");
    }

    private async void OnAddExpenseClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(
            $"{nameof(TransactionFormPage)}?type=Expense");
    }

    private async void OnAddIncomeClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(
            $"{nameof(TransactionFormPage)}?type=Income");
    }

    private async void OnOpenConverterTapped(object? sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(CurrencyConverterPage));
    }

    private async void OnOpenWrappedTapped(object? sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(WrappedPage));
    }

    private async void OnOpenSimulatorTapped(object? sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(SimulatorPage));
    }
}

public class DashboardRecentItem
{
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string AmountText { get; set; } = string.Empty;
    public Color AmountColor { get; set; } = Palette.Text;
    public Color TypeColor { get; set; } = Palette.Expense;
    public string IconSource { get; set; } = CategoryIcons.Fallback;
}
