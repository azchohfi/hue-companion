namespace HueWindows.Core.Models;

/// <summary>
/// Information about a native Hue effect.
/// </summary>
public class NativeEffectInfo
{
    /// <summary>
    /// Effect identifier (matches API value).
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Display name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Short description.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Segoe Fluent Icons glyph for display.
    /// </summary>
    public string IconGlyph { get; init; } = "\uE7B1"; // Sparkle

    /// <summary>
    /// Default speed (0.0-1.0).
    /// </summary>
    public double DefaultSpeed { get; init; } = 0.5;

    /// <summary>
    /// Default brightness (0.0-1.0).
    /// </summary>
    public double DefaultBrightness { get; init; } = 1.0;

    /// <summary>
    /// Gets all available native effects.
    /// </summary>
    public static IReadOnlyList<NativeEffectInfo> All { get; } = new List<NativeEffectInfo>
    {
        new() { Id = "fire", Name = "Fire", Description = "Warm flickering flames", IconGlyph = "\uE7B1" },
        new() { Id = "candle", Name = "Candle", Description = "Soft candle flicker", IconGlyph = "\uE7B1" },
        new() { Id = "sparkle", Name = "Sparkle", Description = "Twinkling sparkle", IconGlyph = "\uE7B1" },
        new() { Id = "glisten", Name = "Glisten", Description = "Gentle shimmer", IconGlyph = "\uE7B1" },
        new() { Id = "opal", Name = "Opal", Description = "Soft opalescent flow", IconGlyph = "\uE7B1" },
        new() { Id = "prism", Name = "Prism", Description = "Color-shifting prism", IconGlyph = "\uE7B1" },
        new() { Id = "underwater", Name = "Underwater", Description = "Blue-green underwater", IconGlyph = "\uE7B1" },
        new() { Id = "cosmos", Name = "Cosmos", Description = "Space galaxy drift", IconGlyph = "\uE7B1" },
        new() { Id = "sunbeam", Name = "Sunbeam", Description = "Warm sunlight rays", IconGlyph = "\uE7B1" },
        new() { Id = "enchant", Name = "Enchant", Description = "Magical transitions", IconGlyph = "\uE7B1" },
    };
}
