using BudgetWise.Models;
using SQLite;

namespace BudgetWise.Services;

public class DatabaseService
{
    private readonly SQLiteAsyncConnection _db;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    public DatabaseService()
    {
        var dbPath = Path.Combine(
            FileSystem.AppDataDirectory,
            "budgetwise.db3");

        _db = new SQLiteAsyncConnection(
            dbPath,
            SQLiteOpenFlags.ReadWrite |
            SQLiteOpenFlags.Create |
            SQLiteOpenFlags.SharedCache);
    }

    public async Task InitializeAsync()
    {
        if (_initialized)
            return;

        await _initLock.WaitAsync();

        try
        {
            if (_initialized)
                return;

            await _db.CreateTableAsync<Category>();
            await _db.CreateTableAsync<Transaction>();
            await _db.CreateTableAsync<Budget>();

            if (await _db.Table<Category>().CountAsync() == 0)
            {
                var categories = new List<Category>
                {
                    new() { Name = "Food", Type = "Expense", Icon = "food", Color = "#FFB86C" },
                    new() { Name = "Transport", Type = "Expense", Icon = "car", Color = "#A78BFA" },
                    new() { Name = "Shopping", Type = "Expense", Icon = "shopping", Color = "#60A5FA" },
                    new() { Name = "Bills", Type = "Expense", Icon = "bills", Color = "#F472B6" },
                    new() { Name = "Entertainment", Type = "Expense", Icon = "movie", Color = "#FBBF24" },
                    new() { Name = "Other", Type = "Expense", Icon = "other", Color = "#94A3B8" },
                    new() { Name = "Salary", Type = "Income", Icon = "salary", Color = "#7CF7E8" },
                    new() { Name = "Freelance", Type = "Income", Icon = "work", Color = "#34D399" },
                    new() { Name = "Other income", Type = "Income", Icon = "income", Color = "#22D3EE" }
                };

                await _db.InsertAllAsync(categories);
            }

            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    // Fills the database with six months of sample data so every screen has something to show.
    public async Task SeedDemoDataAsync()
    {
        await InitializeAsync();

        var categories = await _db.Table<Category>().ToListAsync();
        int IdOf(string name) => categories.First(category => category.Name == name).Id;

        var random = new Random(42);
        DateTime today = DateTime.Today;
        DateTime thisMonth = new(today.Year, today.Month, 1);
        var transactions = new List<Transaction>();

        void Add(DateTime date, string type, string category, int ron, string description)
        {
            long minor = ron * 100L;
            transactions.Add(new Transaction
            {
                Date = date,
                Type = type,
                CategoryId = IdOf(category),
                AmountMinor = minor,
                AmountRonMinor = minor,
                Currency = "RON",
                Description = description
            });
        }

        string Pick(params string[] options) => options[random.Next(options.Length)];

        for (int monthsAgo = 5; monthsAgo >= 0; monthsAgo--)
        {
            DateTime start = thisMonth.AddMonths(-monthsAgo);
            int lastDay = monthsAgo == 0 ? today.Day : DateTime.DaysInMonth(start.Year, start.Month);

            Add(start, "Income", "Salary", 5200, "Monthly salary");
            if (lastDay >= 15)
                Add(start.AddDays(14), "Income", "Freelance", random.Next(6, 15) * 100, "Freelance project");
            if (lastDay >= 5)
                Add(start.AddDays(4), "Expense", "Bills", random.Next(240, 320), Pick("Rent share", "Electricity", "Internet and phone"));
            if (lastDay >= 12)
                Add(start.AddDays(11), "Expense", "Bills", random.Next(90, 180), Pick("Water and gas", "Insurance"));

            for (int day = 1; day <= lastDay; day++)
            {
                DateTime date = start.AddDays(day - 1);

                if (random.NextDouble() < 0.70)
                    Add(date, "Expense", "Food", random.Next(15, 61), Pick("Groceries", "Lunch", "Coffee and snack", "Restaurant"));
                if (random.NextDouble() < 0.30)
                    Add(date, "Expense", "Transport", random.Next(8, 41), Pick("Bus ticket", "Taxi", "Fuel"));
                if (random.NextDouble() < 0.12)
                    Add(date, "Expense", "Shopping", random.Next(40, 251), Pick("Clothes", "Electronics", "Home items"));
                if (random.NextDouble() < 0.10)
                    Add(date, "Expense", "Entertainment", random.Next(30, 121), Pick("Cinema", "Concert", "Streaming"));
                if (random.NextDouble() < 0.06)
                    Add(date, "Expense", "Other", random.Next(10, 80), "Miscellaneous");
            }
        }

        await _db.InsertAllAsync(transactions);

        var existing = await GetBudgetsAsync(today.Month, today.Year);
        if (existing.Count == 0)
        {
            var budgets = new (string Category, int Ron)[]
            {
                ("Food", 1200), ("Transport", 400), ("Shopping", 600),
                ("Entertainment", 300), ("Bills", 800)
            };

            await _db.InsertAllAsync(budgets.Select(budget => new Budget
            {
                CategoryId = IdOf(budget.Category),
                LimitMinor = budget.Ron * 100L,
                Month = today.Month,
                Year = today.Year
            }));
        }
    }

    public async Task<List<Transaction>> GetTransactionsAsync()
    {
        await InitializeAsync();

        return await _db.Table<Transaction>()
            .OrderByDescending(t => t.Date)
            .ToListAsync();
    }

    public async Task<Transaction?> GetTransactionAsync(int id)
    {
        await InitializeAsync();

        return await _db.Table<Transaction>()
            .Where(t => t.Id == id)
            .FirstOrDefaultAsync();
    }

    public async Task<int> SaveTransactionAsync(Transaction transaction)
    {
        await InitializeAsync();

        if (transaction.Id == 0)
            return await _db.InsertAsync(transaction);

        return await _db.UpdateAsync(transaction);
    }

    public async Task<int> DeleteTransactionAsync(Transaction transaction)
    {
        await InitializeAsync();
        return await _db.DeleteAsync(transaction);
    }

    public async Task<List<Category>> GetCategoriesAsync()
    {
        await InitializeAsync();

        return await _db.Table<Category>()
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<int> SaveCategoryAsync(Category category)
    {
        await InitializeAsync();

        return category.Id == 0
            ? await _db.InsertAsync(category)
            : await _db.UpdateAsync(category);
    }

    public async Task<int> DeleteCategoryAsync(Category category)
    {
        await InitializeAsync();
        return await _db.DeleteAsync(category);
    }

    public async Task<List<Budget>> GetBudgetsAsync(int month, int year)
    {
        await InitializeAsync();

        return await _db.Table<Budget>()
            .Where(b => b.Month == month && b.Year == year)
            .ToListAsync();
    }

    public async Task<Budget?> GetBudgetAsync(int id)
    {
        await InitializeAsync();

        return await _db.Table<Budget>()
            .Where(budget => budget.Id == id)
            .FirstOrDefaultAsync();
    }

    public async Task<int> SaveBudgetAsync(Budget budget)
    {
        await InitializeAsync();

        return budget.Id == 0
            ? await _db.InsertAsync(budget)
            : await _db.UpdateAsync(budget);
    }

    public async Task<int> DeleteBudgetAsync(Budget budget)
    {
        await InitializeAsync();
        return await _db.DeleteAsync(budget);
    }
}
