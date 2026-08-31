using Firetrack.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;
using SQLitePCL;
using ZXing.Net.Maui.Controls;

namespace Firetrack;

public static class MauiProgram
{
    public static MauiApp MauiApp { get; private set; } = null!;
    public static IServiceProvider Services => MauiApp.Services;

    public static MauiApp CreateMauiApp()
    {
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

        // ---- Register services ----
        builder.Services.AddSingleton<PdfGenerationService>();
        builder.Services.AddSingleton<EmailService>();
        builder.Services.AddSingleton<SyncService>(provider =>
        {
            string sqlitePath = Path.Combine(FileSystem.AppDataDirectory, "Firetrack.db");
            string sqliteConnectionString = $"Data Source={sqlitePath}";
            string sqlServerConnectionString = App.Configuration.GetConnectionString("SqlServer")
                ?? "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=FiretrackDB;Integrated Security=True;Connect Timeout=30;Encrypt=False;";
            return new SyncService(sqliteConnectionString, sqlServerConnectionString);
        });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // ---- Build the application ----
        MauiApp = builder.Build();

        // ❌ Remove PDF font resolver – built‑in fonts work fine
        // GlobalFontSettings.FontResolver = new PdfFontResolver();

        return MauiApp;
    }
}