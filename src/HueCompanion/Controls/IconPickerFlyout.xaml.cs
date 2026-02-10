using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using HueCompanion.Constants;
using HueCompanion.Core.Utilities;

namespace HueCompanion.Controls;

/// <summary>
/// Flyout control for choosing a custom room icon.
/// </summary>
public sealed partial class IconPickerFlyout : UserControl
{
    /// <summary>
    /// Event raised when an icon is selected.
    /// </summary>
    public event EventHandler<string>? IconSelected;

    /// <summary>
    /// Event raised when the user clicks Reset to Default.
    /// </summary>
    public event EventHandler? ResetRequested;

    public IconPickerFlyout()
    {
        this.InitializeComponent();
        LoadIcons();
    }

    private void LoadIcons()
    {
        var icons = RoomIconHelper.GetAllIcons();
        foreach (var icon in icons)
        {
            var fontIcon = new FontIcon
            {
                Glyph = icon.Glyph,
                FontSize = 18
            };
            IconGrid.Items.Add(fontIcon);
        }
    }

    private void IconGrid_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is FontIcon fontIcon)
        {
            IconSelected?.Invoke(this, fontIcon.Glyph);
        }
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        ResetRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Plays the entrance animation (scale + fade in).
    /// </summary>
    public void AnimateEntrance()
    {
        RootGrid.Opacity = 0;
        RootScaleTransform.ScaleX = 0.95;
        RootScaleTransform.ScaleY = 0.95;

        var duration = TimeSpan.FromMilliseconds(AppConstants.Animation.FastDurationMs);
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };

        var opacityAnim = new DoubleAnimation
        {
            To = 1.0,
            Duration = new Duration(duration),
            EasingFunction = easing
        };

        var scaleXAnim = new DoubleAnimation
        {
            To = 1.0,
            Duration = new Duration(duration),
            EasingFunction = easing
        };

        var scaleYAnim = new DoubleAnimation
        {
            To = 1.0,
            Duration = new Duration(duration),
            EasingFunction = easing
        };

        var storyboard = new Storyboard();
        storyboard.Children.Add(opacityAnim);
        storyboard.Children.Add(scaleXAnim);
        storyboard.Children.Add(scaleYAnim);

        Storyboard.SetTarget(opacityAnim, RootGrid);
        Storyboard.SetTargetProperty(opacityAnim, "Opacity");
        Storyboard.SetTarget(scaleXAnim, RootScaleTransform);
        Storyboard.SetTargetProperty(scaleXAnim, "ScaleX");
        Storyboard.SetTarget(scaleYAnim, RootScaleTransform);
        Storyboard.SetTargetProperty(scaleYAnim, "ScaleY");

        storyboard.Begin();
    }
}
