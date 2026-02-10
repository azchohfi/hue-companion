using System.Globalization;
using System.Text.RegularExpressions;
using HueWindows.Core.Models;

namespace HueWindows.Mcp.Services;

/// <summary>
/// Parses color strings (hex, RGB, named colors) into HueColor (CIE xy).
/// </summary>
public static partial class ColorParser
{
    private static readonly Dictionary<string, (byte R, byte G, byte B)> NamedColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["red"] = (255, 0, 0),
        ["green"] = (0, 255, 0),
        ["blue"] = (0, 0, 255),
        ["yellow"] = (255, 255, 0),
        ["orange"] = (255, 165, 0),
        ["purple"] = (128, 0, 128),
        ["magenta"] = (255, 0, 255),
        ["pink"] = (255, 192, 203),
        ["cyan"] = (0, 255, 255),
        ["white"] = (255, 255, 255),
        ["warm white"] = (255, 244, 229),
        ["cool white"] = (200, 210, 255),
        ["soft white"] = (255, 240, 220),
        ["warm orange"] = (255, 140, 0),
        ["turquoise"] = (64, 224, 208),
        ["teal"] = (0, 128, 128),
        ["coral"] = (255, 127, 80),
        ["salmon"] = (250, 128, 114),
        ["lavender"] = (230, 230, 250),
        ["indigo"] = (75, 0, 130),
        ["violet"] = (238, 130, 238),
        ["lime"] = (0, 255, 0),
        ["gold"] = (255, 215, 0),
        ["crimson"] = (220, 20, 60),
        ["navy"] = (0, 0, 128),
        ["olive"] = (128, 128, 0),
        ["maroon"] = (128, 0, 0),
        ["aqua"] = (0, 255, 255),
        ["peach"] = (255, 218, 185),
        ["mint"] = (152, 255, 152),
        ["rose"] = (255, 0, 127),
    };

    [GeneratedRegex(@"^#?([0-9a-fA-F]{6})$")]
    private static partial Regex HexRegex();

    [GeneratedRegex(@"^rgb\(\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})\s*\)$", RegexOptions.IgnoreCase)]
    private static partial Regex RgbRegex();

    /// <summary>
    /// Parse a color string into a HueColor.
    /// Supports: hex (#FF0000), RGB (rgb(255,0,0)), and named colors (red, warm white, etc.)
    /// </summary>
    public static HueColor? Parse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        input = input.Trim();

        // Try hex
        var hexMatch = HexRegex().Match(input);
        if (hexMatch.Success)
        {
            var hex = hexMatch.Groups[1].Value;
            byte r = byte.Parse(hex[..2], NumberStyles.HexNumber);
            byte g = byte.Parse(hex[2..4], NumberStyles.HexNumber);
            byte b = byte.Parse(hex[4..6], NumberStyles.HexNumber);
            return HueColor.FromRgb(r, g, b);
        }

        // Try rgb()
        var rgbMatch = RgbRegex().Match(input);
        if (rgbMatch.Success)
        {
            // Clamp to 0-255 to prevent overflow from out-of-range values
            byte r = (byte)Math.Clamp(int.Parse(rgbMatch.Groups[1].Value), 0, 255);
            byte g = (byte)Math.Clamp(int.Parse(rgbMatch.Groups[2].Value), 0, 255);
            byte b = (byte)Math.Clamp(int.Parse(rgbMatch.Groups[3].Value), 0, 255);
            return HueColor.FromRgb(r, g, b);
        }

        // Try named color
        if (NamedColors.TryGetValue(input, out var named))
        {
            return HueColor.FromRgb(named.R, named.G, named.B);
        }

        return null;
    }

    /// <summary>
    /// Convert a HueColor to hex string for display.
    /// </summary>
    public static string ToHex(HueColor color, double brightness = 1.0)
    {
        var (r, g, b) = color.ToRgb(brightness);
        return $"#{r:X2}{g:X2}{b:X2}";
    }
}
