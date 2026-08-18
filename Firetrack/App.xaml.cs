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

        // Configurable SQL Server connection string using laptop's IP
        // You need to create a SQL Server login (User ID/Password) for this to work
        public static string SqlServerConnectionString { get; set; } =
            @"Data Source=10.209.102.18;Initial Catalog=FiretrackDB;User ID=firetrack_user;Password=yourpassword;Connect Timeout=30;Encrypt=False;";

        public App()
        {
            InitializeComponent();
            // Force dark theme for consistency across all pages
            this.UserAppTheme = AppTheme.Dark;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
#if ANDROID
            string dbPath = Path.Combine(FileSystem.AppDataDirectory, "Firetrack.db");
            string connectionString = $"Data Source={dbPath}";
#else
            // Use configurable connection string (can be changed at runtime)
            string connectionString = SqlServerConnectionString;
#endif

            try
            {
                Database = new DatabaseService(connectionString);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Database initialization failed: {ex}");
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