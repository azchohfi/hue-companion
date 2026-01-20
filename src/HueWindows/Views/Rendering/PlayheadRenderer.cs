using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using System.Numerics;
using Windows.UI;

namespace HueWindows.Views.Rendering;

/// <summary>
/// Renders the playhead vertical line and triangle handle.
/// </summary>
public class PlayheadRenderer
{
    private CanvasGeometry? _triangleGeometry;
    private ICanvasResourceCreator? _resourceCreator;

    /// <summary>
    /// Draws the playhead line and handle.
    /// </summary>
    /// <param name="ds">Canvas drawing session.</param>
    /// <param name="playheadX">X position of the playhead in pixels.</param>
    /// <param name="height">Height of the canvas.</param>
    public void Draw(CanvasDrawingSession ds, float playheadX, float height)
    {
        var playheadColor = Color.FromArgb(255, 255, 100, 100); // Red

        // Draw vertical playhead line
        ds.DrawLine(
            new Vector2(playheadX, 0),
            new Vector2(playheadX, height),
            playheadColor,
            2.0f
        );

        // Draw triangle handle at top
        EnsureTriangleGeometry(ds);
        if (_triangleGeometry != null)
        {
            // Translate the pre-created triangle to the playhead position
            var transform = Matrix3x2.CreateTranslation(playheadX, 0);
            ds.Transform = transform;
            ds.FillGeometry(_triangleGeometry, playheadColor);
            ds.Transform = Matrix3x2.Identity;
        }
    }

    /// <summary>
    /// Ensures the triangle geometry is created (only once per resource creator).
    /// </summary>
    private void EnsureTriangleGeometry(CanvasDrawingSession ds)
    {
        if (_triangleGeometry != null && ReferenceEquals(_resourceCreator, ds.Device))
            return;

        // Dispose old geometry if resource creator changed
        _triangleGeometry?.Dispose();
        _resourceCreator = ds.Device;

        // Create triangle pointing down (centered at origin)
        using var pathBuilder = new CanvasPathBuilder(ds);
        pathBuilder.BeginFigure(-8, 0);   // Left point
        pathBuilder.AddLine(8, 0);        // Right point
        pathBuilder.AddLine(0, 12);       // Bottom point
        pathBuilder.EndFigure(CanvasFigureLoop.Closed);

        _triangleGeometry = CanvasGeometry.CreatePath(pathBuilder);
    }

    /// <summary>
    /// Disposes cached geometry resources.
    /// </summary>
    public void Dispose()
    {
        _triangleGeometry?.Dispose();
        _triangleGeometry = null;
    }
}
