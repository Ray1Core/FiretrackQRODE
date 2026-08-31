using Firetrack.Models;
using Firetrack.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Firetrack
{
    public partial class App : Application
    {
        public static UserModel? CurrentUser { get; set; }
        public static DatabaseService? Database { get; private set; }
        public static IConfiguration Configuration { get; private set; } = null!;

        // NEW: Expose the service provider from MauiProgram
        public static IServiceProvider Services => MauiProgram.Services;

        public App()
        {
            // ---- GLOBAL EXCEPTION HANDLERS ----
            // Catch any unhandled exceptions from the UI thread
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                LogException("UnhandledException", ex);
                ShowErrorAlert(ex);
            };

            // Catch unobserved task exceptions (background threads)
            TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                LogException("UnobservedTaskException", e.Exception);
                e.SetObserved(); // prevent the app from crashing
                ShowErrorAlert(e.Exception);
            };

            // Also catch UI thread exceptions via the dispatcher (optional)
            // This is not available in all MAUI versions, but we can add it:
            // Microsoft.Maui.Controls.Application.Current?.Dispatcher.UnhandledException += ...

            InitializeComponent();
            this.UserAppTheme = AppTheme.Dark;

            // ---- Load configuration ----
            var builder = new ConfigurationBuilder()
                .SetBasePath(FileSystem.AppDataDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);

#if WINDOWS
            var exeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!;
            builder.SetBasePath(exeDir);
#endif

            Configuration = builder.Build();

            // ---- Initialise database (will be done in CreateWindow) ----
        }

        private void LogException(string source, Exception? ex)
        {
            if (ex == null) return;
            System.Diagnostics.Debug.WriteLine($"‼️ {source}: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");
            // Also write to a file if you want
            // File.AppendAllText(Path.Combine(FileSystem.AppDataDirectory, "error.log"), $"{DateTime.Now}: {source} - {ex}\n");
        }

        private void ShowErrorAlert(Exception? ex)
        {
            if (ex == null) return;
            // Try to show a message box on the UI thread (if the main page exists)
            try
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    if (Application.Current?.MainPage != null)
                    {
                        await Application.Current.MainPage.DisplayAlert(
                            "Unexpected Error",
                            $"Something went wrong:\n{ex.Message}\n\nCheck the debug output for details.",
                            "OK");
                    }
                });
            }
            catch { /* Ignore if UI not ready */ }
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            string connectionString;

#if ANDROID
            // On Android, always use SQLite
            string dbPath = Path.Combine(FileSystem.AppDataDirectory, "Firetrack.db");
            connectionString = $"Data Source={dbPath}";
#else
            // On Windows, try SQL Server first, then fallback to SQLite
            string? serverCs = Configuration.GetConnectionString("SqlServer");
            bool useSqlServer = false;

            if (!string.IsNullOrEmpty(serverCs))
            {
                try
                {
                    using var testConn = new Microsoft.Data.SqlClient.SqlConnection(serverCs);
                    testConn.Open();
                    useSqlServer = true;
                    System.Diagnostics.Debug.WriteLine("✅ SQL Server connection successful.");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ SQL Server connection failed: {ex.Message}. Falling back to SQLite.");
                    useSqlServer = false;
                }
            }

            if (useSqlServer && serverCs != null)
            {
                connectionString = serverCs;
            }
            else
            {
                string dbPath = Path.Combine(FileSystem.AppDataDirectory, "Firetrack.db");
                connectionString = $"Data Source={dbPath}";
                System.Diagnostics.Debug.WriteLine("ℹ️ Using SQLite on Windows as fallback.");
            }
#endif

            try
            {
                Database = new DatabaseService(connectionString);
                System.Diagnostics.Debug.WriteLine("✅ Database initialized successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Database init failed: {ex}");
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await Application.Current!.MainPage!.DisplayAlert("Error",
                        $"Database error: {ex.Message}\nCheck logs for details.", "OK");
                });
                throw;
            }

            return new Window(new AppShell());
        }
    }
}