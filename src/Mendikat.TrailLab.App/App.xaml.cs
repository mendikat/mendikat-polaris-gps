using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using Mendikat.TrailLab.App.Logging;
using Mendikat.TrailLab.App.Map;
using Mendikat.TrailLab.App.Resources;
using Mendikat.TrailLab.App.Services;
using Mendikat.TrailLab.App.Settings;
using Mendikat.TrailLab.App.ViewModels;
using Mendikat.TrailLab.Mapping.Elevation;
using Mendikat.TrailLab.Mapping.Tiles;
using Microsoft.Extensions.Logging;

namespace Mendikat.TrailLab.App;

/// <summary>
/// Main application entry point.
/// </summary>
public partial class App : Application
{
    private ILoggerFactory? _loggerFactory;
    private ILogger? _logger;
    private HttpClient? _tileHttpClient;
    private HttpClient? _demHttpClient;

    /// <summary>
    /// Handles the application startup event.
    /// </summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mendikat", "TrailLab", "logs");
        _loggerFactory = LoggerFactory.Create(builder => builder
            .SetMinimumLevel(LogLevel.Information)
            .AddProvider(new FileLoggerProvider(logDirectory, PathRedactor.ForCurrentUser())));
        _logger = _loggerFactory.CreateLogger<App>();
        RegisterGlobalExceptionHandlers();
        _logger.LogInformation("Mendikat TrailLab {Version} starting", typeof(App).Assembly.GetName().Version);

        RouteMapsuiLogging();

        var version = typeof(App).Assembly.GetName().Version?.ToString(3) ?? "0";
        _tileHttpClient = TileHttpClientFactory.Create(version);
        // DEM tiles are larger and slower than map tiles: longer timeout (DEM-01).
        _demHttpClient = TileHttpClientFactory.Create(version, timeout: TimeSpan.FromSeconds(30));
        // One source per resolution for the whole session, so their tile caches are shared by the "Adjust elevations"
        // tool and the pointer elevation in the status bar (COORD-08).
        var dem5 = new IgnWcsElevationSource(_demHttpClient, IgnDemResolution.FiveMeters);
        var dem25 = new IgnWcsElevationSource(_demHttpClient, IgnDemResolution.TwentyFiveMeters);
        var settings = new JsonSettingsStore(JsonSettingsStore.DefaultPath, _loggerFactory.CreateLogger<JsonSettingsStore>());
        ApplyCulture(AppLanguage.FromCode(settings.Load().Language));
        var viewModel = new MainViewModel(
            new WpfDialogService(), new FileDocumentStore(), settings, _loggerFactory.CreateLogger<MainViewModel>())
        {
            ElevationSources = resolution => resolution == IgnDemResolution.FiveMeters ? dem5 : dem25,
            LocalMapLoader = (path, cancellationToken) => Mapping.Local.LocalMapLoader.Load(path, new WpfRasterImageDecoder(), cancellationToken),
        };
        var window = new MainWindow(
            viewModel, map => new MapController(map, _tileHttpClient, _loggerFactory.CreateLogger<MapController>()));
        MainWindow = window;
        window.Show();
    }

    /// <summary>
    /// Sends Mapsui's own errors to the technical log. Tile failures are reported by MapController
    /// (once per state change), so they are skipped here to avoid flooding the log when offline.
    /// </summary>
    private void RouteMapsuiLogging()
    {
        // Mapsui draws its log entries over the map whenever a debugger is attached (F5); they belong in the log file.
        Mapsui.Widgets.InfoWidgets.LoggingWidget.ShowLoggingInMap = Mapsui.Widgets.ActiveMode.No;

        var logger = _loggerFactory!.CreateLogger("Mapsui");
        Mapsui.Logging.Logger.LogDelegate = (level, message, exception) =>
        {
            if (level != Mapsui.Logging.LogLevel.Error
                || exception is HttpRequestException or OperationCanceledException or IOException)
            {
                return;
            }

            logger.LogError(exception, "{Message}", message);
        };
    }

    /// <summary>
    /// Handles the application exit event.
    /// </summary>
    protected override void OnExit(ExitEventArgs e)
    {
        _logger?.LogInformation("Mendikat TrailLab exiting");
        _tileHttpClient?.Dispose();
        _demHttpClient?.Dispose();
        _loggerFactory?.Dispose();
        base.OnExit(e);
    }

    /// <summary>
    /// Texts in the language chosen in File › Settings (Spanish by default). Numbers and dates keep the Spanish format
    /// (decimal comma) whatever the language and the Windows regional settings: coordinates typed by the user and
    /// exported values do not change with the language.
    /// </summary>
    private static void ApplyCulture(AppLanguage language)
    {
        var culture = CultureInfo.GetCultureInfo("es-ES");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = language.Culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = language.Culture;
        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement), new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(culture.IetfLanguageTag)));
    }

    /// <summary>
    /// Registers global exception handlers for the application.
    /// </summary>
    private void RegisterGlobalExceptionHandlers()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            _logger?.LogError(args.Exception, "Unobserved task exception");
            args.SetObserved();
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            _logger?.LogCritical(args.ExceptionObject as Exception, "Fatal unhandled exception");
    }

    /// <summary>
    /// Manages unhandled exceptions that occur on the UI thread.
    /// </summary>
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _logger?.LogError(e.Exception, "Unhandled exception on UI thread");
        MessageBox.Show(
            MainWindow,
            string.Format(Strings.ErrorUnexpected, e.Exception.Message),
            Strings.DialogErrorTitle,
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }
}
