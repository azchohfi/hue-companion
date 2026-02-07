using Microsoft.Graphics.Canvas;
using System.Collections.Generic;
using Windows.Foundation;
using Windows.UI;

namespace HueWindows.Views.Rendering;

/// <summary>
/// Represents an active visual pulse triggered by an event firing.
/// </summary>
public record ActivePulse(
    int TrackIndex,       // Which light track to pulse (-1 for event track)
    int EventTrackIndex,  // Index within EventTracks (-1 for light track)
    long StartTick,       // Environment.TickCount64 when pulse started
    Color PulseColor      // Color of the pulse
);

/// <summary>
/// Renders animated pulse overlays when event tracks fire during playback.
/// Pulses fade from semi-transparent to invisible over 300ms.
/// </summary>
public class EventPulseRenderer
{
    private const int PulseDurationMs = 300;

    /// <summary>
    /// Draws active pulse overlays on the timeline.
    /// </summary>
    /// <param name="ds">Canvas drawing session.</param>
    /// <param name="activePulses">Currently active pulses to render.</param>
    /// <param name="lightTrackCount">Number of light tracks (for y-offset calculation).</param>
    /// <param name="trackHeight">Height of each track row.</param>
    /// <param name="canvasWidth">Width of the canvas.</param>
    /// <param name="currentTick">Current Environment.TickCount64 value.</param>
    public void Draw(
        CanvasDrawingSession ds,
        IReadOnlyList<ActivePulse> activePulses,
        int lightTrackCount,
        float trackHeight,
        float canvasWidth,
        long currentTick)
    {
        foreach (var pulse in activePulses)
        {
            var age = currentTick - pulse.StartTick;
            if (age >= PulseDurationMs)
                continue;

            // Fade from 0.4 to 0.0 over duration
            var progress = age / (double)PulseDurationMs;
            var alpha = (byte)(102 * (1.0 - progress)); // 102 = 0.4 * 255

            var color = Color.FromArgb(alpha, pulse.PulseColor.R, pulse.PulseColor.G, pulse.PulseColor.B);

            // Calculate y position
            float y;
            if (pulse.TrackIndex >= 0)
            {
                // Light track pulse
                y = pulse.TrackIndex * trackHeight;
            }
            else
            {
                // Event track pulse
                y = (lightTrackCount + pulse.EventTrackIndex) * trackHeight;
            }

            var rect = new Rect(0, y, canvasWidth, trackHeight);
            ds.FillRectangle(rect, color);
        }
    }

    /// <summary>
    /// Duration of a pulse in milliseconds. Used for pruning expired pulses.
    /// </summary>
    public static int DurationMs => PulseDurationMs;
}
