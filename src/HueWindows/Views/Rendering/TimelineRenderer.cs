using Microsoft.Graphics.Canvas;
using HueWindows.Core.ViewModels;
using System;
using System.Collections.Generic;
using System.Numerics;
using Windows.Foundation;
using Windows.UI;

namespace HueWindows.Views.Rendering;

/// <summary>
/// Context data required for timeline rendering.
/// </summary>
public record TimelineRenderContext(
    IReadOnlyList<TrackViewModel> Tracks,
    IReadOnlyList<EventTrackViewModel> EventTracks,
    IReadOnlyCollection<KeyframeViewModel> SelectedKeyframes,
    float ZoomLevel,
    float DurationSeconds,
    float PlayheadPosition,
    float CanvasWidth,
    float CanvasHeight,
    float TrackHeight,
    float SnapInterval,
    bool IsSnapEnabled,
    KeyframeViewModel? HoveredKeyframe = null,
    bool IsPlayheadHovered = false,
    bool IsPlayheadDragging = false,
    IReadOnlyList<ActivePulse>? ActivePulses = null,
    long CurrentTick = 0,
    (Vector2 Start, Vector2 End)? SelectionRect = null
);

/// <summary>
/// Orchestrates all timeline rendering components in the correct layer order.
/// </summary>
public class TimelineRenderer : IDisposable
{
    private readonly RenderCache _cache;
    private readonly TimeRulerRenderer _rulerRenderer;
    private readonly TrackLanesRenderer _trackRenderer;
    private readonly GradientTrackRenderer _gradientRenderer;
    private readonly KeyframeLayerRenderer _keyframeRenderer;
    private readonly EventPulseRenderer _eventPulseRenderer;
    private readonly PlayheadRenderer _playheadRenderer;

    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimelineRenderer"/> class.
    /// </summary>
    public TimelineRenderer()
    {
        _cache = new RenderCache();
        _rulerRenderer = new TimeRulerRenderer();
        _trackRenderer = new TrackLanesRenderer();
        _gradientRenderer = new GradientTrackRenderer();
        _keyframeRenderer = new KeyframeLayerRenderer();
        _eventPulseRenderer = new EventPulseRenderer();
        _playheadRenderer = new PlayheadRenderer();
    }

    /// <summary>
    /// Creates or recreates cached resources for rendering.
    /// Call this when zoom level or duration changes.
    /// </summary>
    /// <param name="creator">Canvas resource creator.</param>
    /// <param name="context">Current render context.</param>
    public void CreateResources(ICanvasResourceCreator creator, TimelineRenderContext context)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(TimelineRenderer));

        if (context.IsSnapEnabled)
        {
            _cache.Recreate(
                creator,
                context.ZoomLevel,
                context.DurationSeconds,
                context.CanvasHeight,
                context.SnapInterval
            );
        }
    }

    /// <summary>
    /// Draws the complete timeline with all layers in correct order.
    /// </summary>
    /// <param name="ds">Canvas drawing session.</param>
    /// <param name="context">Current render context.</param>
    public void Draw(CanvasDrawingSession ds, TimelineRenderContext context)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(TimelineRenderer));

        var gridColor = Color.FromArgb(25, 255, 255, 255);

        // Layer 1: Snap grid lines (if enabled and cached)
        if (context.IsSnapEnabled && _cache.IsValid)
        {
            _cache.Draw(ds, gridColor);
        }

        // Layer 2: Track lanes (separators, loop region, event tracks)
        _trackRenderer.Draw(
            ds,
            context.Tracks.Count,
            context.EventTracks.Count,
            context.CanvasWidth,
            context.TrackHeight,
            context.DurationSeconds,
            context.ZoomLevel
        );

        // Layer 2.5: Gradient strips (after tracks, before keyframes)
        _gradientRenderer.Draw(
            ds,
            context.Tracks,
            context.ZoomLevel,
            context.TrackHeight,
            context.DurationSeconds
        );

        // Layer 3: Keyframes
        _keyframeRenderer.Draw(
            ds,
            context.Tracks,
            context.SelectedKeyframes,
            context.HoveredKeyframe,
            context.ZoomLevel,
            context.TrackHeight
        );

        // Layer 3.5: Event pulses (between keyframes and playhead)
        if (context.ActivePulses != null && context.ActivePulses.Count > 0)
        {
            _eventPulseRenderer.Draw(
                ds,
                context.ActivePulses,
                context.Tracks.Count,
                context.TrackHeight,
                context.CanvasWidth,
                context.CurrentTick
            );
        }

        // Layer 4: Playhead (draws on top of everything)
        _playheadRenderer.Draw(
            ds,
            GradientTrackRenderer.LeftMargin + (context.PlayheadPosition * context.ZoomLevel),
            context.CanvasHeight,
            context.IsPlayheadHovered,
            context.IsPlayheadDragging
        );

        // Layer 5: Selection rectangle (topmost)
        if (context.SelectionRect.HasValue)
        {
            var (start, end) = context.SelectionRect.Value;
            var rect = new Rect(
                Math.Min(start.X, end.X), Math.Min(start.Y, end.Y),
                Math.Abs(end.X - start.X), Math.Abs(end.Y - start.Y));
            ds.FillRectangle(rect, Color.FromArgb(30, 0, 120, 215));
            ds.DrawRectangle(rect, Color.FromArgb(180, 0, 120, 215), 1f);
        }
    }

    /// <summary>
    /// Draws the time ruler (separate canvas above timeline).
    /// </summary>
    /// <param name="ds">Canvas drawing session for ruler.</param>
    /// <param name="width">Width of the ruler.</param>
    /// <param name="height">Height of the ruler (typically 20-30 pixels).</param>
    /// <param name="zoomLevel">Current zoom level.</param>
    /// <param name="durationSeconds">Timeline duration.</param>
    public void DrawRuler(
        CanvasDrawingSession ds,
        float width,
        float height,
        float zoomLevel,
        int durationSeconds)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(TimelineRenderer));

        _rulerRenderer.Draw(ds, width, height, zoomLevel, durationSeconds);
    }

    /// <summary>
    /// Invalidates cached resources, forcing them to be recreated on next CreateResources call.
    /// </summary>
    public void InvalidateCache()
    {
        if (!_disposed)
        {
            _cache.Invalidate();
        }
    }

    /// <summary>
    /// Disposes all renderer resources.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _cache.Dispose();
            _rulerRenderer.Dispose();
            _playheadRenderer.Dispose();
            _disposed = true;
        }
    }
}
