using Firetrack.Models;
using Firetrack.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using System.IO;

namespace Firetrack
{
    public partial class App : Application
    {
        public static UserModel? CurrentUser { get; set; }
        public static DatabaseService? Database { get; private set; }

        public static string SqlServerConnectionString { get; set; } =
            @"Data Source=10.209.102.18;Initial Catalog=FiretrackDB;User ID=firetrack_user;Password=yourpassword;Connect Timeout=30;Encrypt=False;";

        public App()
        {
            InitializeComponent();
            this.UserAppTheme = AppTheme.Dark;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
#if ANDROID
            string dbPath = Path.Combine(FileSystem.AppDataDirectory, "Firetrack.db");
            string connectionString = $"Data Source={dbPath}";

            // ===== ADD THIS =====
            // Log database file existence
            bool dbExists = File.Exists(dbPath);
            System.Diagnostics.Debug.WriteLine($"✅ Database file exists: {dbExists} at {dbPath}");

            // Ensure directory exists
            var dir = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
                System.Diagnostics.Debug.WriteLine($"✅ Created directory: {dir}");
            }
#else
            string connectionString = SqlServerConnectionString;
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