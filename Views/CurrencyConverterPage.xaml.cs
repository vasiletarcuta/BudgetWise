using System.Globalization;
using BudgetWise.Services;

namespace BudgetWise.Views;

public partial class CurrencyConverterPage : ContentPage
{
    private static readonly string[] SupportedCurrencies = { "RON", "EUR", "USD" };
    private readonly CurrencyService _currencyService;
    private readonly CultureInfo _culture = CultureInfo.GetCultureInfo("ro-RO");
    private bool _converting;

    public CurrencyConverterPage(CurrencyService currencyService)
    {
        InitializeComponent();
        _currencyService = currencyService;

        foreach (string currency in SupportedCurrencies)
        {
            FromPicker.Items.Add(currency);
            ToPicker.Items.Add(currency);
        }

        FromPicker.SelectedItem = "RON";
        ToPicker.SelectedItem = "EUR";
    }

    private async void OnConvertClicked(object? sender, EventArgs e)
    {
        if (_converting)
            return;

        string rawAmount = (AmountEntry.Text ?? string.Empty).Trim().Replace(',', '.');
        if (!decimal.TryParse(rawAmount, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal amount) ||
            amount <= 0 || amount > 1_000_000_000m || decimal.Round(amount, 2) != amount)
        {
            await DisplayAlertAsync("Invalid amount", "Enter a positive amount with up to 2 decimal places.", "OK");
            return;
        }

        if (FromPicker.SelectedItem is not string from || ToPicker.SelectedItem is not string to)
        {
            await DisplayAlertAsync("Missing currency", "Please select both currencies.", "OK");
            return;
        }

        _converting = true;
        ConvertButton.IsEnabled = false;
        SwapButton.IsEnabled = false;
        LoadingIndicator.IsVisible = true;
        LoadingIndicator.IsRunning = true;
        StatusLabel.Text = "Retrieving the latest available rate...";

        try
        {
            CurrencyRate rateInfo = await _currencyService.GetRateAsync(
                from,
                to,
                OfflineCacheSwitch.IsToggled);

            decimal converted = decimal.Round(
                amount * rateInfo.Rate,
                2,
                MidpointRounding.AwayFromZero);

            ResultLabel.Text = converted.ToString("N2", _culture) + " " + to;
            RateLabel.Text = $"1 {from} = {rateInfo.Rate.ToString("0.######", _culture)} {to}";

            if (from == to)
            {
                RateDateLabel.Text = "No exchange rate required";
                StatusLabel.Text = "Both currencies are identical.";
            }
            else
            {
                RateDateLabel.Text = "Rate date: " + rateInfo.Date;
                StatusLabel.Text = rateInfo.IsCached
                    ? "Offline: using a previously saved rate."
                    : "Latest available published daily rate.";
            }
        }
        catch (Exception ex)
        {
            ResultLabel.Text = "—";
            RateLabel.Text = "Exchange rate unavailable";
            RateDateLabel.Text = string.Empty;
            StatusLabel.Text = "Conversion could not be completed.";
            await DisplayAlertAsync("Conversion error", ex.Message, "OK");
        }
        finally
        {
            LoadingIndicator.IsRunning = false;
            LoadingIndicator.IsVisible = false;
            ConvertButton.IsEnabled = true;
            SwapButton.IsEnabled = true;
            _converting = false;
        }
    }

    private void OnSwapClicked(object? sender, TappedEventArgs e)
    {
        object? previousFrom = FromPicker.SelectedItem;
        FromPicker.SelectedItem = ToPicker.SelectedItem;
        ToPicker.SelectedItem = previousFrom;
        ClearResult();
    }

    private void OnAmountChanged(object? sender, TextChangedEventArgs e)
    {
        ClearResult();
    }

    private void OnCurrencyChanged(object? sender, EventArgs e)
    {
        ClearResult();
    }

    private void ClearResult()
    {
        if (ResultLabel is null || RateLabel is null || RateDateLabel is null || StatusLabel is null)
            return;

        ResultLabel.Text = "—";
        RateLabel.Text = "Press Convert to calculate";
        RateDateLabel.Text = "Exchange rate not loaded";
        StatusLabel.Text = "Frankfurter publishes daily reference rates.";
    }

    private async void OnBackClicked(object? sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
