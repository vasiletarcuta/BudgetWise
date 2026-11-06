using System.Collections.ObjectModel;
using System.Globalization;
using BudgetWise.Services;

namespace BudgetWise.Views;

public partial class TransactionsPage : ContentPage
{
    private readonly DatabaseService _database;
    private readonly List<TransactionListItem> _allItems = new();

    public ObservableCollection<TransactionListItem> Transactions { get; } = new();

    public TransactionsPage(DatabaseService database)
    {
        InitializeComponent();

        _database = database;
        BindingContext = this;

        TypeFilter.Items.Add("All");
        TypeFilter.Items.Add("Expense");
        TypeFilter.Items.Add("Income");
        TypeFilter.SelectedIndex = 0;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadTransactionsAsync();
    }

    private async Task LoadTransactionsAsync()
    {
        try
        {
            var transactions = await _database.GetTransactionsAsync();
            var categories = await _database.GetCategoriesAsync();
            var categoryLookup = categories.ToDictionary(category => category.Id, category => category.Name);
            var iconLookup = categories.ToDictionary(category => category.Id, category => category.Icon);
            var culture = CultureInfo.GetCultureInfo("ro-RO");

            _allItems.Clear();

            foreach (var transaction in transactions)
            {
                bool isIncome = transaction.Type == "Income";
                string categoryName = categoryLookup.GetValueOrDefault(transaction.CategoryId, "Other");
                decimal amount = transaction.AmountMinor / 100m;

                _allItems.Add(new TransactionListItem
                {
                    Id = transaction.Id,
                    Title = string.IsNullOrWhiteSpace(transaction.Description)
                        ? categoryName
                        : transaction.Description,
                    Subtitle = $"{categoryName} · {transaction.Date.ToString("dd MMM yyyy", culture)}",
                    Type = transaction.Type,
                    AmountText = $"{(isIncome ? "+" : "-")}{amount.ToString("N2", culture)} {transaction.Currency}",
                    AmountColor = isIncome ? Palette.IncomeText : Palette.ExpenseText,
                    TypeColor = isIncome ? Palette.Income : Palette.Expense,
                    IconSource = CategoryIcons.For(iconLookup.GetValueOrDefault(transaction.CategoryId))
                });
            }

            ApplyFilters();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Database error", ex.Message, "OK");
        }
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        ApplyFilters();
    }

    private void OnFilterChanged(object? sender, EventArgs e)
    {
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        string search = TransactionSearch?.Text?.Trim() ?? string.Empty;
        string type = TypeFilter?.SelectedItem?.ToString() ?? "All";
        var filtered = _allItems.Where(item =>
            (type == "All" || item.Type == type) &&
            (string.IsNullOrWhiteSpace(search) ||
             item.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
             item.Subtitle.Contains(search, StringComparison.OrdinalIgnoreCase)));

        Transactions.Clear();
        foreach (var item in filtered)
            Transactions.Add(item);

        if (CountLabel is not null)
            CountLabel.Text = $"{Transactions.Count} transactions";
    }

    private async void OnAddTransactionClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(
            $"{nameof(TransactionFormPage)}?type=Expense");
    }

    private async void OnEditClicked(object? sender, EventArgs e)
    {
        if (sender is not Button button || !TryGetTransactionId(button, out int id))
            return;

        await Shell.Current.GoToAsync(
            $"{nameof(TransactionFormPage)}?id={id}");
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        if (sender is not Button button || !TryGetTransactionId(button, out int id))
            return;

        bool confirmed = await DisplayAlertAsync(
            "Delete transaction",
            "Are you sure you want to delete this transaction?",
            "Delete",
            "Cancel");

        if (!confirmed)
            return;

        try
        {
            var transaction = await _database.GetTransactionAsync(id);
            if (transaction is null)
                return;

            await _database.DeleteTransactionAsync(transaction);
            await LoadTransactionsAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Delete error", ex.Message, "OK");
        }
    }

    private static bool TryGetTransactionId(Button button, out int id)
    {
        if (button.CommandParameter is int parameterId)
        {
            id = parameterId;
            return true;
        }

        return int.TryParse(button.CommandParameter?.ToString(), out id);
    }
}

public class TransactionListItem
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string AmountText { get; set; } = string.Empty;
    public Color AmountColor { get; set; } = Palette.Text;
    public Color TypeColor { get; set; } = Palette.Expense;
    public string IconSource { get; set; } = CategoryIcons.Fallback;
}
