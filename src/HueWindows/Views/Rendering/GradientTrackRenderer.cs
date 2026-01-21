using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using HueWindows.Core.ViewModels;
using HueWindows.Core.Models;
using HueWindows.Core.Services;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Windows.Foundation;
using Windows.UI;

namespace HueWindows.Views.Rendering;

/// <summary>
/// Renders continuous color gradient strips behind keyframe markers.
/// Gradients show how light colors evolve between keyframes with easing-aware interpolation.
/// </summary>
public class GradientTrackRenderer
{
    /// <summary>
    /// Number of gradient stops for smooth visual appearance.
    /// 15 stops provides smooth gradients without excessive GPU work.
    /// </summary>
    private const int NumGradientStops = 15;

    /// <summary>
    /// Minimum segment width in pixels before falling back to solid color.
    /// Very narrow gradients can cause visual artifacts.
    /// </summary>
    private const float MinSegmentWidth = 5.0f;

    /// <summary>
    /// Draws gradient strips for all tracks showing color evolution between keyframes.
    /// </summary>
    /// <param name="ds">Canvas drawing session.</param>
    /// <param name="tracks">Collection of track view models containing keyframes.</param>
    /// <param name="zoomLevel">Current zoom level (pixels per second).</param>
    /// <param name="trackHeight">Height of each track (typically 50 pixels).</param>
    /// <param name="durationSeconds">Total duration of the timeline.</param>
    public void Draw(
        CanvasDrawingSession ds,
        IEnumerable<TrackViewModel> tracks,
        float zoomLevel,
        float trackHeight,
        float durationSeconds)
    {
        int trackIndex = 0;
        foreach (var track in tracks)
        {
            var y = trackIndex * trackHeight;
            DrawTrackGradient(ds, track, y, zoomLevel, trackHeight, durationSeconds);
            trackIndex++;
        }
    }

    /// <summary>
    /// Draws the gradient strip for a single track.
    /// </summary>
    private void DrawTrackGradient(
        CanvasDrawingSession ds,
        TrackViewModel track,
        float y,
        float zoomLevel,
        float trackHeight,
        float durationSeconds)
    {
        var keyframes = track.Keyframes.OrderBy(k => k.TimeSeconds).ToList();
        if (keyframes.Count == 0)
            return;

        var firstKf = keyframes[0];
        var lastKf = keyframes[^1];

        // Edge extension: solid color before first keyframe
        if (firstKf.TimeSeconds > 0)
        {
            var width = (float)(firstKf.TimeSeconds * zoomLevel);
            var color = HueColorToWindowsColor(firstKf.Color);
            DrawSolidSegment(ds, 0, y, width, trackHeight, color);
        }

        // Gradient segments between keyframes
        for (int i = 0; i < keyframes.Count - 1; i++)
        {
            var startKf = keyframes[i];
            var endKf = keyframes[i + 1];

            var segmentStartX = (float)(startKf.TimeSeconds * zoomLevel);
            var segmentEndX = (float)(endKf.TimeSeconds * zoomLevel);
            var segmentWidth = segmentEndX - segmentStartX;

            // Very short segments fall back to solid midpoint color
            if (segmentWidth < MinSegmentWidth)
            {
                var midColor = InterpolateColor(startKf.Color, endKf.Color, 0.5);
                DrawSolidSegment(ds, segmentStartX, y, segmentWidth, trackHeight, midColor);
            }
            else
            {
                DrawGradientSegment(ds, startKf, endKf, segmentStartX, y, segmentWidth, trackHeight);
            }
        }

        // Edge extension: solid color after last keyframe
        if (lastKf.TimeSeconds < durationSeconds)
        {
            var extendX = (float)(lastKf.TimeSeconds * zoomLevel);
            var extendWidth = (float)((durationSeconds - lastKf.TimeSeconds) * zoomLevel);
            var color = HueColorToWindowsColor(lastKf.Color);
            DrawSolidSegment(ds, extendX, y, extendWidth, trackHeight, color);
        }
    }

    /// <summary>
    /// Draws a gradient segment between two keyframes with easing-aware interpolation.
    /// </summary>
    private void DrawGradientSegment(
        CanvasDrawingSession ds,
        KeyframeViewModel startKf,
        KeyframeViewModel endKf,
        float x,
        float y,
        float width,
        float height)
    {
        var cornerRadius = height / 2; // Pill shape
        var rect = new Rect(x, y, width, height);

        // Create gradient stops with easing
        var stops = CreateEasedGradientStops(startKf, endKf);

        // Create gradient brush with horizontal direction
        using var gradientBrush = new CanvasLinearGradientBrush(ds, stops)
        {
            StartPoint = new Vector2(x, 0),
            EndPoint = new Vector2(x + width, 0)
        };

        // Fill with gradient (pill shape)
        ds.FillRoundedRectangle(rect, cornerRadius, cornerRadius, gradientBrush);

        // Draw subtle dark border
        var borderColor = Color.FromArgb(80, 0, 0, 0);
        ds.DrawRoundedRectangle(rect, cornerRadius, cornerRadius, borderColor, 1.0f);
    }

    /// <summary>
    /// Creates gradient stops with easing applied to match keyframe TransitionStyle.
    /// </summary>
    private CanvasGradientStop[] CreateEasedGradientStops(
        KeyframeViewModel startKf,
        KeyframeViewModel endKf)
    {
        // Special case for Instant transition: hard cut at 99%
        if (endKf.Transition == TransitionStyle.Instant)
        {
            return new CanvasGradientStop[]
            {
                new() { Position = 0.0f, Color = HueColorToWindowsColor(startKf.Color) },
                new() { Position = 0.99f, Color = HueColorToWindowsColor(startKf.Color) },
                new() { Position = 1.0f, Color = HueColorToWindowsColor(endKf.Color) }
            };
        }

        var stops = new CanvasGradientStop[NumGradientStops];

        for (int i = 0; i < NumGradientStops; i++)
        {
            // Linear position within segment (0.0 to 1.0)
            float position = i / (float)(NumGradientStops - 1);

            // Apply easing based on end keyframe's transition style
            double easedT = Easing.Apply(position, endKf.Transition);

            // Interpolate color using eased t
            var color = InterpolateColor(startKf.Color, endKf.Color, easedT);

            stops[i] = new CanvasGradientStop
            {
                Position = position,
                Color = color
            };
        }

        return stops;
    }

    /// <summary>
    /// Draws a solid color segment (for edge extensions or very short segments).
    /// </summary>
    private void DrawSolidSegment(
        CanvasDrawingSession ds,
        float x,
        float y,
        float width,
        float height,
        Color color)
    {
        if (width <= 0)
            return;

        var cornerRadius = height / 2; // Pill shape
        var rect = new Rect(x, y, width, height);

        using var brush = new CanvasSolidColorBrush(ds, color);
        ds.FillRoundedRectangle(rect, cornerRadius, cornerRadius, brush);

        // Draw subtle dark border
        var borderColor = Color.FromArgb(80, 0, 0, 0);
        ds.DrawRoundedRectangle(rect, cornerRadius, cornerRadius, borderColor, 1.0f);
    }

    /// <summary>
    /// Linearly interpolates between two HueColors in RGB space.
    /// </summary>
    private Color InterpolateColor(HueColor startColor, HueColor endColor, double t)
    {
        var startRgb = HueColorToRgb(startColor);
        var endRgb = HueColorToRgb(endColor);

        // Linear RGB interpolation
        var r = startRgb.r + (endRgb.r - startRgb.r) * t;
        var g = startRgb.g + (endRgb.g - startRgb.g) * t;
        var b = startRgb.b + (endRgb.b - startRgb.b) * t;

        return Color.FromArgb(255,
            (byte)System.Math.Clamp(r * 255, 0, 255),
            (byte)System.Math.Clamp(g * 255, 0, 255),
            (byte)System.Math.Clamp(b * 255, 0, 255));
    }

    /// <summary>
    /// Converts HueColor to Windows.UI.Color.
    /// </summary>
    private Color HueColorToWindowsColor(HueColor color)
    {
        var rgb = HueColorToRgb(color);
        return Color.FromArgb(255,
            (byte)(rgb.r * 255),
            (byte)(rgb.g * 255),
            (byte)(rgb.b * 255));
    }

    /// <summary>
    /// Converts HueColor (xy color space) to RGB.
    /// This is a simplified approximation - real conversion requires the light's gamut.
    /// </summary>
    private (double r, double g, double b) HueColorToRgb(HueColor color)
    {
        var x = color.X;
        var y = color.Y;
        var z = 1.0 - x - y;

        var Y = 1.0; // Full brightness for gradient visualization (brightness not shown)
        var X = (Y / y) * x;
        var Z = (Y / y) * z;

        // XYZ to RGB (sRGB D65)
        var r = X * 1.656492 - Y * 0.354851 - Z * 0.255038;
        var g = -X * 0.707196 + Y * 1.655397 + Z * 0.036152;
        var b = X * 0.051713 - Y * 0.121364 + Z * 1.011530;

        // Clamp to 0-1 range
        r = System.Math.Max(0, System.Math.Min(1, r));
        g = System.Math.Max(0, System.Math.Min(1, g));
        b = System.Math.Max(0, System.Math.Min(1, b));

        return (r, g, b);
    }
}
