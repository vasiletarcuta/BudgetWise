using System.Globalization;
using BudgetWise.Models;
using BudgetWise.Services;

namespace BudgetWise.Views;

public partial class TransactionFormPage : ContentPage, IQueryAttributable
{
    private readonly DatabaseService _database;
    private List<Category> _categories = new();
    private BudgetWise.Models.Transaction? _editingTransaction;
    private string _initialType = "Expense";
    private int _editId;
    private bool _initialized;
    private bool _saving;

    public TransactionFormPage(DatabaseService database)
    {
        InitializeComponent();

        _database = database;
        TypePicker.Items.Add("Expense");
        TypePicker.Items.Add("Income");
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("type", out var type))
            _initialType = type?.ToString() == "Income" ? "Income" : "Expense";

        if (query.TryGetValue("id", out var idValue) &&
            int.TryParse(idValue?.ToString(), out int id))
        {
            _editId = id;
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_initialized)
            return;

        try
        {
            _categories = await _database.GetCategoriesAsync();

            if (_editId > 0)
            {
                _editingTransaction = await _database.GetTransactionAsync(_editId);
                if (_editingTransaction is null)
                {
                    await DisplayAlertAsync("Not found", "This transaction no longer exists.", "OK");
                    await Shell.Current.GoToAsync("..");
                    return;
                }

                _initialType = _editingTransaction.Type;
                AmountEntry.Text = (_editingTransaction.AmountMinor / 100m)
                    .ToString("0.00", CultureInfo.InvariantCulture);
                TransactionDatePicker.Date = _editingTransaction.Date;
                DescriptionEditor.Text = _editingTransaction.Description;
                SaveButton.Text = "Update transaction";
            }

            TypePicker.SelectedIndex = _initialType == "Income" ? 1 : 0;
            RefreshCategories();

            if (_editingTransaction is not null && CategoryPicker.ItemsSource is IEnumerable<Category> categories)
            {
                CategoryPicker.SelectedItem = categories
                    .FirstOrDefault(category => category.Id == _editingTransaction.CategoryId);
            }

            _initialized = true;
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Loading error", ex.Message, "OK");
        }
    }

    private void OnTypeChanged(object? sender, EventArgs e)
    {
        RefreshCategories();
    }

    private void RefreshCategories()
    {
        if (TypePicker.SelectedIndex < 0)
            return;

        string type = TypePicker.SelectedItem?.ToString() ?? "Expense";
        var filtered = _categories
            .Where(category => category.Type == type)
            .OrderBy(category => category.Name)
            .ToList();

        CategoryPicker.ItemsSource = filtered;
        CategoryPicker.SelectedIndex = filtered.Count > 0 ? 0 : -1;
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (_saving)
            return;

        string rawAmount = (AmountEntry.Text ?? string.Empty).Trim().Replace(',', '.');
        if (!decimal.TryParse(
                rawAmount,
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out decimal amount)
            || amount <= 0
            || amount > 1_000_000_000m
            || decimal.Round(amount, 2) != amount)
        {
            await DisplayAlertAsync(
                "Invalid amount",
                "Enter a positive amount with up to 2 decimal places.",
                "OK");
            return;
        }

        if (CategoryPicker.SelectedItem is not Category category)
        {
            await DisplayAlertAsync("Missing category", "Please select a category.", "OK");
            return;
        }

        string? type = TypePicker.SelectedItem?.ToString();
        if (type is not ("Income" or "Expense"))
        {
            await DisplayAlertAsync("Invalid type", "Please select a transaction type.", "OK");
            return;
        }

        long amountMinor = (long)(amount * 100m);
        var transaction = _editingTransaction ?? new BudgetWise.Models.Transaction();
        transaction.AmountMinor = amountMinor;
        transaction.AmountRonMinor = amountMinor;
        transaction.Currency = "RON";
        transaction.Type = type;
        transaction.CategoryId = category.Id;
        transaction.Date = TransactionDatePicker.Date ?? DateTime.Today;
        transaction.Description = DescriptionEditor.Text?.Trim() ?? string.Empty;

        _saving = true;
        SaveButton.IsEnabled = false;
        try
        {
            await _database.SaveTransactionAsync(transaction);
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Save error", ex.Message, "OK");
        }
        finally
        {
            _saving = false;
            SaveButton.IsEnabled = true;
        }
    }

    private async void OnCancelClicked(object? sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
