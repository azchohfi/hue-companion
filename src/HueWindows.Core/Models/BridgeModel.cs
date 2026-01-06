namespace HueWindows.Core.Models;

/// <summary>
/// Represents a configured Hue bridge with connection credentials.
/// </summary>
public class BridgeModel
{
    /// <summary>
    /// The unique identifier of the bridge.
    /// </summary>
    public string BridgeId { get; set; } = string.Empty;

    /// <summary>
    /// The IP address of the bridge on the local network.
    /// </summary>
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>
    /// The application key used to authenticate with the bridge.
    /// </summary>
    public string AppKey { get; set; } = string.Empty;

    /// <summary>
    /// The friendly name of the bridge.
    /// </summary>
    public string? FriendlyName { get; set; }

    /// <summary>
    /// The last time the app successfully connected to this bridge.
    /// </summary>
    public DateTime LastConnected { get; set; }
}
