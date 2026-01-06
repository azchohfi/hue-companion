namespace HueWindows.Core.Models;

/// <summary>
/// Application-wide settings.
/// </summary>
public class AppSettings
{
    /// <summary>
    /// The configured Hue bridge, if any.
    /// </summary>
    public BridgeModel? ConfiguredBridge { get; set; }

    /// <summary>
    /// The selected app theme.
    /// </summary>
    public AppTheme Theme { get; set; } = AppTheme.System;
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
