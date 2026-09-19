using Firetrack.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;
using PdfSharpCore.Fonts;
using SQLitePCL;
using System;
using System.IO;
using System.Reflection;
using ZXing.Net.Maui.Controls;

namespace Firetrack;

public static class MauiProgram
{
    public static MauiApp MauiApp { get; private set; } = null!;
    public static IServiceProvider Services => MauiApp.Services;

    // Exposed so App.xaml.cs and EmailService can read config
    public static IConfiguration Configuration { get; private set; } = null!;

    public static MauiApp CreateMauiApp()
    {
        // ---- SQLite native provider init ----
        Batteries_V2.Init();
        SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_e_sqlite3());

        // ============================================================
        // LOAD appsettings.json as EMBEDDED RESOURCE
        // ------------------------------------------------------------
        // Embedding the config file makes it available on ALL platforms
        // (Windows + Android) synchronously via Assembly reflection.
        // This replaces the old AddJsonFile + SetBasePath approach which
        // triggered warning XA0101 on Android and left the file missing
        // from the APK.
        //
        // Expected resource name: <AssemblyName>.appsettings.json
        // Since the file lives at the project root, this resolves to
        // "Firetrack.appsettings.json".
        // ============================================================
        var assembly = typeof(MauiProgram).Assembly;
        string resourceName = $"{assembly.GetName().Name}.appsettings.json";

        var configBuilder = new ConfigurationBuilder();

        using (var stream = assembly.GetManifestResourceStream(resourceName))
        {
            if (stream != null)
            {
                configBuilder.AddJsonStream(stream);
                System.Diagnostics.Debug.WriteLine($"✅ Loaded embedded config: {resourceName}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ Embedded resource '{resourceName}' not found. Falling back to defaults.");
                System.Diagnostics.Debug.WriteLine("   Available embedded resources:");
                foreach (var name in assembly.GetManifestResourceNames())
                    System.Diagnostics.Debug.WriteLine($"     {name}");
            }
        }

        Configuration = configBuilder.Build();

        // ============================================================
        // BUILD MauiApp
        // ============================================================
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseBarcodeReader()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Register configuration for DI (EmailService depends on it)
        builder.Services.AddSingleton<IConfiguration>(Configuration);

        // ---- Register Services ----
        builder.Services.AddSingleton<PdfGenerationService>();
        builder.Services.AddSingleton<EmailService>();
        builder.Services.AddSingleton<SyncService>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        MauiApp = builder.Build();

        // ============================================================
        // PDF FONT RESOLVER — must run after MauiApp is built
        // ------------------------------------------------------------
        // AppFontResolver loads OpenSans-Regular.ttf / OpenSans-Semibold.ttf
        // from the assembly as EmbeddedResource (see Firetrack.csproj).
        // This works on Android because MauiFont files are NOT accessible
        // via FileSystem.OpenAppPackageFileAsync — they are compiled into
        // native font resources, which is why we use embedded resources.
        // ============================================================
        GlobalFontSettings.FontResolver = new AppFontResolver();

        return MauiApp;
    }
}