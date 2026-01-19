namespace HueWindows.Core.Models;

/// <summary>
/// Distinguishes between rooms and zones in the Hue system.
/// </summary>
public enum LightGroupType
{
    Room,
    Zone
}

/// <summary>
/// Represents a room/zone containing Hue lights.
/// </summary>
public class RoomModel
{
    /// <summary>
    /// The unique identifier of the room/zone.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The type of light group (Room or Zone).
    /// </summary>
    public LightGroupType GroupType { get; set; } = LightGroupType.Room;

    /// <summary>
    /// The original name of the room/zone from the Hue bridge.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Whether to show the bridge name prefix in DisplayName (for multi-bridge duplicate handling).
    /// </summary>
    public bool ShowBridgePrefix { get; set; }

    /// <summary>
    /// The display name for UI, including bridge prefix if ShowBridgePrefix is true.
    /// </summary>
    public string DisplayName => ShowBridgePrefix && !string.IsNullOrEmpty(BridgeName)
        ? $"{BridgeName} - {Name}"
        : Name;

    /// <summary>
    /// The bridge ID this room belongs to (for multi-bridge support).
    /// </summary>
    public string? BridgeId { get; set; }

    /// <summary>
    /// The display name of the bridge this room belongs to (for UI).
    /// </summary>
    public string? BridgeName { get; set; }

    /// <summary>
    /// The room archetype (e.g., living room, bedroom, kitchen).
    /// </summary>
    public RoomArchetype Archetype { get; set; }

    /// <summary>
    /// Whether any light in the room is currently on.
    /// </summary>
    public bool IsOn { get; set; }

    /// <summary>
    /// The average brightness of lights in the room (0.0 to 1.0).
    /// </summary>
    public double Brightness { get; set; }

    /// <summary>
    /// The dominant color of the room, if available.
    /// </summary>
    public HueColor? DominantColor { get; set; }

    /// <summary>
    /// The lights in this room.
    /// </summary>
    public List<LightModel> Lights { get; set; } = new();

    /// <summary>
    /// The scenes available for this room.
    /// </summary>
    public List<SceneModel> Scenes { get; set; } = new();
}

/// <summary>
/// Room archetype categories.
/// </summary>
public enum RoomArchetype
{
    LivingRoom,
    Kitchen,
    Dining,
    Bedroom,
    KidsBedroom,
    Bathroom,
    Nursery,
    Recreation,
    Office,
    Gym,
    Hallway,
    Toilet,
    FrontDoor,
    Garage,
    Terrace,
    Garden,
    Driveway,
    Carport,
    Home,
    Downstairs,
    Upstairs,
    TopFloor,
    Attic,
    GuestRoom,
    Staircase,
    Lounge,
    ManCave,
    Computer,
    Studio,
    Music,
    TV,
    Reading,
    Closet,
    Storage,
    LaundryRoom,
    Balcony,
    Porch,
    Barbecue,
    Pool,
    Other
}
