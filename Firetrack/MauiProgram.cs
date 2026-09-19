using Firetrack.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;
using SQLitePCL;
using ZXing.Net.Maui.Controls;
using PdfSharpCore.Fonts;
using System.IO;
using System.Reflection;

namespace Firetrack;

public static class MauiProgram
{
    public static MauiApp MauiApp { get; private set; } = null!;
    public static IServiceProvider Services => MauiApp.Services;

    // Expose Configuration so App.xaml.cs can use it
    public static IConfiguration Configuration { get; private set; } = null!;

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

        // ============================================================
        // FIX: Build IConfiguration here and register it for DI
        // ============================================================
        var configBuilder = new ConfigurationBuilder()
            .SetBasePath(FileSystem.AppDataDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);

#if WINDOWS
        var exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        configBuilder.SetBasePath(exeDir);
#endif

        Configuration = configBuilder.Build();

        // Register the configuration so EmailService receives it via DI
        builder.Services.AddSingleton<IConfiguration>(Configuration);

        // Register Services
        builder.Services.AddSingleton<PdfGenerationService>();
        builder.Services.AddSingleton<EmailService>();
        builder.Services.AddSingleton<SyncService>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        MauiApp = builder.Build();

        // ---- Set custom font resolver for PdfSharpCore ----
        GlobalFontSettings.FontResolver = new AppFontResolver();

        return MauiApp;
    }
}