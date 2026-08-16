using Firetrack.Models;
using Firetrack.Services;
using Microsoft.Maui.Controls;

namespace Firetrack
{
    public partial class App : Application
    {
        public static UserModel? CurrentUser { get; set; }
        public static DatabaseService? Database { get; private set; }

        public App()
        {
            InitializeComponent();

            // ❌ Removed forced Dark Theme – let system decide.
            // Current!.UserAppTheme = AppTheme.Dark;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
#if ANDROID
            string dbPath = Path.Combine(FileSystem.AppDataDirectory, "Firetrack.db");
            string connectionString = $"Data Source={dbPath}";
#else
            string connectionString = @"Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=FiretrackDB;Integrated Security=True;Connect Timeout=30;Encrypt=False;TrustServerCertificate=True;";
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