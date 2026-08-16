using Microsoft.Extensions.Logging;
using Firetrack.Services;
using SQLitePCL;
using ZXing.Net.Maui.Controls;   // ✅ Required for .UseBarcodeReader()
using Microsoft.Maui.Storage;   // For FileSystem

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
            .UseBarcodeReader()   // ✅ ESSENTIAL – enables ZXing QR scanning
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddSingleton<PdfGenerationService>();

        // ✅ Register SyncService with proper connection strings
        builder.Services.AddSingleton<SyncService>(provider =>
        {
            // SQLite path (Android) or SQL Server connection string (Windows)
            string sqlitePath = Path.Combine(FileSystem.AppDataDirectory, "Firetrack.db");
            string sqliteConnectionString = $"Data Source={sqlitePath}";
            string sqlServerConnectionString = App.SqlServerConnectionString; // from App.xaml.cs

            return new SyncService(sqliteConnectionString, sqlServerConnectionString);
        });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}