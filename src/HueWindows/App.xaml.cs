using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using CommunityToolkit.Mvvm.Messaging;
using HueWindows.Core.Services;
using HueWindows.Core.Services.Interfaces;
using HueWindows.Core.ViewModels;
using HueWindows.Helpers;
using HueWindows.Services;

namespace HueWindows;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// Gets the service provider for dependency injection.
    /// </summary>
    public static IServiceProvider Services { get; private set; } = null!;

    /// <summary>
    /// Gets the main window instance.
    /// </summary>
    public static MainWindow MainWindow { get; private set; } = null!;

    /// <summary>
    /// Gets the parsed command-line arguments.
    /// </summary>
    public static CommandLineArgs CommandLineArgs { get; private set; } = null!;

    private static readonly string CrashLogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HueWindows", "crash.log");

    /// <summary>
    /// Initializes the singleton application object.
    /// </summary>
    public App()
    {
        this.InitializeComponent();
        Services = ConfigureServices();

        // Set up unhandled exception handlers for crash logging
        this.UnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        LogCrash("UnhandledException", e.Exception);
        e.Handled = false; // Let it crash but we have the log
    }

    private void OnDomainUnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        LogCrash("DomainUnhandledException", e.ExceptionObject as Exception);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LogCrash("UnobservedTaskException", e.Exception);
    }

    private static void LogCrash(string source, Exception? ex)
    {
        try
        {
            var dir = Path.GetDirectoryName(CrashLogPath);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var message = $"""
                ============ CRASH LOG ============
                Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
                Source: {source}
                Exception: {ex?.GetType().FullName ?? "Unknown"}
                Message: {ex?.Message ?? "No message"}

                Stack Trace:
                {ex?.StackTrace ?? "No stack trace"}

                Inner Exception:
                {ex?.InnerException?.Message ?? "None"}
                {ex?.InnerException?.StackTrace ?? ""}
                ===================================

                """;

            File.AppendAllText(CrashLogPath, message);
            System.Diagnostics.Debug.WriteLine(message);
        }
        catch
        {
            // Can't log the crash log failure
        }
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Parse command-line arguments
        CommandLineArgs = CommandLineParser.Parse(Environment.GetCommandLineArgs());

        if (!CommandLineArgs.IsValid)
        {
            System.Diagnostics.Debug.WriteLine($"[App] Invalid command-line args: {CommandLineArgs.ErrorMessage}");
        }

        // Load settings
        var settingsService = Services.GetRequiredService<ISettingsService>();
        await settingsService.LoadAsync();

        // Create and activate main window
        MainWindow = new MainWindow();

        // Check if we should start minimized
        var startMinimized = settingsService.Settings.StartMinimized && settingsService.Settings.MinimizeToTray;

        if (!startMinimized)
        {
            MainWindow.Activate();
        }

        // Initialize dispatcher helper for UI thread marshaling
        DispatcherHelper.Initialize(MainWindow.DispatcherQueue);
    }

    /// <summary>
    /// Configures the dependency injection container.
    /// </summary>
    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Register services
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IBridgeDiscoveryService, BridgeDiscoveryService>();
        services.AddSingleton<IMultiBridgeService, MultiBridgeService>();
        // Note: IHueBridgeService is no longer registered directly.
        // ViewModels that need bridge services should use IMultiBridgeService and call
        // GetDefaultBridgeService() or GetBridgeService(bridgeId) as needed.
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IPinnedItemsService, PinnedItemsService>();
        services.AddSingleton<ISceneStorageService, SceneStorageService>();
        services.AddSingleton<IAnimationService, AnimationService>();
        services.AddSingleton<IRoomSceneAssignmentService, RoomSceneAssignmentService>();

        // Register hotkey and system tray services
        services.AddSingleton<HotkeyService>();
        services.AddSingleton<IHotkeyService>(sp => sp.GetRequiredService<HotkeyService>());
        services.AddSingleton<SystemTrayService>();
        services.AddSingleton<ISystemTrayService>(sp => sp.GetRequiredService<SystemTrayService>());

        // Register ViewModels
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<CustomDashboardViewModel>();
        services.AddTransient<RoomDetailViewModel>();
        services.AddTransient<LightDetailViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<SetupViewModel>();
        services.AddTransient<ScenesViewModel>();
        services.AddTransient<SceneLibraryViewModel>();
        services.AddTransient<SceneBuilderViewModel>();

        // Register Messenger for MVVM communication
        services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);

        return services.BuildServiceProvider();
    }
}
