using HueWindows.Core.Models;

namespace HueWindows.Core.Utilities;

/// <summary>
/// Provides a single source of truth for mapping room archetypes to icon glyphs.
/// </summary>
public static class RoomIconHelper
{
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
}
