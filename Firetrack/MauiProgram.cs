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
        //
        // IMPORTANT: AddJsonStream() reads the stream LAZILY — it does
        // NOT consume it at call time. The stream must remain readable
        // until configBuilder.Build() is called. We therefore:
        //   1) Copy the embedded resource into a MemoryStream
        //   2) Pass the MemoryStream to AddJsonStream()
        //   3) Call Build() while the MemoryStream is still alive
        //   4) Dispose the MemoryStream afterwards
        // ============================================================
        var assembly = typeof(MauiProgram).Assembly;
        string resourceName = $"{assembly.GetName().Name}.appsettings.json";

        var configBuilder = new ConfigurationBuilder();
        MemoryStream? configStream = null;

        var resourceStream = assembly.GetManifestResourceStream(resourceName);
        if (resourceStream != null)
        {
            // Copy embedded resource into a MemoryStream so it stays readable
            configStream = new MemoryStream();
            resourceStream.CopyTo(configStream);
            resourceStream.Dispose();

            // Rewind so AddJsonStream can read from the beginning
            configStream.Position = 0;
            configBuilder.AddJsonStream(configStream);

            System.Diagnostics.Debug.WriteLine($"✅ Loaded embedded config: {resourceName}");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine(
                $"⚠️ Embedded resource '{resourceName}' not found. Falling back to defaults.");
            System.Diagnostics.Debug.WriteLine("   Available embedded resources:");
            foreach (var name in assembly.GetManifestResourceNames())
                System.Diagnostics.Debug.WriteLine($"     {name}");
        }

        // Build() reads the stream now — it must still be alive at this point
        Configuration = configBuilder.Build();

        // Safe to release now; the configuration has been fully materialized
        configStream?.Dispose();

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
        // ============================================================
        GlobalFontSettings.FontResolver = new AppFontResolver();

        return MauiApp;
    }
}