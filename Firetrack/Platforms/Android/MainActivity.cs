using Android.App;
using Android.Content.PM;
using Android.OS;
using Java.Lang; // Provides ProcessBuilder

namespace Firetrack
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // ===== WORKAROUND FOR OPPO/OnePlus HORAE SERVICE =====
            try
            {
                var process = new ProcessBuilder("setprop", "persist.sys.horae.enable", "0");
                process.Start();
                System.Diagnostics.Debug.WriteLine("✅ Horae service disabled");
            }
            catch (System.Exception ex) // 🔥 Explicitly qualify to avoid ambiguity with Java.Lang.Exception
            {
                System.Diagnostics.Debug.WriteLine($"ℹ️ Horae disable ignored: {ex.Message}");
            }
        }
    }
}