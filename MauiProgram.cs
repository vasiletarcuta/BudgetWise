using BudgetWise.Services;
using BudgetWise.Views;
using Microsoft.Extensions.Logging;

namespace BudgetWise;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif

		builder.Services.AddSingleton<DatabaseService>();
		builder.Services.AddSingleton<CurrencyService>(_ =>
			new CurrencyService(new HttpClient
			{
				BaseAddress = new Uri("https://api.frankfurter.dev/"),
				Timeout = TimeSpan.FromSeconds(12)
			}));
		builder.Services.AddTransient<CurrencyConverterPage>();
		builder.Services.AddTransient<SimulatorPage>();
		builder.Services.AddTransient<WrappedPage>();
		builder.Services.AddTransient<DashboardPage>();
		builder.Services.AddTransient<TransactionsPage>();
		builder.Services.AddTransient<TransactionFormPage>();
		builder.Services.AddTransient<BudgetsPage>();
		builder.Services.AddTransient<BudgetFormPage>();
		builder.Services.AddTransient<StatisticsPage>();

		return builder.Build();
	}
}
