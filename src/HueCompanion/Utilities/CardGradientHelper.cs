using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace HueCompanion.Utilities;

/// <summary>
/// Provides shared card gradient brushes used by RoomCard, LightCard,
/// RoomDetailPage, and LightDetailPage.
/// </summary>
public static class CardGradientHelper
{
    /// <summary>
    /// Creates the standard diagonal gradient background for card-style containers.
    /// </summary>
    /// <param name="isDark">Whether the current theme is dark.</param>
    /// <returns>A diagonal LinearGradientBrush suitable for card backgrounds.</returns>
    public static LinearGradientBrush CreateCardGradient(bool isDark)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Windows.Foundation.Point(0, 0),
            EndPoint = new Windows.Foundation.Point(1, 1)
        };

        if (isDark)
        {
            brush.GradientStops.Add(new GradientStop { Color = ColorHelper.FromArgb(255, 26, 26, 30), Offset = 0 });
            brush.GradientStops.Add(new GradientStop { Color = ColorHelper.FromArgb(255, 37, 37, 40), Offset = 0.5 });
            brush.GradientStops.Add(new GradientStop { Color = ColorHelper.FromArgb(255, 30, 30, 34), Offset = 1 });
        }
        else
        {
            brush.GradientStops.Add(new GradientStop { Color = ColorHelper.FromArgb(255, 245, 245, 245), Offset = 0 });
            brush.GradientStops.Add(new GradientStop { Color = ColorHelper.FromArgb(255, 250, 250, 250), Offset = 0.5 });
            brush.GradientStops.Add(new GradientStop { Color = ColorHelper.FromArgb(255, 248, 248, 248), Offset = 1 });
        }

        return brush;
    }
}
