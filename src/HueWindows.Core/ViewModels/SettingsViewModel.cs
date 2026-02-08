using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HueWindows.Core.Models;
using HueWindows.Core.Services.Interfaces;
using HueWindows.Core.Utilities;

namespace HueWindows.Core.ViewModels;

/// <summary>
/// ViewModel for the settings page.
/// </summary>
public partial class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly ISettingsService _settingsService;
    private readonly IMultiBridgeService _multiBridgeService;
    private readonly IHotkeyService? _hotkeyService;

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

    // Hotkey settings
    [ObservableProperty]
    private bool _isHotkeyEnabled;

    [ObservableProperty]
    private bool _hasWinModifier;

    [ObservableProperty]
    private bool _hasCtrlModifier;

    [ObservableProperty]
    private bool _hasAltModifier;

    [ObservableProperty]
    private bool _hasShiftModifier;

    [ObservableProperty]
    private VirtualKey _selectedKey = VirtualKey.H;

    [ObservableProperty]
    private string _hotkeyDisplayString = string.Empty;

    [ObservableProperty]
    private string _hotkeyStatusMessage = string.Empty;

    [ObservableProperty]
    private bool _isHotkeyError;

    [ObservableProperty]
    private bool _minimizeToTray;

    [ObservableProperty]
    private bool _startMinimized;

    /// <summary>
    /// Event raised when user wants to navigate to bridge setup.
    /// </summary>
    public event EventHandler? BridgeSetupRequested;

    /// <summary>
    /// Event raised when user wants to repair bridge connection.
    /// </summary>
    public event EventHandler? RepairBridgeRequested;

    /// <summary>
    /// Event raised when hotkey settings change and need to be re-registered.
    /// </summary>
    public event EventHandler<HotkeySettings>? HotkeySettingsChanged;

    public SettingsViewModel(
        ISettingsService settingsService,
        IMultiBridgeService multiBridgeService,
        IBridgeDiscoveryService discoveryService,
        IHotkeyService? hotkeyService = null)
    {
        _settingsService = settingsService;
        _multiBridgeService = multiBridgeService;
        _hotkeyService = hotkeyService;

        // Create bridge management view model
        BridgeManagement = new BridgeManagementViewModel(multiBridgeService, discoveryService);

        // Subscribe to connection changes
        _multiBridgeService.BridgeConnectionChanged += OnBridgeConnectionChanged;
    }

    public void LoadSettings()
    {
        SelectedTheme = _settingsService.Settings.Theme;

        UpdateBridgeCounts();

        // Load hotkey settings
        var hotkey = _settingsService.Settings.Hotkey;
        IsHotkeyEnabled = hotkey.IsEnabled;
        HasWinModifier = hotkey.Modifiers.HasFlag(HotkeyModifiers.Win);
        HasCtrlModifier = hotkey.Modifiers.HasFlag(HotkeyModifiers.Ctrl);
        HasAltModifier = hotkey.Modifiers.HasFlag(HotkeyModifiers.Alt);
        HasShiftModifier = hotkey.Modifiers.HasFlag(HotkeyModifiers.Shift);
        SelectedKey = hotkey.Key;
        UpdateHotkeyDisplay();

        // Load tray settings
        MinimizeToTray = _settingsService.Settings.MinimizeToTray;
        StartMinimized = _settingsService.Settings.StartMinimized;

        // Update status from service
        UpdateHotkeyStatus();

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

    private void UpdateHotkeyDisplay()
    {
        var settings = BuildCurrentHotkeySettings();
        HotkeyDisplayString = settings.DisplayString;
    }

    private void UpdateHotkeyStatus()
    {
        if (_hotkeyService == null)
        {
            HotkeyStatusMessage = "Hotkey service not available";
            IsHotkeyError = true;
            return;
        }

        if (!IsHotkeyEnabled)
        {
            HotkeyStatusMessage = "Hotkey disabled";
            IsHotkeyError = false;
            return;
        }

        if (_hotkeyService.IsRegistered)
        {
            HotkeyStatusMessage = "Hotkey active";
            IsHotkeyError = false;
        }
        else if (_hotkeyService.LastError != null)
        {
            HotkeyStatusMessage = _hotkeyService.LastError;
            IsHotkeyError = true;
        }
        else
        {
            HotkeyStatusMessage = "Hotkey not registered";
            IsHotkeyError = true;
        }
    }

    private HotkeySettings BuildCurrentHotkeySettings()
    {
        var modifiers = HotkeyModifiers.None;
        if (HasWinModifier) modifiers |= HotkeyModifiers.Win;
        if (HasCtrlModifier) modifiers |= HotkeyModifiers.Ctrl;
        if (HasAltModifier) modifiers |= HotkeyModifiers.Alt;
        if (HasShiftModifier) modifiers |= HotkeyModifiers.Shift;

        return new HotkeySettings
        {
            IsEnabled = IsHotkeyEnabled,
            Modifiers = modifiers,
            Key = SelectedKey
        };
    }

    /// <summary>
    /// Validates that the hotkey has at least one modifier selected.
    /// </summary>
    public bool ValidateHotkeyModifiers()
    {
        return HasWinModifier || HasCtrlModifier || HasAltModifier || HasShiftModifier;
    }

    partial void OnSelectedThemeChanged(AppTheme value)
    {
        _settingsService.Settings.Theme = value;
        _settingsService.SaveAsync().FireAndForget();
    }

    partial void OnIsHotkeyEnabledChanged(bool value)
    {
        SaveAndApplyHotkeySettings();
    }

    partial void OnHasWinModifierChanged(bool value)
    {
        UpdateHotkeyDisplay();
        SaveAndApplyHotkeySettings();
    }

    partial void OnHasCtrlModifierChanged(bool value)
    {
        UpdateHotkeyDisplay();
        SaveAndApplyHotkeySettings();
    }

    partial void OnHasAltModifierChanged(bool value)
    {
        UpdateHotkeyDisplay();
        SaveAndApplyHotkeySettings();
    }

    partial void OnHasShiftModifierChanged(bool value)
    {
        UpdateHotkeyDisplay();
        SaveAndApplyHotkeySettings();
    }

    partial void OnSelectedKeyChanged(VirtualKey value)
    {
        UpdateHotkeyDisplay();
        SaveAndApplyHotkeySettings();
    }

    partial void OnMinimizeToTrayChanged(bool value)
    {
        _settingsService.Settings.MinimizeToTray = value;
        _settingsService.SaveAsync().FireAndForget();
    }

    partial void OnStartMinimizedChanged(bool value)
    {
        _settingsService.Settings.StartMinimized = value;
        _settingsService.SaveAsync().FireAndForget();
    }

    private void SaveAndApplyHotkeySettings()
    {
        // Validate modifiers - if none selected and hotkey is enabled, show error
        if (IsHotkeyEnabled && !ValidateHotkeyModifiers())
        {
            HotkeyStatusMessage = "At least one modifier key required";
            IsHotkeyError = true;
            return;
        }

        var settings = BuildCurrentHotkeySettings();
        _settingsService.Settings.Hotkey = settings;
        _settingsService.SaveAsync().FireAndForget();

        HotkeySettingsChanged?.Invoke(this, settings);
        UpdateHotkeyStatus();
    }

    /// <summary>
    /// Tests if the current hotkey combination is available.
    /// </summary>
    public bool TestHotkeyAvailability()
    {
        if (_hotkeyService == null)
            return false;

        var settings = BuildCurrentHotkeySettings();
        return _hotkeyService.IsHotkeyAvailable(settings);
    }

    /// <summary>
    /// Gets available virtual keys for the hotkey combo box.
    /// </summary>
    public static IReadOnlyList<VirtualKey> AvailableKeys { get; } = new[]
    {
        // Letters
        VirtualKey.A, VirtualKey.B, VirtualKey.C, VirtualKey.D, VirtualKey.E,
        VirtualKey.F, VirtualKey.G, VirtualKey.H, VirtualKey.I, VirtualKey.J,
        VirtualKey.K, VirtualKey.L, VirtualKey.M, VirtualKey.N, VirtualKey.O,
        VirtualKey.P, VirtualKey.Q, VirtualKey.R, VirtualKey.S, VirtualKey.T,
        VirtualKey.U, VirtualKey.V, VirtualKey.W, VirtualKey.X, VirtualKey.Y,
        VirtualKey.Z,
        // Function keys
        VirtualKey.F1, VirtualKey.F2, VirtualKey.F3, VirtualKey.F4,
        VirtualKey.F5, VirtualKey.F6, VirtualKey.F7, VirtualKey.F8,
        VirtualKey.F9, VirtualKey.F10, VirtualKey.F11, VirtualKey.F12,
        // Numbers
        VirtualKey.D0, VirtualKey.D1, VirtualKey.D2, VirtualKey.D3, VirtualKey.D4,
        VirtualKey.D5, VirtualKey.D6, VirtualKey.D7, VirtualKey.D8, VirtualKey.D9
    };

    /// <summary>
    /// Gets a display-friendly name for a virtual key.
    /// </summary>
    public static string GetKeyDisplayName(VirtualKey key)
    {
        return key switch
        {
            VirtualKey.D0 => "0",
            VirtualKey.D1 => "1",
            VirtualKey.D2 => "2",
            VirtualKey.D3 => "3",
            VirtualKey.D4 => "4",
            VirtualKey.D5 => "5",
            VirtualKey.D6 => "6",
            VirtualKey.D7 => "7",
            VirtualKey.D8 => "8",
            VirtualKey.D9 => "9",
            _ => key.ToString()
        };
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

    public void Dispose()
    {
        _multiBridgeService.BridgeConnectionChanged -= OnBridgeConnectionChanged;
        (BridgeManagement as IDisposable)?.Dispose();
    }
}
