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
