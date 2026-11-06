using System.Globalization;
using BudgetWise.Models;
using BudgetWise.Services;

namespace BudgetWise.Views;

public partial class BudgetFormPage : ContentPage, IQueryAttributable
{
    private readonly DatabaseService _database;
    private Budget? _editingBudget;
    private List<Category> _categories = new();
    private int _editId;
    private DateTime _initialPeriod = DateTime.Today;
    private bool _initialized;
    private bool _saving;

    public BudgetFormPage(DatabaseService database)
    {
        _database = database;
        InitializeComponent();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var idValue) &&
            int.TryParse(idValue?.ToString(), out int id))
        {
            _editId = id;
        }

        if (query.TryGetValue("period", out var periodValue) &&
            DateTime.TryParseExact(
                periodValue?.ToString(),
                "yyyy-MM",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var period))
        {
            _initialPeriod = period;
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_initialized)
            return;

        try
        {
            _categories = (await _database.GetCategoriesAsync())
                .Where(category => category.Type == "Expense")
                .OrderBy(category => category.Name)
                .ToList();

            CategoryPicker.ItemsSource = _categories;
            BudgetDatePicker.Date = _initialPeriod;

            if (_categories.Count > 0)
                CategoryPicker.SelectedIndex = 0;

            if (_editId > 0)
            {
                _editingBudget = await _database.GetBudgetAsync(_editId);
                if (_editingBudget is null)
                {
                    await DisplayAlertAsync("Not found", "This budget no longer exists.", "OK");
                    await Shell.Current.GoToAsync("..");
                    return;
                }

                PageTitleLabel.Text = "EDIT BUDGET";
                SaveBudgetButton.Text = "UPDATE BUDGET";
                BudgetDatePicker.Date = new DateTime(
                    _editingBudget.Year,
                    _editingBudget.Month,
                    1);
                LimitEntry.Text = (_editingBudget.LimitMinor / 100m)
                    .ToString("0.00", CultureInfo.InvariantCulture);
                CategoryPicker.SelectedItem = _categories
                    .FirstOrDefault(category => category.Id == _editingBudget.CategoryId);
            }

            _initialized = true;
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Loading error", ex.Message, "OK");
        }
    }

    private async void OnSaveBudgetClicked(object? sender, EventArgs e)
    {
        if (_saving)
            return;

        if (CategoryPicker.SelectedItem is not Category category)
        {
            await DisplayAlertAsync("Missing category", "Select an expense category.", "OK");
            return;
        }

        string rawAmount = (LimitEntry.Text ?? string.Empty).Trim().Replace(',', '.');
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
                "Invalid limit",
                "Enter a positive limit with up to 2 decimal places.",
                "OK");
            return;
        }

        DateTime date = BudgetDatePicker.Date ?? DateTime.Today;
        _saving = true;
        SaveBudgetButton.IsEnabled = false;

        try
        {
            var existing = await _database.GetBudgetsAsync(date.Month, date.Year);
            bool duplicate = existing.Any(budget =>
                budget.CategoryId == category.Id &&
                budget.Id != (_editingBudget?.Id ?? 0));

            if (duplicate)
            {
                await DisplayAlertAsync(
                    "Budget already exists",
                    "This category already has a budget for the selected month.",
                    "OK");
                return;
            }

            var budget = _editingBudget ?? new Budget();
            budget.CategoryId = category.Id;
            budget.LimitMinor = (long)(amount * 100m);
            budget.Month = date.Month;
            budget.Year = date.Year;

            await _database.SaveBudgetAsync(budget);
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Save error", ex.Message, "OK");
        }
        finally
        {
            _saving = false;
            SaveBudgetButton.IsEnabled = true;
        }
    }

    private async void OnCancelClicked(object? sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
