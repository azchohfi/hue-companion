using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Windows.UI;

namespace HueWindows.Views.Rendering;

/// <summary>
/// Manages cached Win2D geometry for timeline rendering to avoid creating geometry every frame.
/// </summary>
public class RenderCache : IDisposable
{
    private CanvasCachedGeometry? _gridLines;
    private float _cachedZoomLevel;
    private float _cachedDurationSeconds;
    private float _cachedHeight;
    private bool _disposed;

    /// <summary>
    /// Gets whether the cache is valid and ready to use.
    /// </summary>
    public bool IsValid => _gridLines != null && !_disposed;

    /// <summary>
    /// Invalidates the cache, forcing it to be recreated on next use.
    /// </summary>
    public void Invalidate()
    {
        _gridLines?.Dispose();
        _gridLines = null;
    }

    /// <summary>
    /// Recreates cached geometry with new parameters.
    /// </summary>
    /// <param name="creator">Canvas resource creator (typically CanvasDevice or CanvasControl).</param>
    /// <param name="zoomLevel">Current zoom level (pixels per second).</param>
    /// <param name="durationSeconds">Timeline duration in seconds.</param>
    /// <param name="height">Canvas height for grid lines.</param>
    /// <param name="snapInterval">Snap grid interval in seconds.</param>
    public void Recreate(ICanvasResourceCreator creator, float zoomLevel, float durationSeconds, float height, float snapInterval)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(RenderCache));

        // Only recreate if parameters have changed
        if (IsValid &&
            _cachedZoomLevel == zoomLevel &&
            _cachedDurationSeconds == durationSeconds &&
            _cachedHeight == height)
        {
            return;
        }

        // Dispose old geometry
        Invalidate();

        // Create new grid lines geometry
        using var pathBuilder = new CanvasPathBuilder(creator);

        // Grid extends beyond duration with a fixed buffer (matching canvas width calculation)
        var gridExtentSeconds = durationSeconds + (500 / zoomLevel);

        // Draw vertical grid lines at each snap interval
        for (float time = snapInterval; time < gridExtentSeconds; time += snapInterval)
        {
            var x = time * zoomLevel;
            pathBuilder.BeginFigure(x, 0);
            pathBuilder.AddLine(x, height);
            pathBuilder.EndFigure(CanvasFigureLoop.Open);
        }

        var geometry = CanvasGeometry.CreatePath(pathBuilder);
        _gridLines = CanvasCachedGeometry.CreateStroke(geometry, 1.0f);

        // Store parameters for change detection
        _cachedZoomLevel = zoomLevel;
        _cachedDurationSeconds = durationSeconds;
        _cachedHeight = height;

        geometry.Dispose();
    }

    /// <summary>
    /// Draws the cached grid lines.
    /// </summary>
    /// <param name="ds">Canvas drawing session.</param>
    /// <param name="gridColor">Color to draw the grid lines.</param>
    public void Draw(CanvasDrawingSession ds, Color gridColor)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(RenderCache));

        if (_gridLines != null)
        {
            ds.DrawCachedGeometry(_gridLines, gridColor);
        }
    }

    /// <summary>
    /// Disposes cached geometry resources.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _gridLines?.Dispose();
            _gridLines = null;
            _disposed = true;
        }
    }
}
