namespace HueWindows.Core.Models;

/// <summary>
/// Represents an animated scene with one or more animation definitions.
/// </summary>
public class AnimatedSceneModel
{
    /// <summary>
    /// Unique identifier for the scene.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the scene.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Short description of the scene.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Category for filtering/grouping (e.g., "Nature", "Holiday", "Ambient").
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Preview color for UI display.
    /// </summary>
    public HueColor? PreviewColor { get; set; }

    /// <summary>
    /// Preview palette colors (up to 4 colors).
    /// </summary>
    public List<HueColor> PaletteColors { get; set; } = new();

    /// <summary>
    /// List of animation definitions for this scene.
    /// </summary>
    public List<AnimationDefinition> Animations { get; set; } = new();

    /// <summary>
    /// Default target for animations (room, zone, or specific lights).
    /// </summary>
    public LightTargeting DefaultTargeting { get; set; }

    /// <summary>
    /// Target ID (room/zone GUID) if using room or zone targeting.
    /// </summary>
    public Guid? TargetId { get; set; }

    /// <summary>
    /// Specific light IDs if using individual light targeting.
    /// </summary>
    public List<Guid> TargetLights { get; set; } = new();

    /// <summary>
    /// Author or source of the scene.
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// Version of the scene definition.
    /// </summary>
    public string Version { get; set; } = "1.0";

    /// <summary>
    /// Whether this scene is a built-in scene.
    /// </summary>
    public bool IsBuiltIn { get; set; }
}

/// <summary>
/// How lights should be targeted for the animation.
/// </summary>
public enum LightTargeting
{
    /// <summary>
    /// Target all lights in a specific room.
    /// </summary>
    Room,

    /// <summary>
    /// Target all lights in a specific zone.
    /// </summary>
    Zone,

    /// <summary>
    /// Target specific individual lights.
    /// </summary>
    Lights
}
