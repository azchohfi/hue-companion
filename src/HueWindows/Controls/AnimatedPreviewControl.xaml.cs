using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using HueWindows.Core.Models;
using Windows.UI;

namespace HueWindows.Controls;

/// <summary>
/// Control that displays an animated preview of a scene's colors.
/// </summary>
public sealed partial class AnimatedPreviewControl : UserControl
{
    private DispatcherTimer? _animationTimer;
    private double _elapsed;
    private readonly List<Ellipse> _lightDots = new();

    public static readonly DependencyProperty SceneProperty =
        DependencyProperty.Register(nameof(Scene), typeof(AnimatedSceneModel),
            typeof(AnimatedPreviewControl), new PropertyMetadata(null, OnSceneChanged));

    public AnimatedSceneModel? Scene
    {
        get => (AnimatedSceneModel?)GetValue(SceneProperty);
        set => SetValue(SceneProperty, value);
    }

    public static readonly DependencyProperty IsAnimatingProperty =
        DependencyProperty.Register(nameof(IsAnimating), typeof(bool),
            typeof(AnimatedPreviewControl), new PropertyMetadata(true, OnIsAnimatingChanged));

    public bool IsAnimating
    {
        get => (bool)GetValue(IsAnimatingProperty);
        set => SetValue(IsAnimatingProperty, value);
    }

    public AnimatedPreviewControl()
    {
        InitializeComponent();
    }

    private static void OnSceneChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AnimatedPreviewControl ctrl)
            ctrl.SetupPreview();
    }

    private static void OnIsAnimatingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AnimatedPreviewControl ctrl)
        {
            if ((bool)e.NewValue)
                ctrl.StartAnimation();
            else
                ctrl.StopAnimation();
        }
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        SetupPreview();
        if (IsAnimating) StartAnimation();
    }

    private void UserControl_Unloaded(object sender, RoutedEventArgs e)
    {
        StopAnimation();
    }

    private void SetupPreview()
    {
        PreviewCanvas.Children.Clear();
        _lightDots.Clear();

        if (Scene?.PaletteColors == null || Scene.PaletteColors.Count == 0)
            return;

        // Create 4 dots representing lights
        var dotCount = Math.Min(4, Math.Max(2, Scene.PaletteColors.Count));
        var spacing = 70.0 / (dotCount + 1);

        for (int i = 0; i < dotCount; i++)
        {
            var dot = new Ellipse
            {
                Width = 12,
                Height = 12,
                Fill = new SolidColorBrush(Colors.White)
            };
            Canvas.SetLeft(dot, 5 + spacing * (i + 1) - 6);
            Canvas.SetTop(dot, 14);
            PreviewCanvas.Children.Add(dot);
            _lightDots.Add(dot);
        }

        UpdateColors(0);
    }

    private void StartAnimation()
    {
        if (_animationTimer != null) return;

        _animationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(66) // ~15fps
        };
        _animationTimer.Tick += OnAnimationTick;
        _animationTimer.Start();
    }

    private void StopAnimation()
    {
        _animationTimer?.Stop();
        _animationTimer = null;
    }

    private void OnAnimationTick(object? sender, object e)
    {
        if (Scene == null) return;

        var duration = GetSceneDuration();
        _elapsed = (_elapsed + 0.066) % duration;
        UpdateColors(_elapsed);
    }

    private double GetSceneDuration()
    {
        var keyframeAnim = Scene?.Animations?.FirstOrDefault(a => a.Type == AnimationType.Keyframe);
        return keyframeAnim?.DurationSeconds ?? 10;
    }

    private void UpdateColors(double time)
    {
        if (Scene?.PaletteColors == null || _lightDots.Count == 0)
            return;

        var colors = Scene.PaletteColors;
        var duration = GetSceneDuration();
        var progress = time / duration;

        for (int i = 0; i < _lightDots.Count; i++)
        {
            // Offset each light slightly for visual interest
            var offset = (progress + i * 0.25) % 1.0;
            var colorIndex = (int)(offset * colors.Count) % colors.Count;
            var nextIndex = (colorIndex + 1) % colors.Count;
            var t = (offset * colors.Count) % 1.0;

            var c1 = colors[colorIndex];
            var c2 = colors[nextIndex];

            // Simple CIE xy to RGB approximation
            var rgb1 = HueColorToRgb(c1);
            var rgb2 = HueColorToRgb(c2);

            var r = (byte)(rgb1.R + (rgb2.R - rgb1.R) * t);
            var g = (byte)(rgb1.G + (rgb2.G - rgb1.G) * t);
            var b = (byte)(rgb1.B + (rgb2.B - rgb1.B) * t);

            _lightDots[i].Fill = new SolidColorBrush(Color.FromArgb(255, r, g, b));
        }
    }

    private static (byte R, byte G, byte B) HueColorToRgb(HueColor color)
    {
        // Simplified CIE xy to RGB
        var x = color.X;
        var y = color.Y;
        var z = 1.0 - x - y;
        var Y = 1.0;
        var X = (Y / y) * x;
        var Z = (Y / y) * z;

        var r = X * 3.2406 - Y * 1.5372 - Z * 0.4986;
        var g = -X * 0.9689 + Y * 1.8758 + Z * 0.0415;
        var b = X * 0.0557 - Y * 0.2040 + Z * 1.0570;

        r = Math.Max(0, Math.Min(1, r));
        g = Math.Max(0, Math.Min(1, g));
        b = Math.Max(0, Math.Min(1, b));

        return ((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
    }
}
