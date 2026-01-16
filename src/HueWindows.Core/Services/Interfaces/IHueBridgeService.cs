using HueWindows.Core.Models;

namespace HueWindows.Core.Services.Interfaces;

/// <summary>
/// Service for communicating with a connected Hue bridge.
/// </summary>
public interface IHueBridgeService
{
    /// <summary>
    /// Gets whether the service is connected to a bridge.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Event raised when a light's state changes.
    /// </summary>
    event EventHandler<LightStateChangedEventArgs>? LightStateChanged;

    /// <summary>
    /// Event raised when successfully connected to a bridge.
    /// </summary>
    event EventHandler? Connected;

    /// <summary>
    /// Event raised when disconnected from a bridge.
    /// </summary>
    event EventHandler? Disconnected;

    /// <summary>
    /// Connects to a Hue bridge.
    /// </summary>
    /// <param name="ipAddress">The IP address of the bridge.</param>
    /// <param name="appKey">The application key for authentication.</param>
    /// <returns>Result indicating success or failure with error message.</returns>
    Task<Result> ConnectAsync(string ipAddress, string appKey);

    /// <summary>
    /// Disconnects from the current bridge.
    /// </summary>
    void Disconnect();

    // Room operations

    /// <summary>
    /// Gets all rooms from the bridge.
    /// </summary>
    Task<Result<IReadOnlyList<RoomModel>>> GetRoomsAsync();

    /// <summary>
    /// Gets a specific room by ID.
    /// </summary>
    Task<Result<RoomModel>> GetRoomAsync(Guid roomId);

    /// <summary>
    /// Sets the on/off state for all lights in a room.
    /// </summary>
    Task SetRoomOnAsync(Guid roomId, bool isOn);

    /// <summary>
    /// Sets the brightness for all lights in a room.
    /// </summary>
    /// <param name="roomId">The room ID.</param>
    /// <param name="brightness">Brightness value (0.0 to 1.0).</param>
    Task SetRoomBrightnessAsync(Guid roomId, double brightness);

    /// <summary>
    /// Sets the color for all color-capable lights in a room.
    /// </summary>
    /// <param name="roomId">The room ID.</param>
    /// <param name="color">The color to set.</param>
    Task SetRoomColorAsync(Guid roomId, HueColor color);

    // Zone operations

    /// <summary>
    /// Gets all zones from the bridge.
    /// </summary>
    Task<Result<IReadOnlyList<RoomModel>>> GetZonesAsync();

    /// <summary>
    /// Gets a specific zone by ID.
    /// </summary>
    Task<Result<RoomModel>> GetZoneAsync(Guid zoneId);

    /// <summary>
    /// Sets the on/off state for all lights in a zone.
    /// </summary>
    Task SetZoneOnAsync(Guid zoneId, bool isOn);

    /// <summary>
    /// Sets the brightness for all lights in a zone.
    /// </summary>
    /// <param name="zoneId">The zone ID.</param>
    /// <param name="brightness">Brightness value (0.0 to 1.0).</param>
    Task SetZoneBrightnessAsync(Guid zoneId, double brightness);

    /// <summary>
    /// Sets the color for all color-capable lights in a zone.
    /// </summary>
    /// <param name="zoneId">The zone ID.</param>
    /// <param name="color">The color to set.</param>
    Task SetZoneColorAsync(Guid zoneId, HueColor color);

    /// <summary>
    /// Gets all scenes for a zone.
    /// </summary>
    Task<Result<IReadOnlyList<SceneModel>>> GetScenesForZoneAsync(Guid zoneId);

    // Light operations

    /// <summary>
    /// Gets all lights in a room.
    /// </summary>
    Task<Result<IReadOnlyList<LightModel>>> GetLightsInRoomAsync(Guid roomId);

    /// <summary>
    /// Gets a specific light by ID.
    /// </summary>
    Task<Result<LightModel>> GetLightAsync(Guid lightId);

    /// <summary>
    /// Sets the on/off state for a light.
    /// </summary>
    Task SetLightOnAsync(Guid lightId, bool isOn);

    /// <summary>
    /// Sets the brightness for a light.
    /// </summary>
    /// <param name="lightId">The light ID.</param>
    /// <param name="brightness">Brightness value (0.0 to 1.0).</param>
    Task SetLightBrightnessAsync(Guid lightId, double brightness);

    /// <summary>
    /// Sets the color for a light.
    /// </summary>
    Task SetLightColorAsync(Guid lightId, HueColor color);

    /// <summary>
    /// Sets the color temperature for a light.
    /// </summary>
    /// <param name="lightId">The light ID.</param>
    /// <param name="mirek">Color temperature in mirek (153-500).</param>
    Task SetLightTemperatureAsync(Guid lightId, int mirek);

    /// <summary>
    /// Applies a native Hue effect to a light (fire, candle, etc.).
    /// </summary>
    /// <param name="lightId">The light ID.</param>
    /// <param name="effect">The effect to apply.</param>
    Task ApplyEffectAsync(Guid lightId, string effect);

    // Scene operations

    /// <summary>
    /// Gets all scenes for a room.
    /// </summary>
    Task<Result<IReadOnlyList<SceneModel>>> GetScenesForRoomAsync(Guid roomId);

    /// <summary>
    /// Activates a scene.
    /// </summary>
    Task ActivateSceneAsync(Guid sceneId);

    // Event stream

    /// <summary>
    /// Starts listening for real-time state updates from the bridge.
    /// </summary>
    Task StartEventStreamAsync();

    /// <summary>
    /// Stops listening for real-time state updates.
    /// </summary>
    void StopEventStream();
}

/// <summary>
/// Event arguments for light state changes.
/// </summary>
public class LightStateChangedEventArgs : EventArgs
{
    /// <summary>
    /// The ID of the light that changed.
    /// </summary>
    public Guid LightId { get; init; }

    /// <summary>
    /// The new on/off state, if changed.
    /// </summary>
    public bool? IsOn { get; init; }

    /// <summary>
    /// The new brightness, if changed.
    /// </summary>
    public double? Brightness { get; init; }

    /// <summary>
    /// The new color, if changed.
    /// </summary>
    public HueColor? Color { get; init; }
}
