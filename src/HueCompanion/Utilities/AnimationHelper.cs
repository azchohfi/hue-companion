using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using HueCompanion.Constants;
using Windows.UI;

namespace HueCompanion.Utilities;

/// <summary>
/// Helper methods for creating and running common UI animations.
/// </summary>
public static class AnimationHelper
{
    /// <summary>
    /// Animates an element's entrance with a combined fade-in and slide-up effect.
    /// Uses Storyboard BeginTime for stagger delay (no Task.Delay needed).
    /// </summary>
    /// <param name="target">The element to animate.</param>
    /// <param name="delayMs">Stagger delay in milliseconds before animation starts.</param>
    /// <param name="durationMs">Optional duration. Defaults to standard duration.</param>
    public static void AnimateEntrance(UIElement target, int delayMs = 0, int durationMs = 0)
    {
        var duration = durationMs > 0 ? durationMs : AppConstants.Animation.StandardDurationMs;
        target.Opacity = 0;
        target.RenderTransform = new TranslateTransform { Y = 20 };

        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        var beginTime = TimeSpan.FromMilliseconds(delayMs);
        var animDuration = new Duration(TimeSpan.FromMilliseconds(duration));

        var opacityAnim = new DoubleAnimation
        {
            To = 1.0,
            Duration = animDuration,
            BeginTime = beginTime,
            EasingFunction = easing
        };

        var translateAnim = new DoubleAnimation
        {
            To = 0,
            Duration = animDuration,
            BeginTime = beginTime,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        var sb = new Storyboard();
        Storyboard.SetTarget(opacityAnim, target);
        Storyboard.SetTargetProperty(opacityAnim, "Opacity");
        Storyboard.SetTarget(translateAnim, target.RenderTransform);
        Storyboard.SetTargetProperty(translateAnim, "Y");
        sb.Children.Add(opacityAnim);
        sb.Children.Add(translateAnim);
        sb.Begin();
    }

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
