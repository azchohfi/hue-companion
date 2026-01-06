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
    /// The display name of the room/zone.
    /// </summary>
    public string Name { get; set; } = string.Empty;

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
