using HueWindows.Core.Models;

namespace HueWindows.Core.Services.Interfaces;

/// <summary>
/// Service for managing and communicating with multiple Hue bridges.
/// </summary>
public interface IMultiBridgeService
{
    /// <summary>
    /// Gets all configured bridges.
    /// </summary>
    IReadOnlyList<BridgeModel> ConfiguredBridges { get; }

    /// <summary>
    /// Gets connection status for each bridge.
    /// </summary>
    IReadOnlyDictionary<string, bool> ConnectionStatus { get; }

    /// <summary>
    /// Event raised when a bridge connection status changes.
    /// </summary>
    event EventHandler<BridgeConnectionEventArgs>? BridgeConnectionChanged;

    /// <summary>
    /// Event raised when a light's state changes on any bridge.
    /// </summary>
    event EventHandler<MultiBridgeLightStateChangedEventArgs>? LightStateChanged;

    /// <summary>
    /// Adds a new bridge to the configuration.
    /// </summary>
    Task<Result> AddBridgeAsync(BridgeModel bridge);

    /// <summary>
    /// Removes a bridge from the configuration.
    /// </summary>
    Task<Result> RemoveBridgeAsync(string bridgeId);

    /// <summary>
    /// Updates a bridge's friendly name.
    /// </summary>
    Task<Result> UpdateBridgeNameAsync(string bridgeId, string friendlyName);

    /// <summary>
    /// Connects to all configured bridges.
    /// </summary>
    Task ConnectAllAsync();

    /// <summary>
    /// Connects to a specific bridge.
    /// </summary>
    Task<Result> ConnectBridgeAsync(string bridgeId);

    /// <summary>
    /// Disconnects from a specific bridge.
    /// </summary>
    void DisconnectBridge(string bridgeId);

    /// <summary>
    /// Disconnects from all bridges.
    /// </summary>
    void DisconnectAll();

    /// <summary>
    /// Gets all rooms from all connected bridges.
    /// </summary>
    Task<Result<IReadOnlyList<RoomModel>>> GetAllRoomsAsync();

    /// <summary>
    /// Gets all zones from all connected bridges.
    /// </summary>
    Task<Result<IReadOnlyList<RoomModel>>> GetAllZonesAsync();

    /// <summary>
    /// Gets rooms from a specific bridge.
    /// </summary>
    Task<Result<IReadOnlyList<RoomModel>>> GetRoomsForBridgeAsync(string bridgeId);

    /// <summary>
    /// Gets zones from a specific bridge.
    /// </summary>
    Task<Result<IReadOnlyList<RoomModel>>> GetZonesForBridgeAsync(string bridgeId);

    /// <summary>
    /// Gets the bridge service for a specific bridge ID.
    /// </summary>
    IHueBridgeService? GetBridgeService(string bridgeId);

    /// <summary>
    /// Gets the default bridge service (first connected bridge) for backward compatibility.
    /// </summary>
    IHueBridgeService? GetDefaultBridgeService();
}

/// <summary>
/// Event arguments for bridge connection status changes.
/// </summary>
public class BridgeConnectionEventArgs : EventArgs
{
    public string BridgeId { get; init; } = string.Empty;
    public bool IsConnected { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Event arguments for light state changes with bridge context.
/// </summary>
public class MultiBridgeLightStateChangedEventArgs : LightStateChangedEventArgs
{
    public string BridgeId { get; init; } = string.Empty;
}
