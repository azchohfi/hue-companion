using Microsoft.Graphics.Canvas;
using System.Numerics;
using Windows.UI;

namespace HueCompanion.Views.Rendering;

/// <summary>
/// Renders track lane separators, loop region, and event track backgrounds.
/// </summary>
public class TrackLanesRenderer
{
    /// <summary>
    /// Draws track lane separators and backgrounds.
    /// </summary>
    /// <param name="ds">Canvas drawing session.</param>
    /// <param name="trackCount">Number of light tracks.</param>
    /// <param name="eventTrackCount">Number of event tracks.</param>
    /// <param name="width">Canvas width.</param>
    /// <param name="trackHeight">Height of each track (typically 50 pixels).</param>
    /// <param name="loopDurationSeconds">Loop region duration.</param>
    /// <param name="zoomLevel">Current zoom level (pixels per second).</param>
    public void Draw(
        CanvasDrawingSession ds,
        int trackCount,
        int eventTrackCount,
        float width,
        float trackHeight,
        float loopDurationSeconds,
        float zoomLevel)
    {
        var separatorColor = Color.FromArgb(40, 255, 255, 255);
        var loopBackgroundColor = Color.FromArgb(18, 96, 165, 250); // Subtle blue tint
        var loopBorderColor = Color.FromArgb(120, 96, 165, 250); // Accent blue
        var eventTrackBackgroundColor = Color.FromArgb(20, 255, 200, 100); // Subtle warm tint
        var eventTrackSeparatorColor = Color.FromArgb(60, 255, 200, 100);

        var lightTracksHeight = trackCount * trackHeight;
        var totalHeight = lightTracksHeight + (eventTrackCount * trackHeight);

        // Draw loop region background (subtle blue tint)
        var loopEndX = loopDurationSeconds * zoomLevel;
        ds.FillRectangle(
            0, 0,
            loopEndX, totalHeight,
            loopBackgroundColor
        );

        // Draw loop end boundary line (thicker, accent color)
        ds.DrawLine(
            new Vector2(loopEndX, 0),
            new Vector2(loopEndX, totalHeight),
            loopBorderColor,
            2.0f
        );

        // Draw track separator lines for light tracks
        for (int i = 1; i < trackCount; i++)
        {
            var y = i * trackHeight;
            ds.DrawLine(
                new Vector2(0, y),
                new Vector2(width, y),
                separatorColor,
                1.0f
            );
        }

        // Draw event tracks (if any)
        if (eventTrackCount > 0)
        {
            // Draw separator between light tracks and event tracks
            ds.DrawLine(
                new Vector2(0, lightTracksHeight),
                new Vector2(width, lightTracksHeight),
                eventTrackSeparatorColor,
                1.0f
            );

            // Draw background and separators for each event track
            for (int i = 0; i < eventTrackCount; i++)
            {
                var y = lightTracksHeight + (i * trackHeight);

                // Draw background
                ds.FillRectangle(
                    0, y,
                    width, trackHeight,
                    eventTrackBackgroundColor
                );

                // Draw separator line (except for first event track, already drawn above)
                if (i > 0)
                {
                    ds.DrawLine(
                        new Vector2(0, y),
                        new Vector2(width, y),
                        eventTrackSeparatorColor,
                        1.0f
                    );
                }

                // Draw dashed pattern line in the middle of the event track
                var midY = y + (trackHeight / 2);
                DrawDashedLine(ds, 0, midY, width, midY, eventTrackSeparatorColor, 2.0f, 4.0f);
            }
        }
    }

    /// <summary>
    /// Draws a dashed line.
    /// </summary>
    private void DrawDashedLine(
        CanvasDrawingSession ds,
        float x1, float y1,
        float x2, float y2,
        Color color,
        float strokeWidth,
        float dashLength)
    {
        var length = x2 - x1;
        var dashCount = (int)(length / (dashLength * 2));

        for (int i = 0; i < dashCount; i++)
        {
            var startX = x1 + (i * dashLength * 2);
            var endX = startX + dashLength;
            ds.DrawLine(
                new Vector2(startX, y1),
                new Vector2(endX, y2),
                color,
                strokeWidth
            );
        }
    }
}
