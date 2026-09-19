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

        public static IServiceProvider Services => MauiProgram.Services;

        public App()
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                LogException("UnhandledException", ex);
                ShowErrorAlert(ex);
            };

            TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                LogException("UnobservedTaskException", e.Exception);
                e.SetObserved();
                ShowErrorAlert(e.Exception);
            };

            InitializeComponent();
            this.UserAppTheme = AppTheme.Dark;

            var builder = new ConfigurationBuilder()
                .SetBasePath(FileSystem.AppDataDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);

#if WINDOWS
            var exeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!;
            builder.SetBasePath(exeDir);
#endif

            Configuration = builder.Build();
        }

        private void LogException(string source, Exception? ex)
        {
            if (ex == null) return;
            System.Diagnostics.Debug.WriteLine($"‼️ {source}: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");
        }

        private void ShowErrorAlert(Exception? ex)
        {
            if (ex == null) return;
            try
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    // ✅ FIX: Wait a moment for the UI to be ready
                    await Task.Delay(100);
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
            // ===== ANDROID: Always use SQLite =====
            string dbPath = Path.Combine(FileSystem.AppDataDirectory, "Firetrack.db");
            connectionString = $"Data Source={dbPath}";
            System.Diagnostics.Debug.WriteLine($"ℹ️ [ANDROID] Using SQLite. DB Path: {dbPath}");
#else
            // ===== WINDOWS / OTHER: Try SQL Server first, fallback to SQLite =====
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
            else
            {
                System.Diagnostics.Debug.WriteLine("ℹ️ No SQL Server connection string found in appsettings.json.");
            }

            if (useSqlServer && serverCs != null)
            {
                connectionString = serverCs;
                System.Diagnostics.Debug.WriteLine($"ℹ️ [WINDOWS] Using SQL Server. Connection: {serverCs}");
            }
            else
            {
                string dbPath = Path.Combine(FileSystem.AppDataDirectory, "Firetrack.db");
                connectionString = $"Data Source={dbPath}";
                System.Diagnostics.Debug.WriteLine($"ℹ️ [WINDOWS] Using SQLite on Windows as fallback. DB Path: {dbPath}");
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
                // ✅ FIX: We cannot use MainPage here yet, so we just log.
                // The app will still start, and the error will be shown when the first page loads.
            }

            return new Window(new AppShell());
        }
    }
}