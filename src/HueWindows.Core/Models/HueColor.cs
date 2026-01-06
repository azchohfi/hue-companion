namespace HueWindows.Core.Models;

/// <summary>
/// Represents a color for Hue lights, using CIE xy color space.
/// </summary>
public class HueColor
{
    /// <summary>
    /// X coordinate in CIE color space (0.0 to 1.0).
    /// </summary>
    public double X { get; set; }

    /// <summary>
    /// Y coordinate in CIE color space (0.0 to 1.0).
    /// </summary>
    public double Y { get; set; }

    /// <summary>
    /// Creates a HueColor from CIE xy coordinates.
    /// </summary>
    public HueColor(double x, double y)
    {
        X = Math.Clamp(x, 0.0, 1.0);
        Y = Math.Clamp(y, 0.0, 1.0);
    }

    /// <summary>
    /// Creates a HueColor from RGB values.
    /// </summary>
    public static HueColor FromRgb(byte r, byte g, byte b)
    {
        // Convert RGB to CIE xy
        // Apply gamma correction
        double red = GammaCorrect(r / 255.0);
        double green = GammaCorrect(g / 255.0);
        double blue = GammaCorrect(b / 255.0);

        // Convert to XYZ using Wide RGB D65 conversion
        double X = red * 0.664511 + green * 0.154324 + blue * 0.162028;
        double Y = red * 0.283881 + green * 0.668433 + blue * 0.047685;
        double Z = red * 0.000088 + green * 0.072310 + blue * 0.986039;

        // Calculate xy
        double sum = X + Y + Z;
        if (sum == 0)
        {
            return new HueColor(0.3127, 0.3290); // D65 white point
        }

        return new HueColor(X / sum, Y / sum);
    }

    /// <summary>
    /// Converts this HueColor to RGB.
    /// </summary>
    public (byte R, byte G, byte B) ToRgb(double brightness = 1.0)
    {
        // Calculate XYZ from xy and brightness (Y)
        double z = 1.0 - X - Y;
        double Y_val = brightness;
        double X_val = (Y_val / Y) * X;
        double Z_val = (Y_val / Y) * z;

        // Convert to RGB using Wide RGB D65 conversion
        double r = X_val * 1.656492 - Y_val * 0.354851 - Z_val * 0.255038;
        double g = -X_val * 0.707196 + Y_val * 1.655397 + Z_val * 0.036152;
        double b = X_val * 0.051713 - Y_val * 0.121364 + Z_val * 1.011530;

        // Apply reverse gamma correction
        r = ReverseGammaCorrect(r);
        g = ReverseGammaCorrect(g);
        b = ReverseGammaCorrect(b);

        // Clamp and convert to bytes
        return (
            (byte)Math.Clamp(r * 255, 0, 255),
            (byte)Math.Clamp(g * 255, 0, 255),
            (byte)Math.Clamp(b * 255, 0, 255)
        );
    }

    private static double GammaCorrect(double value)
    {
        return value > 0.04045
            ? Math.Pow((value + 0.055) / 1.055, 2.4)
            : value / 12.92;
    }

    private static double ReverseGammaCorrect(double value)
    {
        return value <= 0.0031308
            ? 12.92 * value
            : 1.055 * Math.Pow(value, 1.0 / 2.4) - 0.055;
    }

    /// <summary>
    /// Creates a default white color.
    /// </summary>
    public static HueColor White => new(0.3127, 0.3290);

    /// <summary>
    /// Creates a warm white color.
    /// </summary>
    public static HueColor WarmWhite => new(0.4596, 0.4105);
}
