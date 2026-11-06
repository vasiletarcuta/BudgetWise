using Android.App;
using Android.Content.PM;
using Android.OS;

namespace BudgetWise;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // The app uses a light theme, so the status and navigation bar icons must be dark.
        if (Window is { } window && AndroidX.Core.View.WindowCompat.GetInsetsController(window, window.DecorView) is { } controller)
        {
            controller.AppearanceLightStatusBars = true;
            controller.AppearanceLightNavigationBars = true;
        }
    }
}
