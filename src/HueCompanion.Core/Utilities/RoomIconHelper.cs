using HueCompanion.Core.Models;

namespace HueCompanion.Core.Utilities;

/// <summary>
/// An icon choice for the icon picker.
/// </summary>
public record IconItem(string Glyph, string Name);

/// <summary>
/// A room archetype choice for the archetype picker.
/// Record properties are bindable in DataTemplates (unlike named value tuples).
/// </summary>
public record ArchetypeItem(RoomArchetype Archetype, string DisplayName, string IconGlyph);

/// <summary>
/// Provides a single source of truth for mapping room archetypes to icon glyphs.
/// </summary>
public static class RoomIconHelper
{
    /// <summary>
    /// Gets the icon for a room, checking custom overrides first, then falling back to archetype.
    /// </summary>
    public static string GetIconForRoom(Guid roomId, RoomArchetype archetype, Dictionary<string, string>? customIcons)
    {
        if (customIcons != null && customIcons.TryGetValue(roomId.ToString(), out var custom))
            return custom;
        return GetIconForArchetype(archetype);
    }

    /// <summary>
    /// Gets the Segoe Fluent Icons glyph for the given room archetype.
    /// </summary>
    public static string GetIconForArchetype(RoomArchetype archetype)
    {
        return archetype switch
        {
            RoomArchetype.LivingRoom => "\uE7F4",    // Couch
            RoomArchetype.Lounge => "\uE7F4",        // Couch
            RoomArchetype.Kitchen => "\uED56",       // Kitchen
            RoomArchetype.Dining => "\uE799",        // Dining/eating
            RoomArchetype.Bedroom => "\uEC32",       // Bed
            RoomArchetype.KidsBedroom => "\uEC32",   // Bed
            RoomArchetype.GuestRoom => "\uEC32",     // Bed
            RoomArchetype.Bathroom => "\uE9FC",      // Shower
            RoomArchetype.Toilet => "\uE9FC",        // Shower
            RoomArchetype.Nursery => "\uE734",       // Star (for kids)
            RoomArchetype.Recreation => "\uE7FC",    // Game controller
            RoomArchetype.ManCave => "\uE7FC",       // Game controller
            RoomArchetype.Office => "\uE821",        // Briefcase
            RoomArchetype.Computer => "\uE7F8",      // Computer/PC
            RoomArchetype.Studio => "\uE722",        // Camera
            RoomArchetype.Gym => "\uE805",           // Fitness
            RoomArchetype.Hallway => "\uE8B0",       // Walking/hall
            RoomArchetype.Staircase => "\uE74A",     // Up arrow
            RoomArchetype.FrontDoor => "\uE7AD",     // Door
            RoomArchetype.Garage => "\uE804",        // Car
            RoomArchetype.Carport => "\uE804",       // Car
            RoomArchetype.Driveway => "\uE804",      // Car
            RoomArchetype.Terrace => "\uE8B3",       // Outdoor/sun
            RoomArchetype.Garden => "\uE8E2",        // Leaf/nature
            RoomArchetype.Balcony => "\uE8B3",       // Outdoor/sun
            RoomArchetype.Porch => "\uE8B3",         // Outdoor/sun
            RoomArchetype.Pool => "\uE8A2",          // Water
            RoomArchetype.Barbecue => "\uE8F9",      // Flame
            RoomArchetype.Home => "\uE80F",          // House
            RoomArchetype.Downstairs => "\uE74B",    // Down arrow
            RoomArchetype.Upstairs => "\uE74A",      // Up arrow
            RoomArchetype.TopFloor => "\uE74A",      // Up arrow
            RoomArchetype.Attic => "\uE74A",         // Up arrow
            RoomArchetype.Music => "\uE8D6",         // Music note
            RoomArchetype.TV => "\uE7F4",            // Display/monitor
            RoomArchetype.Reading => "\uE736",       // Book
            RoomArchetype.Closet => "\uE8AF",        // Cabinet
            RoomArchetype.Storage => "\uE8AF",       // Cabinet
            RoomArchetype.LaundryRoom => "\uE8AF",   // Cabinet
            _ => "\uE781"                            // Default light bulb
        };
    }

    /// <summary>
    /// Gets all room archetypes with their display names and icon glyphs.
    /// Used for archetype pickers in create/edit dialogs.
    /// </summary>
    public static IReadOnlyList<ArchetypeItem> GetAllArchetypes() => AllArchetypes;

    /// <summary>
    /// Gets all available icons for the icon picker.
    /// </summary>
    public static IReadOnlyList<IconItem> GetAllIcons() => AllIcons;

    private static readonly IReadOnlyList<IconItem> AllIcons = new List<IconItem>
    {
        // Lighting
        new("\uE781", "Light Bulb"),
        new("\uE706", "Sun"),
        new("\uE8EA", "Moon"),
        new("\uE9A8", "Fireplace"),
        new("\uE8F9", "Flame"),
        new("\uE7B1", "Sparkle"),

        // Rooms
        new("\uE80F", "Home"),
        new("\uE7F4", "Couch"),
        new("\uED56", "Kitchen"),
        new("\uE799", "Dining"),
        new("\uEC32", "Bed"),
        new("\uE9FC", "Shower"),
        new("\uE7AD", "Door"),
        new("\uE8AF", "Cabinet"),

        // Work & Study
        new("\uE821", "Briefcase"),
        new("\uE7F8", "Computer"),
        new("\uE722", "Camera"),
        new("\uE736", "Book"),
        new("\uE70B", "Edit"),
        new("\uE8A1", "Clipboard"),

        // Entertainment
        new("\uE8D6", "Music"),
        new("\uE7FC", "Game"),
        new("\uE768", "Play"),
        new("\uE714", "Video"),
        new("\uE7F5", "Headphone"),
        new("\uE8B8", "Slideshow"),

        // Nature & Outdoors
        new("\uE8B3", "Outdoor"),
        new("\uE8E2", "Leaf"),
        new("\uE8A2", "Water"),
        new("\uE7C4", "Globe"),
        new("\uE9CE", "Cloud"),
        new("\uE916", "Snowflake"),
        new("\uE869", "Bug"),

        // Transport & Places
        new("\uE804", "Car"),
        new("\uE8B0", "Walking"),
        new("\uE805", "Fitness"),
        new("\uE709", "Airplane"),
        new("\uE806", "Map"),

        // Symbols & Shapes
        new("\uE734", "Star"),
        new("\uE735", "Star Filled"),
        new("\uE7C8", "Heart"),
        new("\uE76E", "Diamond"),
        new("\uE91B", "Palette"),
        new("\uE790", "Color"),
        new("\uE945", "Wand"),
        new("\uE77B", "Pin"),

        // Arrows & Navigation
        new("\uE74A", "Up Arrow"),
        new("\uE74B", "Down Arrow"),
        new("\uE72A", "Back"),
        new("\uE72B", "Forward"),

        // People & Social
        new("\uE77B", "Contact"),
        new("\uE716", "People"),
        new("\uE8D4", "Chat"),
        new("\uE715", "Email"),

        // Food & Drink
        new("\uE8A6", "Wine"),
        new("\uEC32", "Dining"),
        new("\uE7A7", "Coffee"),

        // Misc
        new("\uE713", "Settings"),
        new("\uE72E", "Lock"),
        new("\uE7EF", "Shield"),
        new("\uE8B7", "Wrench"),
        new("\uE74C", "Check"),
        new("\uE783", "Stopwatch"),
        new("\uE823", "Gift"),
        new("\uE8D1", "Flag"),
    };

    private static readonly IReadOnlyList<ArchetypeItem> AllArchetypes = new List<ArchetypeItem>
    {
        new(RoomArchetype.LivingRoom, "Living Room", "\uE7F4"),
        new(RoomArchetype.Lounge, "Lounge", "\uE7F4"),
        new(RoomArchetype.Kitchen, "Kitchen", "\uED56"),
        new(RoomArchetype.Dining, "Dining", "\uE799"),
        new(RoomArchetype.Bedroom, "Bedroom", "\uEC32"),
        new(RoomArchetype.KidsBedroom, "Kids Bedroom", "\uEC32"),
        new(RoomArchetype.GuestRoom, "Guest Room", "\uEC32"),
        new(RoomArchetype.Bathroom, "Bathroom", "\uE9FC"),
        new(RoomArchetype.Toilet, "Toilet", "\uE9FC"),
        new(RoomArchetype.Nursery, "Nursery", "\uE734"),
        new(RoomArchetype.Recreation, "Recreation", "\uE7FC"),
        new(RoomArchetype.ManCave, "Man Cave", "\uE7FC"),
        new(RoomArchetype.Office, "Office", "\uE821"),
        new(RoomArchetype.Computer, "Computer", "\uE7F8"),
        new(RoomArchetype.Studio, "Studio", "\uE722"),
        new(RoomArchetype.Gym, "Gym", "\uE805"),
        new(RoomArchetype.Hallway, "Hallway", "\uE8B0"),
        new(RoomArchetype.Staircase, "Staircase", "\uE74A"),
        new(RoomArchetype.FrontDoor, "Front Door", "\uE7AD"),
        new(RoomArchetype.Garage, "Garage", "\uE804"),
        new(RoomArchetype.Carport, "Carport", "\uE804"),
        new(RoomArchetype.Driveway, "Driveway", "\uE804"),
        new(RoomArchetype.Terrace, "Terrace", "\uE8B3"),
        new(RoomArchetype.Garden, "Garden", "\uE8E2"),
        new(RoomArchetype.Balcony, "Balcony", "\uE8B3"),
        new(RoomArchetype.Porch, "Porch", "\uE8B3"),
        new(RoomArchetype.Pool, "Pool", "\uE8A2"),
        new(RoomArchetype.Barbecue, "Barbecue", "\uE8F9"),
        new(RoomArchetype.Home, "Home", "\uE80F"),
        new(RoomArchetype.Downstairs, "Downstairs", "\uE74B"),
        new(RoomArchetype.Upstairs, "Upstairs", "\uE74A"),
        new(RoomArchetype.TopFloor, "Top Floor", "\uE74A"),
        new(RoomArchetype.Attic, "Attic", "\uE74A"),
        new(RoomArchetype.Music, "Music", "\uE8D6"),
        new(RoomArchetype.TV, "TV", "\uE7F4"),
        new(RoomArchetype.Reading, "Reading", "\uE736"),
        new(RoomArchetype.Closet, "Closet", "\uE8AF"),
        new(RoomArchetype.Storage, "Storage", "\uE8AF"),
        new(RoomArchetype.LaundryRoom, "Laundry Room", "\uE8AF"),
        new(RoomArchetype.Other, "Other", "\uE781"),
    };
}
