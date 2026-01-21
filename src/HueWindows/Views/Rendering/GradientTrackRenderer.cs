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
    /// Corner radius for rounded rectangles.
    /// Subtle rounding rather than full pill shape.
    /// </summary>
    private const float CornerRadius = 4.0f;

    /// <summary>
    /// Left margin for timeline content to prevent keyframes at time=0 from being clipped.
    /// </summary>
    public const float LeftMargin = 14.0f;

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
    /// All x positions are offset by LeftMargin to give keyframes room at edges.
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
            var x = LeftMargin;
            var width = (float)(firstKf.TimeSeconds * zoomLevel);
            var color = HueColorToWindowsColor(firstKf.Color);
            DrawSolidSegment(ds, x, y, width, trackHeight, color);
        }

        // Gradient segments between keyframes
        for (int i = 0; i < keyframes.Count - 1; i++)
        {
            var startKf = keyframes[i];
            var endKf = keyframes[i + 1];

            var segmentStartX = LeftMargin + (float)(startKf.TimeSeconds * zoomLevel);
            var segmentEndX = LeftMargin + (float)(endKf.TimeSeconds * zoomLevel);
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
            var extendX = LeftMargin + (float)(lastKf.TimeSeconds * zoomLevel);
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
        var rect = new Rect(x, y, width, height);

        // Create gradient stops with easing
        var stops = CreateEasedGradientStops(startKf, endKf);

        // Create gradient brush with horizontal direction
        using var gradientBrush = new CanvasLinearGradientBrush(ds, stops)
        {
            StartPoint = new Vector2(x, 0),
            EndPoint = new Vector2(x + width, 0)
        };

        // Fill with gradient
        ds.FillRoundedRectangle(rect, CornerRadius, CornerRadius, gradientBrush);

        // Add depth effect (highlight and shadow)
        DrawDepthEffect(ds, x, y, width, height);

        // Draw subtle dark border
        var borderColor = Color.FromArgb(60, 0, 0, 0);
        ds.DrawRoundedRectangle(rect, CornerRadius, CornerRadius, borderColor, 1.0f);
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

        var rect = new Rect(x, y, width, height);

        using var brush = new CanvasSolidColorBrush(ds, color);
        ds.FillRoundedRectangle(rect, CornerRadius, CornerRadius, brush);

        // Add depth effect (highlight and shadow)
        DrawDepthEffect(ds, x, y, width, height);

        // Draw subtle dark border
        var borderColor = Color.FromArgb(60, 0, 0, 0);
        ds.DrawRoundedRectangle(rect, CornerRadius, CornerRadius, borderColor, 1.0f);
    }

    /// <summary>
    /// Draws a depth effect with top highlight and bottom shadow for a 3D appearance.
    /// </summary>
    private void DrawDepthEffect(
        CanvasDrawingSession ds,
        float x,
        float y,
        float width,
        float height)
    {
        // Inset from edges to stay within rounded corners
        var inset = CornerRadius;
        var effectWidth = width - (inset * 2);
        if (effectWidth <= 0)
            return;

        // Top highlight - subtle white gradient fading down
        var highlightHeight = height * 0.35f;
        var highlightStops = new CanvasGradientStop[]
        {
            new() { Position = 0.0f, Color = Color.FromArgb(50, 255, 255, 255) },
            new() { Position = 1.0f, Color = Color.FromArgb(0, 255, 255, 255) }
        };
        using var highlightBrush = new CanvasLinearGradientBrush(ds, highlightStops)
        {
            StartPoint = new Vector2(0, y),
            EndPoint = new Vector2(0, y + highlightHeight)
        };
        var highlightRect = new Rect(x + inset, y, effectWidth, highlightHeight);
        ds.FillRectangle(highlightRect, highlightBrush);

        // Bottom shadow - subtle dark gradient fading up
        var shadowHeight = height * 0.25f;
        var shadowStops = new CanvasGradientStop[]
        {
            new() { Position = 0.0f, Color = Color.FromArgb(0, 0, 0, 0) },
            new() { Position = 1.0f, Color = Color.FromArgb(40, 0, 0, 0) }
        };
        using var shadowBrush = new CanvasLinearGradientBrush(ds, shadowStops)
        {
            StartPoint = new Vector2(0, y + height - shadowHeight),
            EndPoint = new Vector2(0, y + height)
        };
        var shadowRect = new Rect(x + inset, y + height - shadowHeight, effectWidth, shadowHeight);
        ds.FillRectangle(shadowRect, shadowBrush);
    }

    /// <summary>
    /// Linearly interpolates between two HueColors in RGB space.
    /// </summary>
    private Color InterpolateColor(HueColor startColor, HueColor endColor, double t)
    {
        var (sr, sg, sb) = startColor.ToRgb(1.0);
        var (er, eg, eb) = endColor.ToRgb(1.0);

        // Linear RGB interpolation in byte space
        var r = sr + (er - sr) * t;
        var g = sg + (eg - sg) * t;
        var b = sb + (eb - sb) * t;

        return Color.FromArgb(255, (byte)r, (byte)g, (byte)b);
    }

    /// <summary>
    /// Converts HueColor to Windows.UI.Color using canonical ToRgb() method.
    /// </summary>
    private Color HueColorToWindowsColor(HueColor color)
    {
        var (r, g, b) = color.ToRgb(1.0); // brightness=1.0 for visualization
        return Color.FromArgb(255, r, g, b);
    }
}
