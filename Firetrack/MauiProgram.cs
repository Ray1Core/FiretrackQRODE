using Firetrack.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;   // ✅ Required for GetRequiredService<T>
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;
using PdfSharpCore.Fonts;
using SQLitePCL;
using ZXing.Net.Maui.Controls;

namespace Firetrack;

public static class MauiProgram
{
    // Store the built app so its services can be accessed from anywhere
    public static MauiApp MauiApp { get; private set; } = null!;
    public static IServiceProvider Services => MauiApp.Services;

    public static MauiApp CreateMauiApp()
    {
        // Initialize SQLite
        Batteries_V2.Init();
        SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_e_sqlite3());

        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseBarcodeReader()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // ---- Register application services ----
        builder.Services.AddSingleton<PdfGenerationService>();
        builder.Services.AddSingleton<EmailService>();        // ✅ For OTP emails
        builder.Services.AddSingleton<SyncService>(provider =>
        {
            string sqlitePath = Path.Combine(FileSystem.AppDataDirectory, "Firetrack.db");
            string sqliteConnectionString = $"Data Source={sqlitePath}";

            // Read SQL Server connection string from configuration
            string sqlServerConnectionString = App.Configuration.GetConnectionString("SqlServer")
                ?? "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=FiretrackDB;Integrated Security=True;Connect Timeout=30;Encrypt=False;";

            return new SyncService(sqliteConnectionString, sqlServerConnectionString);
        });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // ---- Build the application ----
        MauiApp = builder.Build();

        // ---- Register PDF font resolver ----
        try
        {
            GlobalFontSettings.FontResolver = new PdfFontResolver();
            System.Diagnostics.Debug.WriteLine("✅ PDF Font Resolver registered successfully.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"⚠️ Failed to set PDF font resolver: {ex.Message}");
        }

        return MauiApp;
    }
}