using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace HueWindows.Utilities;

/// <summary>
/// Extension methods for visual tree navigation.
/// </summary>
public static class VisualTreeExtensions
{
    /// <summary>
    /// Checks if a dependency object is a descendant of a target element in the visual tree.
    /// </summary>
    /// <param name="current">The potential descendant element.</param>
    /// <param name="target">The target ancestor element.</param>
    /// <returns>True if current is a descendant of target, false otherwise.</returns>
    public static bool IsDescendantOf(this DependencyObject? current, DependencyObject target)
    {
        while (current != null)
        {
            if (current == target) return true;
            try
            {
                current = VisualTreeHelper.GetParent(current);
            }
            catch
            {
                // In some cases (like popups) GetParent might fail
                return false;
            }
        }
        return false;
    }
}
