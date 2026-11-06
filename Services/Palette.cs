namespace BudgetWise.Services;

// Code-side access to the colours defined in Resources/Styles/Palette.xaml, so charts and
// code-behind use the same single source as the XAML pages.
public static class Palette
{
    public static Color Background => Get("AppBackground");
    public static Color Card => Get("AppCard");
    public static Color CardAlt => Get("AppCardAlt");
    public static Color Border => Get("AppBorder");
    public static Color Track => Get("AppTrack");
    public static Color Grid => Get("AppGrid");
    public static Color Text => Get("AppText");
    public static Color Muted => Get("AppMuted");
    public static Color Subtle => Get("AppSubtle");
    public static Color Primary => Get("AppPrimary");
    public static Color PrimaryText => Get("AppPrimaryText");
    public static Color OnPrimary => Get("AppOnPrimary");
    public static Color Income => Get("AppIncome");
    public static Color IncomeText => Get("AppIncomeText");
    public static Color Butter => Get("AppButter");
    public static Color Expense => Get("AppExpense");
    public static Color ExpenseText => Get("AppExpenseText");
    public static Color HeatEmpty => Get("AppHeatEmpty");
    public static Color WrappedIntroBg => Get("AppWrappedIntroBg");
    public static Color WrappedIntroAccent => Get("AppWrappedIntroAccent");
    public static Color WrappedText => Get("AppWrappedText");
    public static Color WrappedMuted => Get("AppWrappedMuted");
    public static Color WrappedChip => Get("AppWrappedChip");
    public static Color WrappedTrack => Get("AppWrappedTrack");
    public static Color WrappedTopBg => Get("AppWrappedTopBg");
    public static Color WrappedTopAccent => Get("AppWrappedTopAccent");
    public static Color WrappedDayBg => Get("AppWrappedDayBg");
    public static Color WrappedDayAccent => Get("AppWrappedDayAccent");
    public static Color WrappedSaveBg => Get("AppWrappedSaveBg");
    public static Color WrappedSaveAccent => Get("AppWrappedSaveAccent");
    public static Color WrappedCompareBg => Get("AppWrappedCompareBg");
    public static Color WrappedCompareAccent => Get("AppWrappedCompareAccent");

    public static Color[] Chart => new[]
    {
        Get("AppChart1"), Get("AppChart2"), Get("AppChart3"), Get("AppChart4"),
        Get("AppChart5"), Get("AppChart6"), Get("AppChart7"), Get("AppChart8")
    };

    private static Color Get(string key)
    {
        return Application.Current?.Resources.TryGetValue(key, out object? value) == true && value is Color color
            ? color
            : Colors.Magenta;
    }
}
