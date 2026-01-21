using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
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
    /// <param name="hoveredKeyframe">Currently hovered keyframe (if any).</param>
    /// <param name="zoomLevel">Current zoom level (pixels per second).</param>
    /// <param name="trackHeight">Height of each track (typically 50 pixels).</param>
    public void Draw(
        CanvasDrawingSession ds,
        IEnumerable<TrackViewModel> tracks,
        IEnumerable<KeyframeViewModel> selectedKeyframes,
        KeyframeViewModel? hoveredKeyframe,
        float zoomLevel,
        float trackHeight)
    {
        var normalStrokeColor = Color.FromArgb(255, 255, 255, 255); // White

        // Convert selected keyframes to HashSet for O(1) lookup
        var selectedSet = new HashSet<KeyframeViewModel>(selectedKeyframes);

        int trackIndex = 0;
        foreach (var track in tracks)
        {
            var y = (trackIndex * trackHeight) + (trackHeight / 2);

            foreach (var keyframe in track.Keyframes)
            {
                // Apply left margin to match gradient bar positioning
                var x = GradientTrackRenderer.LeftMargin + (float)(keyframe.TimeSeconds * zoomLevel);
                var isSelected = selectedSet.Contains(keyframe);
                var isHovered = keyframe == hoveredKeyframe;

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
                var strokeColor = isSelected ? Color.FromArgb(255, 0, 0, 0) : normalStrokeColor;

                // Glow parameters: selected gets persistent glow, hovered gets lighter glow
                var showGlow = isSelected || isHovered;
                var glowRadius = isSelected ? 12f : 8f;
                var glowOpacity = isSelected ? 0.8f : 0.6f;

                DrawKeyframeWithGlow(
                    ds,
                    new Vector2(x, y),
                    radius,
                    color,
                    strokeColor,
                    strokeWidth,
                    showGlow,
                    glowRadius,
                    glowOpacity);
            }

            trackIndex++;
        }
    }

    /// <summary>
    /// Draws a keyframe with optional glow effect for hover/selection state.
    /// </summary>
    private void DrawKeyframeWithGlow(
        CanvasDrawingSession ds,
        Vector2 position,
        float radius,
        Color keyframeColor,
        Color strokeColor,
        float strokeWidth,
        bool showGlow,
        float glowRadius,
        float glowOpacity)
    {
        if (showGlow)
        {
            // Create CommandList for keyframe shape (required as ShadowEffect source)
            using var commandList = new CanvasCommandList(ds);
            using (var clDs = commandList.CreateDrawingSession())
            {
                clDs.FillCircle(position, radius, keyframeColor);
            }

            // Create glow with color matching keyframe
            var glowColor = Color.FromArgb(
                (byte)(255 * glowOpacity),
                keyframeColor.R,
                keyframeColor.G,
                keyframeColor.B);

            using var glowEffect = new ShadowEffect
            {
                Source = commandList,
                BlurAmount = glowRadius,
                ShadowColor = glowColor
            };

            // Draw glow centered on keyframe
            ds.DrawImage(glowEffect);
        }

        // Draw contrast outline (slightly larger for visibility on gradients)
        var colorBrightness = GetBrightness(keyframeColor.R, keyframeColor.G, keyframeColor.B);
        var contrastColor = colorBrightness > 0.5
            ? Color.FromArgb(255, 0, 0, 0)
            : Color.FromArgb(255, 255, 255, 255);
        ds.FillCircle(position, radius + 2, contrastColor);

        // Draw filled keyframe circle
        ds.FillCircle(position, radius, keyframeColor);

        // Draw stroke
        ds.DrawCircle(position, radius, strokeColor, strokeWidth);
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
