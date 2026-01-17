using HueWindows.Core.Models;

namespace HueWindows.Core.Services;

/// <summary>
/// Provides easing functions for smooth animation transitions.
/// </summary>
public static class Easing
{
    /// <summary>
    /// Applies the specified easing function to a normalized progress value (0.0 to 1.0).
    /// </summary>
    /// <param name="t">Normalized time/progress value (0.0 to 1.0).</param>
    /// <param name="style">The easing transition style to apply.</param>
    /// <returns>The eased progress value.</returns>
    public static double Apply(double t, TransitionStyle style)
    {
        return style switch
        {
            TransitionStyle.Linear => t,
            TransitionStyle.EaseIn => t * t,
            TransitionStyle.EaseOut => 1 - (1 - t) * (1 - t),
            TransitionStyle.EaseInOut => t < 0.5 
                ? 2 * t * t 
                : 1 - Math.Pow(-2 * t + 2, 2) / 2,
            TransitionStyle.Instant => t >= 1.0 ? 1.0 : 0.0,
            _ => t
        };
    }
}
