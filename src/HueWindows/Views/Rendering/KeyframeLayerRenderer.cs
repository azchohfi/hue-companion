using Microsoft.Graphics.Canvas;
using HueWindows.Core.ViewModels;
using System.Collections.Generic;
using System.Numerics;
using Windows.UI;

namespace HueWindows.Views.Rendering;

/// <summary>
/// Renders keyframe circles on the timeline.
/// </summary>
public class KeyframeLayerRenderer
{
    /// <summary>
    /// Draws keyframe circles with colors based on their HueColor values.
    /// </summary>
    /// <param name="ds">Canvas drawing session.</param>
    /// <param name="tracks">Collection of track view models containing keyframes.</param>
    /// <param name="selectedKeyframes">Set of currently selected keyframes for highlight rendering.</param>
    /// <param name="zoomLevel">Current zoom level (pixels per second).</param>
    /// <param name="trackHeight">Height of each track (typically 50 pixels).</param>
    public void Draw(
        CanvasDrawingSession ds,
        IEnumerable<TrackViewModel> tracks,
        IEnumerable<KeyframeViewModel> selectedKeyframes,
        float zoomLevel,
        float trackHeight)
    {
        var selectionHighlightColor = Color.FromArgb(255, 96, 165, 250); // Accent blue
        var normalStrokeColor = Color.FromArgb(255, 255, 255, 255); // White

        // Convert selected keyframes to HashSet for O(1) lookup
        var selectedSet = new HashSet<KeyframeViewModel>(selectedKeyframes);

        int trackIndex = 0;
        foreach (var track in tracks)
        {
            var y = (trackIndex * trackHeight) + (trackHeight / 2);

            foreach (var keyframe in track.Keyframes)
            {
                var x = (float)(keyframe.TimeSeconds * zoomLevel);
                var isSelected = selectedSet.Contains(keyframe);

                // Convert HueColor to RGB
                var rgb = HueColorToRgb(keyframe.Color);
                var brightness = keyframe.Brightness;
                var color = Color.FromArgb(255,
                    (byte)(rgb.r * brightness),
                    (byte)(rgb.g * brightness),
                    (byte)(rgb.b * brightness));

                // Size and stroke based on selection
                var radius = isSelected ? 10.0f : 8.0f;
                var strokeWidth = isSelected ? 3.0f : 2.0f;
                var strokeColor = isSelected ? selectionHighlightColor : normalStrokeColor;

                // Determine contrast color based on keyframe color brightness
                var colorBrightness = GetBrightness(rgb.r, rgb.g, rgb.b);
                var contrastColor = colorBrightness > 0.5
                    ? Color.FromArgb(255, 0, 0, 0)       // Black for bright keyframes
                    : Color.FromArgb(255, 255, 255, 255); // White for dark keyframes

                // Draw contrast outline (slightly larger than keyframe for visibility on gradients)
                ds.FillCircle(new Vector2(x, y), radius + 2, contrastColor);

                // Draw filled circle
                ds.FillCircle(new Vector2(x, y), radius, color);

                // Draw stroke
                ds.DrawCircle(new Vector2(x, y), radius, strokeColor, strokeWidth);
            }

            trackIndex++;
        }
    }

    /// <summary>
    /// Calculates perceived brightness of a color (0.0 to 1.0).
    /// Uses standard luminance formula (ITU-R BT.601).
    /// </summary>
    private double GetBrightness(int r, int g, int b)
    {
        return (0.299 * r + 0.587 * g + 0.114 * b) / 255.0;
    }

    /// <summary>
    /// Converts HueColor (xy color space) to RGB.
    /// This is a simplified approximation - real conversion requires the light's gamut.
    /// </summary>
    private (int r, int g, int b) HueColorToRgb(HueWindows.Core.Models.HueColor color)
    {
        var x = color.X;
        var y = color.Y;
        var z = 1.0 - x - y;

        var Y = 1.0; // Brightness
        var X = (Y / y) * x;
        var Z = (Y / y) * z;

        // XYZ to RGB (sRGB D65)
        var r = X * 1.656492 - Y * 0.354851 - Z * 0.255038;
        var g = -X * 0.707196 + Y * 1.655397 + Z * 0.036152;
        var b = X * 0.051713 - Y * 0.121364 + Z * 1.011530;

        // Clamp and convert to 0-255
        r = System.Math.Max(0, System.Math.Min(1, r));
        g = System.Math.Max(0, System.Math.Min(1, g));
        b = System.Math.Max(0, System.Math.Min(1, b));

        return ((int)(r * 255), (int)(g * 255), (int)(b * 255));
    }
}
