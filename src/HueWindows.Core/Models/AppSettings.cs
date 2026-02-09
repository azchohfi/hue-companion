namespace HueWindows.Core.Models;

/// <summary>
/// Application-wide settings.
/// </summary>
public class AppSettings
{
    /// <summary>
    /// The configured Hue bridge, if any.
    /// [Deprecated] Use ConfiguredBridges for multi-bridge support.
    /// </summary>
    [Obsolete("Use ConfiguredBridges instead")]
    public BridgeModel? ConfiguredBridge { get; set; }

    /// <summary>
    /// All configured Hue bridges.
    /// </summary>
    public List<BridgeModel> ConfiguredBridges { get; set; } = new();

    /// <summary>
    /// The selected app theme.
    /// </summary>
    public AppTheme Theme { get; set; } = AppTheme.System;

    /// <summary>
    /// Items pinned to the custom dashboard, in display order.
    /// </summary>
    public List<PinnedItem> PinnedDashboardItems { get; set; } = new();

    /// <summary>
    /// Global hotkey settings for show/hide toggle.
    /// </summary>
    public HotkeySettings Hotkey { get; set; } = new();

    /// <summary>
    /// Whether to minimize to system tray instead of taskbar.
    /// </summary>
    public bool MinimizeToTray { get; set; } = true;

    /// <summary>
    /// Whether to start minimized to system tray.
    /// </summary>
    public bool StartMinimized { get; set; } = false;

    /// <summary>
    /// Custom icon overrides per room/zone (room ID string -> glyph string).
    /// </summary>
    public Dictionary<string, string> CustomRoomIcons { get; set; } = new();

    /// <summary>
    /// Custom light ordering per room/zone (group ID string -> list of light ID strings).
    /// </summary>
    public Dictionary<string, List<string>> LightOrders { get; set; } = new();

    /// <summary>
    /// Favorited scenes per room/zone (group ID string -> list of scene ID strings).
    /// </summary>
    public Dictionary<string, List<string>> SceneFavorites { get; set; } = new();

    /// <summary>
    /// Recent scene activations per room/zone (group ID string -> list of activation records).
    /// </summary>
    public Dictionary<string, List<SceneActivation>> SceneRecents { get; set; } = new();
}

/// <summary>
/// Available application themes.
/// </summary>
public enum AppTheme
{
    /// <summary>
    /// Follow system theme.
    /// </summary>
    System,

    /// <summary>
    /// Always use light theme.
    /// </summary>
    Light,

    /// <summary>
    /// Always use dark theme.
    /// </summary>
    Dark
}
