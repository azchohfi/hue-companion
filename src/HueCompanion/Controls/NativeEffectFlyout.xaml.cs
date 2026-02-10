using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using HueCompanion.Constants;
using HueCompanion.Core.Models;

namespace HueCompanion.Controls;

/// <summary>
/// Flyout control for configuring and applying native Hue effects.
/// </summary>
public sealed partial class NativeEffectFlyout : UserControl
{
    private NativeEffectInfo? _effect;

    /// <summary>
    /// Event raised when user clicks Apply.
    /// </summary>
    public event EventHandler<NativeEffectApplyEventArgs>? ApplyRequested;

    /// <summary>
    /// Event raised when user clicks Save as Scene.
    /// </summary>
    public event EventHandler<NativeEffectApplyEventArgs>? SaveRequested;

    /// <summary>
    /// Gets or sets the effect to configure.
    /// </summary>
    public NativeEffectInfo? Effect
    {
        get => _effect;
        set
        {
            _effect = value;
            UpdateEffectDisplay();
        }
    }

    /// <summary>
    /// Gets the current speed value (0.0-1.0).
    /// </summary>
    public double Speed => SpeedSlider.Value / 100.0;

    /// <summary>
    /// Gets the current brightness value (0.0-1.0).
    /// </summary>
    public double Brightness => BrightnessSlider.Value / 100.0;

    public NativeEffectFlyout()
    {
        this.InitializeComponent();
        UpdateValueLabels();
    }

    private void UpdateEffectDisplay()
    {
        if (_effect == null) return;

        EffectNameText.Text = _effect.Name;
        EffectDescriptionText.Text = _effect.Description;
        EffectIcon.Glyph = _effect.IconGlyph;
        SpeedSlider.Value = _effect.DefaultSpeed * 100;
        BrightnessSlider.Value = _effect.DefaultBrightness * 100;
        UpdateValueLabels();
    }

    private void UpdateValueLabels()
    {
        // Guard against calls during XAML initialization
        if (SpeedValueText == null || BrightnessValueText == null ||
            SpeedSlider == null || BrightnessSlider == null)
            return;

        SpeedValueText.Text = $"{(int)SpeedSlider.Value}%";
        BrightnessValueText.Text = $"{(int)BrightnessSlider.Value}%";
    }

    private void SpeedSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        UpdateValueLabels();
    }

    private void BrightnessSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        UpdateValueLabels();
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_effect == null) return;
        ApplyRequested?.Invoke(this, new NativeEffectApplyEventArgs(_effect, Speed, Brightness));
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_effect == null) return;
        SaveRequested?.Invoke(this, new NativeEffectApplyEventArgs(_effect, Speed, Brightness));
    }

    /// <summary>
    /// Plays the entrance animation (scale + fade in).
    /// </summary>
    public void AnimateEntrance()
    {
        // Set initial state
        RootGrid.Opacity = 0;
        RootScaleTransform.ScaleX = 0.95;
        RootScaleTransform.ScaleY = 0.95;

        var duration = TimeSpan.FromMilliseconds(AppConstants.Animation.FastDurationMs);
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };

        // Opacity animation
        var opacityAnim = new DoubleAnimation
        {
            To = 1.0,
            Duration = new Duration(duration),
            EasingFunction = easing
        };

        // Scale X animation
        var scaleXAnim = new DoubleAnimation
        {
            To = 1.0,
            Duration = new Duration(duration),
            EasingFunction = easing
        };

        // Scale Y animation
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

/// <summary>
/// Event args for native effect apply/save requests.
/// </summary>
public class NativeEffectApplyEventArgs : EventArgs
{
    /// <summary>
    /// The effect to apply.
    /// </summary>
    public NativeEffectInfo Effect { get; }

    /// <summary>
    /// Effect speed (0.0-1.0).
    /// </summary>
    public double Speed { get; }

    /// <summary>
    /// Brightness level (0.0-1.0).
    /// </summary>
    public double Brightness { get; }

    public NativeEffectApplyEventArgs(NativeEffectInfo effect, double speed, double brightness)
    {
        Effect = effect;
        Speed = speed;
        Brightness = brightness;
    }
}
