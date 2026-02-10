namespace HueCompanion.Core.Models;

/// <summary>
/// Represents a color for Hue lights, using CIE xy color space.
/// </summary>
public class HueColor
{
    /// <summary>
    /// X coordinate in CIE color space (0.0 to 1.0).
    /// </summary>
    public double X { get; init; }

    /// <summary>
    /// Y coordinate in CIE color space (0.0 to 1.0).
    /// </summary>
    public double Y { get; init; }

    /// <summary>
    /// Creates a HueColor from CIE xy coordinates.
    /// </summary>
    public HueColor(double x, double y)
    {
        X = Math.Clamp(x, 0.0, 1.0);
        Y = Math.Clamp(y, 0.0, 1.0);
    }

    /// <summary>
    /// Creates a HueColor from RGB values using standard sRGB color space.
    /// </summary>
    public static HueColor FromRgb(byte r, byte g, byte b)
    {
        // Convert sRGB to linear RGB
        double red = SrgbToLinear(r / 255.0);
        double green = SrgbToLinear(g / 255.0);
        double blue = SrgbToLinear(b / 255.0);

        // Convert linear RGB to XYZ using sRGB matrix (D65 illuminant)
        double X = red * 0.4124564 + green * 0.3575761 + blue * 0.1804375;
        double Y = red * 0.2126729 + green * 0.7151522 + blue * 0.0721750;
        double Z = red * 0.0193339 + green * 0.1191920 + blue * 0.9503041;

        // Calculate xy chromaticity coordinates
        double sum = X + Y + Z;
        if (sum == 0)
        {
            return new HueColor(0.3127, 0.3290); // D65 white point
        }

        return new HueColor(X / sum, Y / sum);
    }

    /// <summary>
    /// Converts this HueColor to RGB using standard sRGB color space.
    /// </summary>
    public (byte R, byte G, byte B) ToRgb(double brightness = 1.0)
    {
        // Handle edge case where Y is 0
        if (Y <= 0)
        {
            return (255, 255, 255); // Return white
        }

        // Calculate XYZ from xy and brightness (Y component)
        double z = 1.0 - X - Y;
        double Y_xyz = brightness;
        double X_xyz = (Y_xyz / Y) * X;
        double Z_xyz = (Y_xyz / Y) * z;

        // Convert XYZ to linear sRGB using standard sRGB matrix (D65 illuminant)
        double r = X_xyz * 3.2404542 - Y_xyz * 1.5371385 - Z_xyz * 0.4985314;
        double g = -X_xyz * 0.9692660 + Y_xyz * 1.8760108 + Z_xyz * 0.0415560;
        double b = X_xyz * 0.0556434 - Y_xyz * 0.2040259 + Z_xyz * 1.0572252;

        // Clamp negative values (out of gamut colors)
        r = Math.Max(0, r);
        g = Math.Max(0, g);
        b = Math.Max(0, b);

        // If any channel exceeds 1.0, scale all channels to preserve hue
        double maxComponent = Math.Max(r, Math.Max(g, b));
        if (maxComponent > 1.0)
        {
            r /= maxComponent;
            g /= maxComponent;
            b /= maxComponent;
        }

        // Apply sRGB gamma correction (linear to sRGB)
        r = LinearToSrgb(r);
        g = LinearToSrgb(g);
        b = LinearToSrgb(b);

        // Convert to bytes
        return (
            (byte)Math.Round(r * 255),
            (byte)Math.Round(g * 255),
            (byte)Math.Round(b * 255)
        );
    }

    /// <summary>
    /// Converts linear RGB to sRGB gamma-corrected value.
    /// </summary>
    private static double LinearToSrgb(double linear)
    {
        if (linear <= 0.0031308)
            return 12.92 * linear;
        return 1.055 * Math.Pow(linear, 1.0 / 2.4) - 0.055;
    }

    /// <summary>
    /// Converts sRGB gamma-corrected value to linear RGB.
    /// </summary>
    private static double SrgbToLinear(double srgb)
    {
        if (srgb <= 0.04045)
            return srgb / 12.92;
        return Math.Pow((srgb + 0.055) / 1.055, 2.4);
    }

    /// <summary>
    /// Calculates the relative luminance of this color (0.0 to 1.0).
    /// Used for determining contrast ratios.
    /// </summary>
    public double GetRelativeLuminance(double brightness = 1.0)
    {
        var (r, g, b) = ToRgb(brightness);

        // Convert to sRGB
        double rSrgb = r / 255.0;
        double gSrgb = g / 255.0;
        double bSrgb = b / 255.0;

        // Apply luminance formula (ITU-R BT.709)
        double rLum = rSrgb <= 0.03928 ? rSrgb / 12.92 : Math.Pow((rSrgb + 0.055) / 1.055, 2.4);
        double gLum = gSrgb <= 0.03928 ? gSrgb / 12.92 : Math.Pow((gSrgb + 0.055) / 1.055, 2.4);
        double bLum = bSrgb <= 0.03928 ? bSrgb / 12.92 : Math.Pow((bSrgb + 0.055) / 1.055, 2.4);

        return 0.2126 * rLum + 0.7152 * gLum + 0.0722 * bLum;
    }

    /// <summary>
    /// Determines if black text should be used on this color background for readability.
    /// Returns true if black text is recommended, false if white text is recommended.
    /// </summary>
    public bool ShouldUseBlackText(double brightness = 1.0)
    {
        double luminance = GetRelativeLuminance(brightness);
        // WCAG recommends a contrast ratio of at least 4.5:1
        // Luminance of white is 1.0, black is 0.0
        // If background luminance > 0.179, use black text
        return luminance > 0.179;
    }

    /// <summary>
    /// Creates a default white color.
    /// </summary>
    public static HueColor White => new(0.3127, 0.3290);

    /// <summary>
    /// Creates a warm white color.
    /// </summary>
    public static HueColor WarmWhite => new(0.4596, 0.4105);

    /// <summary>
    /// Creates a HueColor from a color temperature in mirek (153-500).
    /// Uses approximation based on Planckian locus.
    /// </summary>
    public static HueColor FromMirek(int mirek)
    {
        // Mirek to Kelvin: K = 1,000,000 / mirek
        // Hue range: 153 (6500K cool) to 500 (2000K warm)
        mirek = Math.Clamp(mirek, 153, 500);
        double kelvin = 1000000.0 / mirek;

        // Approximate xy coordinates based on color temperature
        // Using simplified Planckian locus approximation
        double x, y;

        if (kelvin <= 4000)
        {
            // Warm (2000K-4000K): orangish to warm white
            double t = (kelvin - 2000) / 2000.0;
            x = 0.5267 - t * 0.1140; // 0.5267 at 2000K, ~0.4127 at 4000K
            y = 0.4133 - t * 0.0133; // 0.4133 at 2000K, ~0.4000 at 4000K
        }
        else
        {
            // Cool (4000K-6500K): neutral to cool white
            double t = (kelvin - 4000) / 2500.0;
            x = 0.4127 - t * 0.1000; // ~0.4127 at 4000K, ~0.3127 at 6500K
            y = 0.4000 - t * 0.0710; // ~0.4000 at 4000K, ~0.3290 at 6500K
        }

        return new HueColor(x, y);
    }
}
