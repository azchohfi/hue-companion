namespace HueWindows.Core.Models;

/// <summary>
/// Provides color space conversion utilities for Hue XY colors.
/// </summary>
public static class ColorConverter
{
    /// <summary>
    /// Converts Hue XY color to HSV color space.
    /// </summary>
    /// <param name="xy">The XY color coordinates.</param>
    /// <param name="brightness">The brightness value (0.0 to 1.0).</param>
    /// <returns>HSV color as (hue: 0-360, saturation: 0-1, value: 0-1).</returns>
    public static (double Hue, double Saturation, double Value) XyToHsv(HueColor xy, double brightness = 1.0)
    {
        // Convert XY to RGB first
        var (r, g, b) = XyToRgb(xy, brightness);

        // Convert RGB to HSV
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;

        // Calculate hue
        double hue = 0;
        if (delta > 0)
        {
            if (max == r)
                hue = 60 * (((g - b) / delta) % 6);
            else if (max == g)
                hue = 60 * (((b - r) / delta) + 2);
            else
                hue = 60 * (((r - g) / delta) + 4);

            if (hue < 0)
                hue += 360;
        }

        // Calculate saturation
        var saturation = max > 0 ? delta / max : 0;

        // Value is the max component
        var value = max;

        return (hue, saturation, value);
    }

    /// <summary>
    /// Converts HSV color to Hue XY color space.
    /// </summary>
    /// <param name="hue">Hue (0-360 degrees).</param>
    /// <param name="saturation">Saturation (0-1).</param>
    /// <param name="value">Value/brightness (0-1).</param>
    /// <returns>XY color coordinates and brightness.</returns>
    public static (HueColor Xy, double Brightness) HsvToXy(double hue, double saturation, double value)
    {
        // Convert HSV to RGB
        var c = value * saturation;
        var x = c * (1 - Math.Abs((hue / 60) % 2 - 1));
        var m = value - c;

        double r, g, b;
        if (hue < 60)
            (r, g, b) = (c, x, 0);
        else if (hue < 120)
            (r, g, b) = (x, c, 0);
        else if (hue < 180)
            (r, g, b) = (0, c, x);
        else if (hue < 240)
            (r, g, b) = (0, x, c);
        else if (hue < 300)
            (r, g, b) = (x, 0, c);
        else
            (r, g, b) = (c, 0, x);

        r = (r + m);
        g = (g + m);
        b = (b + m);

        // Convert RGB to XY
        return RgbToXy(r, g, b);
    }

    /// <summary>
    /// Converts XY color to RGB.
    /// </summary>
    private static (double R, double G, double B) XyToRgb(HueColor xy, double brightness)
    {
        var x = xy.X;
        var y = xy.Y;
        var z = 1.0 - x - y;

        var Y = brightness;
        var X = (Y / y) * x;
        var Z = (Y / y) * z;

        // Convert XYZ to RGB using sRGB D65 matrix
        var r = X * 3.2406 - Y * 1.5372 - Z * 0.4986;
        var g = -X * 0.9689 + Y * 1.8758 + Z * 0.0415;
        var b = X * 0.0557 - Y * 0.2040 + Z * 1.0570;

        // Apply gamma correction
        r = ApplyGamma(r);
        g = ApplyGamma(g);
        b = ApplyGamma(b);

        // Clamp to [0, 1]
        r = Math.Clamp(r, 0, 1);
        g = Math.Clamp(g, 0, 1);
        b = Math.Clamp(b, 0, 1);

        return (r, g, b);
    }

    /// <summary>
    /// Converts RGB to XY color.
    /// </summary>
    private static (HueColor Xy, double Brightness) RgbToXy(double r, double g, double b)
    {
        // Apply inverse gamma correction
        r = RemoveGamma(r);
        g = RemoveGamma(g);
        b = RemoveGamma(b);

        // Convert RGB to XYZ using sRGB D65 matrix
        var X = r * 0.4124 + g * 0.3576 + b * 0.1805;
        var Y = r * 0.2126 + g * 0.7152 + b * 0.0722;
        var Z = r * 0.0193 + g * 0.1192 + b * 0.9505;

        // Convert XYZ to xy
        var sum = X + Y + Z;
        if (sum == 0)
            return (HueColors.WarmWhite, 0);

        var x = X / sum;
        var y = Y / sum;

        // Clamp to valid gamut
        x = Math.Clamp(x, 0, 1);
        y = Math.Clamp(y, 0, 1);

        return (new HueColor(x, y), Y);
    }

    /// <summary>
    /// Applies gamma correction for sRGB.
    /// </summary>
    private static double ApplyGamma(double value)
    {
        return value <= 0.0031308
            ? 12.92 * value
            : 1.055 * Math.Pow(value, 1.0 / 2.4) - 0.055;
    }

    /// <summary>
    /// Removes gamma correction for sRGB.
    /// </summary>
    private static double RemoveGamma(double value)
    {
        return value <= 0.04045
            ? value / 12.92
            : Math.Pow((value + 0.055) / 1.055, 2.4);
    }

    /// <summary>
    /// Interpolates between two colors in HSV color space to avoid muddy colors.
    /// Brightness is handled separately and should not affect the color interpolation.
    /// </summary>
    /// <param name="start">Starting XY color.</param>
    /// <param name="end">Ending XY color.</param>
    /// <param name="t">Interpolation factor (0.0 to 1.0).</param>
    /// <param name="startBrightness">Starting brightness (not used for color, kept for API compatibility).</param>
    /// <param name="endBrightness">Ending brightness (not used for color, kept for API compatibility).</param>
    /// <returns>Interpolated XY color.</returns>
    public static HueColor InterpolateHsv(HueColor start, HueColor end, double t, double startBrightness = 1.0, double endBrightness = 1.0)
    {
        // Convert to HSV at full brightness to preserve color saturation
        // Brightness is handled separately in the keyframe system
        var (h1, s1, _) = XyToHsv(start, 1.0);
        var (h2, s2, _) = XyToHsv(end, 1.0);

        // Interpolate hue using shortest path around the color wheel
        var hueDiff = h2 - h1;
        if (Math.Abs(hueDiff) > 180)
        {
            if (hueDiff > 0)
                h1 += 360;
            else
                h2 += 360;
        }

        var hue = h1 + (h2 - h1) * t;
        if (hue < 0)
            hue += 360;
        if (hue >= 360)
            hue -= 360;

        // Interpolate saturation
        var saturation = s1 + (s2 - s1) * t;

        // Always use full value (1.0) for color - brightness is separate
        var value = 1.0;

        // Convert back to XY
        var (xy, _) = HsvToXy(hue, saturation, value);
        return xy;
    }
}
