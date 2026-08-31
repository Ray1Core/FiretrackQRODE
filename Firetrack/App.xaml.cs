using Firetrack.Models;
using Firetrack.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;   // ✅ Added for IServiceProvider
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using System.IO;

namespace Firetrack
{
    public partial class App : Application
    {
        public static UserModel? CurrentUser { get; set; }
        public static DatabaseService? Database { get; private set; }
        public static IConfiguration Configuration { get; private set; } = null!;

        // ✅ NEW: Expose the service provider from MauiProgram
        public static IServiceProvider Services => MauiProgram.Services;

        public App()
        {
            InitializeComponent();
            this.UserAppTheme = AppTheme.Dark;

            // Load configuration from appsettings.json
            var builder = new ConfigurationBuilder()
                .SetBasePath(FileSystem.AppDataDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);

            // On Windows, also look in the executable folder
#if WINDOWS
            var exeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!;
            builder.SetBasePath(exeDir);
#endif

            Configuration = builder.Build();
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
            string? serverCs = Configuration.GetConnectionString("SqlServer"); // ✅ nullable
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
                // Fallback to SQLite on Windows
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