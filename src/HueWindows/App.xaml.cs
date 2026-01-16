using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using CommunityToolkit.Mvvm.Messaging;
using HueWindows.Core.Services;
using HueWindows.Core.Services.Interfaces;
using HueWindows.Core.ViewModels;
using HueWindows.Helpers;

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

    /// <summary>
    /// Initializes the singleton application object.
    /// </summary>
    public App()
    {
        this.InitializeComponent();
        Services = ConfigureServices();
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
        MainWindow.Activate();
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
        services.AddSingleton<IHueBridgeService, HueBridgeService>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IPinnedItemsService, PinnedItemsService>();
        services.AddSingleton<ISceneStorageService, SceneStorageService>();
        services.AddSingleton<IAnimationService, AnimationService>();

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
