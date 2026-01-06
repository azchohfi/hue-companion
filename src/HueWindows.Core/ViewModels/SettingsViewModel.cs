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
    private readonly IHueBridgeService _bridgeService;

    [ObservableProperty]
    private AppTheme _selectedTheme;

    [ObservableProperty]
    private string _bridgeIpAddress = string.Empty;

    [ObservableProperty]
    private string _bridgeId = string.Empty;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _appVersion = "1.0.0";

    /// <summary>
    /// Event raised when user wants to re-pair the bridge.
    /// </summary>
    public event EventHandler? RepairBridgeRequested;

    public SettingsViewModel(
        ISettingsService settingsService,
        IHueBridgeService bridgeService)
    {
        _settingsService = settingsService;
        _bridgeService = bridgeService;
    }

    public void LoadSettings()
    {
        SelectedTheme = _settingsService.Settings.Theme;

        var bridge = _settingsService.Settings.ConfiguredBridge;
        if (bridge != null)
        {
            BridgeIpAddress = bridge.IpAddress;
            BridgeId = bridge.BridgeId;
        }

        IsConnected = _bridgeService.IsConnected;

        // Get app version from assembly
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        if (version != null)
        {
            AppVersion = $"{version.Major}.{version.Minor}.{version.Build}";
        }
    }

    partial void OnSelectedThemeChanged(AppTheme value)
    {
        _settingsService.Settings.Theme = value;
        _ = _settingsService.SaveAsync();
    }

    [RelayCommand]
    private void RepairBridge()
    {
        RepairBridgeRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task DisconnectBridgeAsync()
    {
        _bridgeService.Disconnect();

        // Clear saved bridge
        _settingsService.Settings.ConfiguredBridge = null;
        await _settingsService.SaveAsync();

        BridgeIpAddress = string.Empty;
        BridgeId = string.Empty;
        IsConnected = false;

        // Request re-pair
        RepairBridgeRequested?.Invoke(this, EventArgs.Empty);
    }
}
