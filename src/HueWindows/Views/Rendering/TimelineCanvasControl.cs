using Microsoft.Graphics.Canvas.UI.Xaml;
using Windows.UI.Core;

namespace HueWindows.Views.Rendering;

/// <summary>
/// Helper class to manage cursor for CanvasControl via reflection.
/// Required because ProtectedCursor is protected and CanvasControl is sealed.
/// </summary>
public static class CanvasControlCursorHelper
{
    /// <summary>
    /// Sets the cursor for a CanvasControl using reflection to access ProtectedCursor.
    /// </summary>
    /// <param name="canvasControl">The CanvasControl to set cursor on</param>
    /// <param name="cursor">CoreCursor to display (Arrow, Hand, SizeAll)</param>
    public static void SetCursor(CanvasControl canvasControl, CoreCursor cursor)
    {
        // Use reflection to access the protected ProtectedCursor property
        var propertyInfo = typeof(CanvasControl).GetProperty("ProtectedCursor",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        if (propertyInfo != null)
        {
            propertyInfo.SetValue(canvasControl, cursor);
        }
    }
}
