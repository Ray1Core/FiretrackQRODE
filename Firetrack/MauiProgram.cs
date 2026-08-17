using Microsoft.Extensions.Logging;
using Firetrack.Services;
using SQLitePCL;
using ZXing.Net.Maui.Controls;
using Microsoft.Maui.Storage;
using PdfSharpCore.Fonts;   // <-- ADD THIS

namespace Firetrack;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        // ✅ Must be called before any database code
        Batteries_V2.Init();

        // ✅ Explicitly set the SQLite provider for Android
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

        builder.Services.AddSingleton<PdfGenerationService>();

        // ✅ Register SyncService with proper connection strings
        builder.Services.AddSingleton<SyncService>(provider =>
        {
            string sqlitePath = Path.Combine(FileSystem.AppDataDirectory, "Firetrack.db");
            string sqliteConnectionString = $"Data Source={sqlitePath}";
            string sqlServerConnectionString = App.SqlServerConnectionString;

            return new SyncService(sqliteConnectionString, sqlServerConnectionString);
        });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // ===== ✅ FIX PDF FONT RESOLVER =====
        // Register the custom font resolver BEFORE any PDF is generated
        try
        {
            GlobalFontSettings.FontResolver = new PdfFontResolver();
            System.Diagnostics.Debug.WriteLine("✅ PDF Font Resolver registered successfully.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"⚠️ Failed to set PDF font resolver: {ex.Message}");
        }

        return builder.Build();
    }
}