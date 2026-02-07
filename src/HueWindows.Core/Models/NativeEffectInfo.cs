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
        new() { Id = "fire", Name = "Fire", Description = "Warm flickering flames", IconGlyph = "\uE9A8" },       // Fireplace
        new() { Id = "candle", Name = "Candle", Description = "Soft candle flicker", IconGlyph = "\uEA14" },      // Candle/Lightbulb
        new() { Id = "sparkle", Name = "Sparkle", Description = "Twinkling sparkle", IconGlyph = "\uE7B1" },      // Sparkle
        new() { Id = "glisten", Name = "Glisten", Description = "Gentle shimmer", IconGlyph = "\uE2B1" },         // Shimmer/Diamond
        new() { Id = "opal", Name = "Opal", Description = "Soft opalescent flow", IconGlyph = "\uF259" },         // ColorSolid
        new() { Id = "prism", Name = "Prism", Description = "Color-shifting prism", IconGlyph = "\uE790" },       // Color
        new() { Id = "underwater", Name = "Underwater", Description = "Blue-green underwater", IconGlyph = "\uE81D" }, // Water/Drop
        new() { Id = "cosmos", Name = "Cosmos", Description = "Space galaxy drift", IconGlyph = "\uE7C4" },       // Globe
        new() { Id = "sunbeam", Name = "Sunbeam", Description = "Warm sunlight rays", IconGlyph = "\uE706" },     // Brightness/Sun
        new() { Id = "enchant", Name = "Enchant", Description = "Magical transitions", IconGlyph = "\uE945" },    // Wand/Magic
    };
}
