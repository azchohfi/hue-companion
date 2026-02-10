namespace HueCompanion.Core.Services.Interfaces;

/// <summary>
/// Service for managing the system tray icon and menu.
/// </summary>
public interface ISystemTrayService : IDisposable
{
    /// <summary>
    /// Event raised when the user requests to show the window.
    /// </summary>
    event EventHandler? ShowWindowRequested;

    /// <summary>
    /// Event raised when the user requests to hide the window.
    /// </summary>
    event EventHandler? HideWindowRequested;

    /// <summary>
    /// Event raised when the user requests to toggle window visibility.
    /// </summary>
    event EventHandler? ToggleWindowRequested;

    /// <summary>
    /// Event raised when the user requests to exit the application.
    /// </summary>
    event EventHandler? ExitRequested;

    /// <summary>
    /// Event raised when the user clicks the tray icon.
    /// </summary>
    event EventHandler? TrayIconClicked;

    /// <summary>
    /// Gets or sets whether the tray icon is visible.
    /// </summary>
    bool IsVisible { get; set; }

    /// <summary>
    /// Initializes the system tray icon.
    /// </summary>
    /// <param name="windowHandle">The main window handle.</param>
    void Initialize(IntPtr windowHandle);

    /// <summary>
    /// Updates the tooltip text on the tray icon.
    /// </summary>
    /// <param name="tooltip">The new tooltip text.</param>
    void SetTooltip(string tooltip);

    /// <summary>
    /// Shows a balloon notification from the tray icon.
    /// </summary>
    /// <param name="title">The notification title.</param>
    /// <param name="message">The notification message.</param>
    /// <param name="icon">The notification icon type.</param>
    void ShowBalloon(string title, string message, TrayBalloonIcon icon = TrayBalloonIcon.Info);

    /// <summary>
    /// Updates the menu item states (e.g., check marks).
    /// </summary>
    /// <param name="isWindowVisible">Whether the window is currently visible.</param>
    void UpdateMenuState(bool isWindowVisible);
}

/// <summary>
/// Icon types for tray balloon notifications.
/// </summary>
public enum TrayBalloonIcon
{
    None,
    Info,
    Warning,
    Error
}
