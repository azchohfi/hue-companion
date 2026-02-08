namespace HueWindows.Core.Models;

/// <summary>
/// Represents an individual Hue light.
/// </summary>
public class LightModel
{
    /// <summary>
    /// The unique identifier of the light.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The display name of the light.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Whether the light is currently on.
    /// </summary>
    public bool IsOn { get; set; }

    /// <summary>
    /// The current brightness (0.0 to 1.0).
    /// </summary>
    public double Brightness { get; set; }

    /// <summary>
    /// Whether this light supports color.
    /// </summary>
    public bool SupportsColor { get; set; }

    /// <summary>
    /// Whether this light supports color temperature.
    /// </summary>
    public bool SupportsColorTemperature { get; set; }

    /// <summary>
    /// The current color, if the light supports color.
    /// </summary>
    public HueColor? CurrentColor { get; set; }

    /// <summary>
    /// The current color temperature in mirek (153-500), if supported.
    /// </summary>
    public int? ColorTemperature { get; set; }

    /// <summary>
    /// The light archetype (bulb type/appearance).
    /// </summary>
    public LightArchetype Archetype { get; set; }

    /// <summary>
    /// Whether this light is currently reachable.
    /// </summary>
    public bool IsReachable { get; set; } = true;

    /// <summary>
    /// The owner device ID (for rename operations via Device API).
    /// </summary>
    public Guid? DeviceId { get; set; }

    /// <summary>
    /// The product name of the light (e.g., "Hue color lamp").
    /// </summary>
    public string? ProductName { get; set; }

    /// <summary>
    /// The firmware version of the light's device.
    /// </summary>
    public string? FirmwareVersion { get; set; }

    /// <summary>
    /// The model ID of the light.
    /// </summary>
    public string? ModelId { get; set; }

    /// <summary>
    /// The power-on behavior preset for this light.
    /// </summary>
    public PowerOnPreset? PowerOnPreset { get; set; }

    /// <summary>
    /// The custom power-on brightness (0.0 to 1.0), if preset is Custom.
    /// </summary>
    public double? PowerOnBrightness { get; set; }

    /// <summary>
    /// The custom power-on color, if preset is Custom.
    /// </summary>
    public HueColor? PowerOnColor { get; set; }
}

/// <summary>
/// Power-on behavior preset for a light.
/// </summary>
public enum PowerOnPreset
{
    LastOnState,
    Safety,
    PowerFail,
    Custom
}

/// <summary>
/// Light archetype/form factor.
/// </summary>
public enum LightArchetype
{
    UnknownArchetype,
    ClassicBulb,
    SultanBulb,
    FloodBulb,
    SpotBulb,
    CandleBulb,
    LusterBulb,
    PendantRound,
    PendantLong,
    CeilingRound,
    CeilingSquare,
    FloorShade,
    FloorLantern,
    TableShade,
    RecessedCeiling,
    RecessedFloor,
    SingleSpot,
    DoubleSpot,
    TableWash,
    WallLantern,
    WallShade,
    FlexibleLamp,
    GroundSpot,
    WallSpot,
    Plug,
    HueGo,
    HueLightstrip,
    HueIris,
    HueBloom,
    Bollard,
    WallWasher,
    HuePlay,
    VintageBulb,
    ChristmasTree,
    HueCentris,
    HueSigne,
    PendantSpot,
    CeilingHorizontal,
    CeilingTube
}
