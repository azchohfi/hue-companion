namespace HueWindows.Helpers;

/// <summary>
/// Represents parsed command-line arguments for deep linking.
/// </summary>
public record CommandLineArgs
{
    /// <summary>
    /// The target page to navigate to (null for default behavior).
    /// </summary>
    public string? Page { get; init; }

    /// <summary>
    /// The ID parameter for pages that require it (room, zone, light).
    /// </summary>
    public Guid? Id { get; init; }

    /// <summary>
    /// Whether screenshot mode is enabled (auto-close after navigation).
    /// </summary>
    public bool ScreenshotMode { get; init; }

    /// <summary>
    /// Delay in milliseconds before auto-close in screenshot mode. Default is 5000ms.
    /// </summary>
    public int ScreenshotDelayMs { get; init; } = 5000;

    /// <summary>
    /// Whether the arguments are valid.
    /// </summary>
    public bool IsValid { get; init; } = true;

    /// <summary>
    /// Error message if arguments are invalid.
    /// </summary>
    public string? ErrorMessage { get; init; }
}
