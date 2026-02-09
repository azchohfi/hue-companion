using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using System.Numerics;
using HueWindows.Core.ViewModels;

namespace HueWindows.Views.Rendering;

/// <summary>
/// Result of hit testing a point on the timeline.
/// </summary>
public record HitTestResult
{
    public HitType Type { get; init; }
    public KeyframeViewModel? Keyframe { get; init; }
    public TrackViewModel? Track { get; init; }
    public EventTrackViewModel? EventTrack { get; init; }
    public int TrackIndex { get; init; } = -1;
}

public enum HitType
{
    None,
    Playhead,
    Keyframe,
    Track,
    EventTrack
}

/// <summary>
/// Provides hit testing for timeline elements using Win2D geometry.
/// </summary>
public static class HitTestHelper
{
    private const float KeyframeRadius = 8f;
    private const float KeyframeSelectedRadius = 10f;
    private const float PlayheadHitWidth = 44f; // WCAG 2.5.5 AAA minimum
    private const float PlayheadTriangleHitHeight = 44f; // Extended vertical hit area for handle
    private const float PlayheadLineHitWidth = 12f; // Narrower hit area for line portion
    private const float TrackHeight = 50f;

    /// <summary>
    /// Tests what element is at the given point on the timeline.
    /// </summary>
    public static HitTestResult HitTest(
        ICanvasResourceCreator device,
        Vector2 point,
        float playheadX,
        float canvasHeight,
        IReadOnlyList<TrackViewModel> tracks,
        IReadOnlyList<EventTrackViewModel> eventTracks,
        IReadOnlyCollection<KeyframeViewModel> selectedKeyframes,
        float zoomLevel)
    {
        // Test playhead handle first (top triangle, highest priority)
        if (HitTestPlayheadHandle(point, playheadX))
        {
            return new HitTestResult { Type = HitType.Playhead };
        }

        // Test keyframes (higher priority than playhead line in track area)
        var keyframeResult = HitTestKeyframes(point, tracks, selectedKeyframes, zoomLevel);
        if (keyframeResult != null)
        {
            return keyframeResult;
        }

        // Test playhead line (lower priority than keyframes so dots are clickable)
        if (HitTestPlayheadLine(point, playheadX, canvasHeight))
        {
            return new HitTestResult { Type = HitType.Playhead };
        }

        // Test event tracks
        var lightTracksHeight = tracks.Count * TrackHeight;
        var eventTrackResult = HitTestEventTracks(point, eventTracks, lightTracksHeight);
        if (eventTrackResult != null)
        {
            return eventTrackResult;
        }

        // Test regular tracks
        var trackResult = HitTestTracks(point, tracks);
        if (trackResult != null)
        {
            return trackResult;
        }

        return new HitTestResult { Type = HitType.None };
    }

    private static bool HitTestPlayheadHandle(Vector2 point, float playheadX)
    {
        // Top area: 44x44px hit zone for triangle handle (WCAG 2.5.5 compliant)
        if (point.Y > PlayheadTriangleHitHeight)
            return false;

        var hitLeft = playheadX - PlayheadHitWidth / 2;
        var hitRight = playheadX + PlayheadHitWidth / 2;
        return point.X >= hitLeft && point.X <= hitRight;
    }

    private static bool HitTestPlayheadLine(Vector2 point, float playheadX, float height)
    {
        // Rest of line: narrower hit area (12px wide) for precision
        var lineHitLeft = playheadX - PlayheadLineHitWidth / 2;
        var lineHitRight = playheadX + PlayheadLineHitWidth / 2;

        return point.X >= lineHitLeft && point.X <= lineHitRight &&
               point.Y >= PlayheadTriangleHitHeight && point.Y <= height;
    }

    private static HitTestResult? HitTestKeyframes(
        Vector2 point,
        IReadOnlyList<TrackViewModel> tracks,
        IReadOnlyCollection<KeyframeViewModel> selectedKeyframes,
        float zoomLevel)
    {
        for (int trackIndex = 0; trackIndex < tracks.Count; trackIndex++)
        {
            var track = tracks[trackIndex];
            var trackY = trackIndex * TrackHeight + TrackHeight / 2;

            // Test in reverse order (later keyframes drawn on top)
            for (int i = track.Keyframes.Count - 1; i >= 0; i--)
            {
                var keyframe = track.Keyframes[i];
                var kfX = GradientTrackRenderer.LeftMargin + (float)(keyframe.TimeSeconds * zoomLevel);
                var isSelected = selectedKeyframes.Contains(keyframe);
                var radius = isSelected ? KeyframeSelectedRadius : KeyframeRadius;

                // Circle hit test
                var dx = point.X - kfX;
                var dy = point.Y - trackY;
                if (dx * dx + dy * dy <= radius * radius)
                {
                    return new HitTestResult
                    {
                        Type = HitType.Keyframe,
                        Keyframe = keyframe,
                        Track = track,
                        TrackIndex = trackIndex
                    };
                }
            }
        }
        return null;
    }

    private static HitTestResult? HitTestTracks(Vector2 point, IReadOnlyList<TrackViewModel> tracks)
    {
        var trackIndex = (int)(point.Y / TrackHeight);
        if (trackIndex >= 0 && trackIndex < tracks.Count)
        {
            return new HitTestResult
            {
                Type = HitType.Track,
                Track = tracks[trackIndex],
                TrackIndex = trackIndex
            };
        }
        return null;
    }

    private static HitTestResult? HitTestEventTracks(
        Vector2 point,
        IReadOnlyList<EventTrackViewModel> eventTracks,
        float lightTracksHeight)
    {
        if (point.Y < lightTracksHeight)
            return null;

        var eventTrackIndex = (int)((point.Y - lightTracksHeight) / TrackHeight);
        if (eventTrackIndex >= 0 && eventTrackIndex < eventTracks.Count)
        {
            return new HitTestResult
            {
                Type = HitType.EventTrack,
                EventTrack = eventTracks[eventTrackIndex],
                TrackIndex = eventTrackIndex
            };
        }
        return null;
    }
}
