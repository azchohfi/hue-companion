using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using System.Numerics;
using Windows.UI;

namespace HueWindows.Views.Rendering;

/// <summary>
/// Renders the time ruler with tick marks and time labels.
/// </summary>
public class TimeRulerRenderer
{
    private readonly CanvasTextFormat _labelFormat;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeRulerRenderer"/> class.
    /// </summary>
    public TimeRulerRenderer()
    {
        _labelFormat = new CanvasTextFormat
        {
            FontSize = 10,
            HorizontalAlignment = CanvasHorizontalAlignment.Left,
            VerticalAlignment = CanvasVerticalAlignment.Top
        };
    }

    /// <summary>
    /// Draws the time ruler with tick marks and labels.
    /// </summary>
    /// <param name="ds">Canvas drawing session.</param>
    /// <param name="width">Width of the ruler.</param>
    /// <param name="height">Height of the ruler (typically 20-30 pixels).</param>
    /// <param name="zoomLevel">Current zoom level (pixels per second).</param>
    /// <param name="durationSeconds">Timeline duration in seconds.</param>
    public void Draw(CanvasDrawingSession ds, float width, float height, float zoomLevel, int durationSeconds)
    {
        var tickColor = Color.FromArgb(255, 128, 128, 128); // Gray
        var labelColor = Color.FromArgb(255, 128, 128, 128); // Gray

        // Draw tick marks every second, labels every 5 seconds
        for (int i = 0; i <= durationSeconds; i++)
        {
            var x = i * zoomLevel;
            var tickHeight = (i % 5 == 0) ? 12.0f : 6.0f;

            // Draw tick mark
            ds.DrawLine(
                new Vector2(x, height - tickHeight),
                new Vector2(x, height),
                tickColor,
                1.0f
            );

            // Add labels every 5 seconds
            if (i % 5 == 0)
            {
                var label = $"{i}s";
                ds.DrawText(
                    label,
                    new Vector2(x - 8, 0),
                    labelColor,
                    _labelFormat
                );
            }
        }
    }
}
