using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using HueWindows.Constants;
using Windows.UI;

namespace HueWindows.Utilities;

/// <summary>
/// Helper methods for creating and running common UI animations.
/// </summary>
public static class AnimationHelper
{
    /// <summary>
    /// Animates the opacity of a UIElement.
    /// </summary>
    /// <param name="target">The element to animate.</param>
    /// <param name="toOpacity">The target opacity value (0.0 to 1.0).</param>
    /// <param name="durationMs">Optional duration in milliseconds. Defaults to standard duration.</param>
    public static void AnimateOpacity(UIElement target, double toOpacity, int? durationMs = null)
    {
        var animation = new DoubleAnimation
        {
            To = toOpacity,
            Duration = new Duration(TimeSpan.FromMilliseconds(durationMs ?? AppConstants.Animation.StandardDurationMs)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, "Opacity");
        storyboard.Begin();
    }

    /// <summary>
    /// Animates the color of a SolidColorBrush.
    /// </summary>
    /// <param name="brush">The brush to animate.</param>
    /// <param name="fromColor">The starting color.</param>
    /// <param name="toColor">The target color.</param>
    /// <param name="durationMs">Optional duration in milliseconds. Defaults to standard duration.</param>
    public static void AnimateColor(SolidColorBrush brush, Color fromColor, Color toColor, int? durationMs = null)
    {
        var animation = new ColorAnimation
        {
            From = fromColor,
            To = toColor,
            Duration = new Duration(TimeSpan.FromMilliseconds(durationMs ?? AppConstants.Animation.StandardDurationMs)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            EnableDependentAnimation = true
        };

        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        Storyboard.SetTarget(animation, brush);
        Storyboard.SetTargetProperty(animation, "Color");
        storyboard.Begin();
    }
}
