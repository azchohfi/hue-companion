namespace HueWindows.Core.Models;

/// <summary>
/// Represents a Philips Hue color gamut triangle in CIE xy color space.
/// Different Hue light generations support different gamuts (A, B, C).
/// </summary>
public class ColorGamut
{
    /// <summary>Red vertex of the gamut triangle in CIE xy coordinates.</summary>
    public (double X, double Y) Red { get; }

    /// <summary>Green vertex of the gamut triangle in CIE xy coordinates.</summary>
    public (double X, double Y) Green { get; }

    /// <summary>Blue vertex of the gamut triangle in CIE xy coordinates.</summary>
    public (double X, double Y) Blue { get; }

    /// <summary>D65 white point (center of saturation).</summary>
    public static readonly (double X, double Y) WhitePoint = (0.3127, 0.3290);

    /// <summary>
    /// Gamut A: Legacy lights (LivingColors Iris, Bloom, Aura, original LightStrips).
    /// Wider green range, weaker reds.
    /// </summary>
    public static readonly ColorGamut GamutA = new(
        (0.704, 0.296), (0.2151, 0.7106), (0.138, 0.08));

    /// <summary>
    /// Gamut B: Gen 1-2 Hue bulbs (LCT001, LCT002, LCT003).
    /// Good whites, narrower green range.
    /// </summary>
    public static readonly ColorGamut GamutB = new(
        (0.675, 0.322), (0.4091, 0.518), (0.167, 0.04));

    /// <summary>
    /// Gamut C: Current generation (A19 Gen 3+, BR30, Hue Go, LightStrips Plus, Play bars).
    /// Best overall coverage, improved greens and blues.
    /// </summary>
    public static readonly ColorGamut GamutC = new(
        (0.6915, 0.3083), (0.17, 0.7), (0.1532, 0.0475));

    /// <summary>
    /// Creates a ColorGamut from red, green, and blue vertex coordinates.
    /// </summary>
    public ColorGamut((double X, double Y) red, (double X, double Y) green, (double X, double Y) blue)
    {
        Red = red;
        Green = green;
        Blue = blue;
    }

    // Contains(), Clip(), GetMaxSaturationForHue() added in Task 2
}
