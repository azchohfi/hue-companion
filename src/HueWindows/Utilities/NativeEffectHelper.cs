using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using HueWindows.Controls;
using HueWindows.Core.Models;

namespace HueWindows.Utilities;

/// <summary>
/// Shared logic for creating and showing NativeEffect flyouts.
/// Used by RoomDetailPage and ScenesPage.
/// </summary>
public static class NativeEffectHelper
{
    /// <summary>
    /// Creates and shows a NativeEffectFlyout at the given button.
    /// </summary>
    /// <param name="button">The button to anchor the flyout to.</param>
    /// <param name="effect">The native effect to configure.</param>
    /// <param name="flyoutStyle">Optional FlyoutPresenter style (e.g., EffectFlyoutPresenterStyle).</param>
    /// <param name="onApply">Callback when Apply is clicked.</param>
    /// <param name="onSave">Callback when Save as Scene is clicked.</param>
    public static void ShowNativeEffectFlyout(
        Button button,
        NativeEffectInfo effect,
        Style? flyoutStyle,
        Func<NativeEffectApplyEventArgs, Task> onApply,
        Func<NativeEffectApplyEventArgs, Task> onSave)
    {
        var flyout = new Flyout
        {
            ShouldConstrainToRootBounds = false
        };

        if (flyoutStyle != null)
        {
            flyout.FlyoutPresenterStyle = flyoutStyle;
        }

        var flyoutContent = new NativeEffectFlyout { Effect = effect };
        flyoutContent.ApplyRequested += async (s, args) =>
        {
            flyout.Hide();
            await onApply(args);
        };
        flyoutContent.SaveRequested += async (s, args) =>
        {
            flyout.Hide();
            await onSave(args);
        };

        flyout.Content = flyoutContent;
        flyout.Opening += (s, args) => flyoutContent.AnimateEntrance();
        flyout.ShowAt(button);
    }
}
