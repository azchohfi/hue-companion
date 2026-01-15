using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.UI;

namespace HueWindows.Utilities;

/// <summary>
/// Factory methods for creating common brush types.
/// </summary>
public static class BrushFactory
{
    /// <summary>
    /// Creates a LinearGradientBrush from a list of RGB color tuples.
    /// </summary>
    /// <param name="colors">List of RGB color tuples.</param>
    /// <param name="startPoint">Gradient start point (default: top-left 0,0).</param>
    /// <param name="endPoint">Gradient end point (default: bottom-right 1,1).</param>
    /// <returns>A LinearGradientBrush with evenly distributed color stops.</returns>
    public static LinearGradientBrush CreateGradient(
        IReadOnlyList<(byte r, byte g, byte b)> colors,
        Point? startPoint = null,
        Point? endPoint = null)
    {
        var gradient = new LinearGradientBrush
        {
            StartPoint = startPoint ?? new Point(0, 0),
            EndPoint = endPoint ?? new Point(1, 1)
        };

        if (colors.Count == 0)
        {
            // Fallback to gray
            gradient.GradientStops.Add(new GradientStop { Color = Colors.Gray, Offset = 0 });
            gradient.GradientStops.Add(new GradientStop { Color = Colors.Gray, Offset = 1 });
        }
        else if (colors.Count == 1)
        {
            var (r, g, b) = colors[0];
            var color = Color.FromArgb(255, r, g, b);
            gradient.GradientStops.Add(new GradientStop { Color = color, Offset = 0 });
            gradient.GradientStops.Add(new GradientStop { Color = color, Offset = 1 });
        }
        else
        {
            for (int i = 0; i < colors.Count; i++)
            {
                var (r, g, b) = colors[i];
                var color = Color.FromArgb(255, r, g, b);
                gradient.GradientStops.Add(new GradientStop
                {
                    Color = color,
                    Offset = (double)i / (colors.Count - 1)
                });
            }
        }

        return gradient;
    }

    /// <summary>
    /// Creates a horizontal LinearGradientBrush (left to right).
    /// </summary>
    public static LinearGradientBrush CreateHorizontalGradient(IReadOnlyList<(byte r, byte g, byte b)> colors)
    {
        return CreateGradient(colors, new Point(0, 0), new Point(1, 0));
    }

    /// <summary>
    /// Creates a diagonal LinearGradientBrush (top-left to bottom-right).
    /// </summary>
    public static LinearGradientBrush CreateDiagonalGradient(IReadOnlyList<(byte r, byte g, byte b)> colors)
    {
        return CreateGradient(colors, new Point(0, 0), new Point(1, 1));
    }

    /// <summary>
    /// Creates a solid color brush from an RGB tuple.
    /// </summary>
    public static SolidColorBrush CreateSolid((byte r, byte g, byte b) color)
    {
        return new SolidColorBrush(Color.FromArgb(255, color.r, color.g, color.b));
    }

    /// <summary>
    /// Creates a solid color brush from a Color.
    /// </summary>
    public static SolidColorBrush CreateSolid(Color color)
    {
        return new SolidColorBrush(color);
    }
}
