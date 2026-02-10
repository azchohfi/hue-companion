namespace HueCompanion.Core.Services.Interfaces;

/// <summary>
/// Service for discovering and registering with Hue bridges on the local network.
/// </summary>
public interface IBridgeDiscoveryService
{
    /// <summary>
    /// Discovers Hue bridges on the local network.
    /// </summary>
    /// <param name="timeout">Maximum time to wait for discovery.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of discovered bridges.</returns>
    Task<IReadOnlyList<DiscoveredBridge>> DiscoverBridgesAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers the application with a bridge.
    /// User must press the link button on the bridge before calling this.
    /// </summary>
    /// <param name="ipAddress">The IP address of the bridge.</param>
    /// <param name="appName">The application name to register.</param>
    /// <param name="deviceName">The device name to register.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Registration result with app key if successful.</returns>
    Task<BridgeRegistrationResult> RegisterAsync(
        string ipAddress,
        string appName,
        string deviceName,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a discovered Hue bridge on the network.
/// </summary>
/// <param name="BridgeId">The unique identifier of the bridge.</param>
/// <param name="IpAddress">The local IP address of the bridge.</param>
public record DiscoveredBridge(string BridgeId, string IpAddress);

/// <summary>
/// Result of a bridge registration attempt.
/// </summary>
/// <param name="Success">Whether registration was successful.</param>
/// <param name="AppKey">The application key if successful.</param>
/// <param name="Error">Error message if unsuccessful.</param>
public record BridgeRegistrationResult(bool Success, string? AppKey, string? Error);
