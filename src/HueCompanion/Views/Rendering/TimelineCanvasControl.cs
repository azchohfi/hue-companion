using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Input;
using Windows.UI.Core;

namespace HueCompanion.Views.Rendering;

/// <summary>
/// Helper class to manage cursor for CanvasControl via reflection.
/// Required because ProtectedCursor is protected and CanvasControl is sealed.
/// </summary>
public static class CanvasControlCursorHelper
{
    /// <summary>
    /// Sets the cursor for a CanvasControl using WinUI 3 InputCursor API.
    /// </summary>
    /// <param name="canvasControl">The CanvasControl to set cursor on</param>
    /// <param name="cursor">CoreCursor specifying cursor type (Arrow, Hand, SizeAll)</param>
    public static void SetCursor(CanvasControl canvasControl, CoreCursor cursor)
    {
        try
        {
            // Convert CoreCursor type to InputSystemCursorShape
            var shape = cursor.Type switch
            {
                CoreCursorType.Arrow => InputSystemCursorShape.Arrow,
                CoreCursorType.Hand => InputSystemCursorShape.Hand,
                CoreCursorType.SizeAll => InputSystemCursorShape.SizeAll,
                CoreCursorType.IBeam => InputSystemCursorShape.IBeam,
                CoreCursorType.Wait => InputSystemCursorShape.Wait,
                CoreCursorType.SizeNorthSouth => InputSystemCursorShape.SizeNorthSouth,
                CoreCursorType.SizeWestEast => InputSystemCursorShape.SizeWestEast,
                _ => InputSystemCursorShape.Arrow
            };

            // Create WinUI 3 InputCursor and set via reflection
            var inputCursor = InputSystemCursor.Create(shape);

            var propertyInfo = typeof(CanvasControl).GetProperty("ProtectedCursor",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            if (propertyInfo != null)
            {
                propertyInfo.SetValue(canvasControl, inputCursor);
            }
        }
        catch
        {
            // Silently ignore cursor setting failures - non-critical UI feature
        }
    }
}
