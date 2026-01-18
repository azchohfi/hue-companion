using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HueWindows.Core.Models;
using HueWindows.Core.Services.Interfaces;

namespace HueWindows.Core.ViewModels;

/// <summary>
/// ViewModel for the settings page.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IMultiBridgeService _multiBridgeService;

    [ObservableProperty]
    private AppTheme _selectedTheme;

    [ObservableProperty]
    private string _appVersion = "1.0.0";

    [ObservableProperty]
    private int _bridgeCount;

    [ObservableProperty]
    private int _connectedBridgeCount;

    [ObservableProperty]
    private BridgeManagementViewModel? _bridgeManagement;

    /// <summary>
    /// Event raised when user wants to navigate to bridge setup.
    /// </summary>
    public event EventHandler? BridgeSetupRequested;

    public SettingsViewModel(
        ISettingsService settingsService,
        IMultiBridgeService multiBridgeService,
        IBridgeDiscoveryService discoveryService)
    {
        _settingsService = settingsService;
        _multiBridgeService = multiBridgeService;

        // Create bridge management view model
        BridgeManagement = new BridgeManagementViewModel(multiBridgeService, discoveryService);

        // Subscribe to connection changes
        _multiBridgeService.BridgeConnectionChanged += OnBridgeConnectionChanged;
    }

    public void LoadSettings()
    {
        SelectedTheme = _settingsService.Settings.Theme;

        UpdateBridgeCounts();

        // Migrate legacy single bridge to multi-bridge
        #pragma warning disable CS0618 // Type or member is obsolete
        if (_settingsService.Settings.ConfiguredBridge != null &&
            _settingsService.Settings.ConfiguredBridges.Count == 0)
        {
            _settingsService.Settings.ConfiguredBridges.Add(_settingsService.Settings.ConfiguredBridge);
            _settingsService.Settings.ConfiguredBridge = null;
            _ = _settingsService.SaveAsync();
        }
        #pragma warning restore CS0618

        // Get app version from assembly
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        if (version != null)
        {
            AppVersion = $"{version.Major}.{version.Minor}.{version.Build}";
        }

        // Reload bridge management
        BridgeManagement?.LoadBridges();
    }

    partial void OnSelectedThemeChanged(AppTheme value)
    {
        _settingsService.Settings.Theme = value;
        _ = _settingsService.SaveAsync();
    }

    [RelayCommand]
    private void ManageBridges()
    {
        // This would navigate to bridge management page or open dialog
        BridgeSetupRequested?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateBridgeCounts()
    {
        BridgeCount = _multiBridgeService.ConfiguredBridges.Count;
        ConnectedBridgeCount = _multiBridgeService.ConnectionStatus.Count(kvp => kvp.Value);
    }

    private void OnBridgeConnectionChanged(object? sender, BridgeConnectionEventArgs e)
    {
        UpdateBridgeCounts();
    }
}
