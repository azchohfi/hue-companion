using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using System.Numerics;
using Windows.Foundation;
using Windows.UI;

namespace HueWindows.Controls;

/// <summary>
/// Renders a beautiful gaussian blur glow effect behind cards using Win2D.
/// Features smooth color and opacity animations for a premium light-emission aesthetic.
/// </summary>
public sealed partial class GlowEffect : UserControl
{
    // Glow configuration
    private const float DefaultBlurAmount = 18f;
    private const float DefaultCornerRadius = 16f;
    private const float GlowExtension = 20f; // How far glow extends beyond card bounds

    // Current state
    private Color _targetColor = Colors.Transparent;
    private Color _currentColor = Colors.Transparent;
    private float _targetOpacity = 0f;
    private float _currentOpacity = 0f;
    private bool _isAnimating = false;
    private bool _resourcesCreated = false;

    // Animation timing
    private const double AnimationDurationMs = 250;
    private DateTime _animationStartTime;
    private Color _animationStartColor;
    private float _animationStartOpacity;

    public GlowEffect()
    {
        this.InitializeComponent();
        this.Loaded += OnLoaded;
        this.Unloaded += OnUnloaded;
    }

    #region Dependency Properties

    /// <summary>
    /// The color of the glow effect.
    /// </summary>
    public static readonly DependencyProperty GlowColorProperty = DependencyProperty.Register(
        nameof(GlowColor),
        typeof(Color),
        typeof(GlowEffect),
        new PropertyMetadata(Colors.Transparent, OnGlowColorChanged));

    public Color GlowColor
    {
        get => (Color)GetValue(GlowColorProperty);
        set => SetValue(GlowColorProperty, value);
    }

    private static void OnGlowColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GlowEffect effect && e.NewValue is Color newColor)
        {
            effect.AnimateToColor(newColor);
        }
    }

    /// <summary>
    /// Whether the glow effect is visible (animates in/out).
    /// </summary>
    public static readonly DependencyProperty IsGlowingProperty = DependencyProperty.Register(
        nameof(IsGlowing),
        typeof(bool),
        typeof(GlowEffect),
        new PropertyMetadata(false, OnIsGlowingChanged));

    public bool IsGlowing
    {
        get => (bool)GetValue(IsGlowingProperty);
        set => SetValue(IsGlowingProperty, value);
    }

    private static void OnIsGlowingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GlowEffect effect && e.NewValue is bool isGlowing)
        {
            effect.AnimateToOpacity(isGlowing ? 1f : 0f);
        }
    }

    /// <summary>
    /// The blur amount for the gaussian blur effect.
    /// </summary>
    public static readonly DependencyProperty BlurAmountProperty = DependencyProperty.Register(
        nameof(BlurAmount),
        typeof(float),
        typeof(GlowEffect),
        new PropertyMetadata(DefaultBlurAmount, OnBlurAmountChanged));

    public float BlurAmount
    {
        get => (float)GetValue(BlurAmountProperty);
        set => SetValue(BlurAmountProperty, value);
    }

    private static void OnBlurAmountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GlowEffect effect)
        {
            effect.GlowCanvas?.Invalidate();
        }
    }

    /// <summary>
    /// The corner radius for the glow shape.
    /// </summary>
    public static readonly DependencyProperty GlowCornerRadiusProperty = DependencyProperty.Register(
        nameof(GlowCornerRadius),
        typeof(float),
        typeof(GlowEffect),
        new PropertyMetadata(DefaultCornerRadius, OnGlowCornerRadiusChanged));

    public float GlowCornerRadius
    {
        get => (float)GetValue(GlowCornerRadiusProperty);
        set => SetValue(GlowCornerRadiusProperty, value);
    }

    private static void OnGlowCornerRadiusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GlowEffect effect)
        {
            effect.GlowCanvas?.Invalidate();
        }
    }

    #endregion

    #region Lifecycle

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Initial state based on current properties
        _targetColor = GlowColor;
        _currentColor = GlowColor;
        _targetOpacity = IsGlowing ? 1f : 0f;
        _currentOpacity = _targetOpacity;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Clean up Win2D resources
        _isAnimating = false;
        try
        {
            GlowCanvas?.RemoveFromVisualTree();
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    #endregion

    #region Win2D Events

    private void GlowCanvas_CreateResources(CanvasControl sender, CanvasCreateResourcesEventArgs args)
    {
        _resourcesCreated = true;
    }

    private void GlowCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        if (!_resourcesCreated) return;

        // Update animation state
        UpdateAnimationState();

        // Skip rendering if invisible
        if (_currentOpacity <= 0.001f) return;

        var width = (float)sender.ActualWidth;
        var height = (float)sender.ActualHeight;

        if (width <= 0 || height <= 0) return;

        // Calculate glow rectangle (slightly larger than the card for soft falloff)
        var glowRect = new Rect(
            GlowExtension,
            GlowExtension,
            width - (GlowExtension * 2),
            height - (GlowExtension * 2));

        // Create the glow color with current animated opacity
        var glowColor = Color.FromArgb(
            (byte)(_currentOpacity * 255),
            _currentColor.R,
            _currentColor.G,
            _currentColor.B);

        try
        {
            // Create a command list to draw the base shape
            using var commandList = new CanvasCommandList(sender);
            using (var ds = commandList.CreateDrawingSession())
            {
                // Draw rounded rectangle as the glow source
                ds.FillRoundedRectangle(
                    glowRect,
                    GlowCornerRadius,
                    GlowCornerRadius,
                    glowColor);
            }

            // Apply gaussian blur effect for smooth glow
            var blurEffect = new GaussianBlurEffect
            {
                Source = commandList,
                BlurAmount = BlurAmount,
                BorderMode = EffectBorderMode.Soft,
                Optimization = EffectOptimization.Speed
            };

            // Draw the blurred glow
            args.DrawingSession.DrawImage(blurEffect);
        }
        catch (Exception)
        {
            // Handle device lost gracefully - will recreate on next frame
        }

        // Continue animation loop if needed
        if (_isAnimating)
        {
            sender.Invalidate();
        }
    }

    #endregion

    #region Animation

    private void AnimateToColor(Color newColor)
    {
        if (_currentColor == newColor && _targetColor == newColor)
            return;

        _animationStartColor = _currentColor;
        _targetColor = newColor;
        StartAnimation();
    }

    private void AnimateToOpacity(float newOpacity)
    {
        if (Math.Abs(_currentOpacity - newOpacity) < 0.001f && Math.Abs(_targetOpacity - newOpacity) < 0.001f)
            return;

        _animationStartOpacity = _currentOpacity;
        _targetOpacity = newOpacity;
        StartAnimation();
    }

    private void StartAnimation()
    {
        _animationStartTime = DateTime.Now;
        _animationStartColor = _currentColor;
        _animationStartOpacity = _currentOpacity;
        _isAnimating = true;
        GlowCanvas?.Invalidate();
    }

    private void UpdateAnimationState()
    {
        if (!_isAnimating) return;

        var elapsed = (DateTime.Now - _animationStartTime).TotalMilliseconds;
        var progress = Math.Min(1.0, elapsed / AnimationDurationMs);

        // Use ease-out cubic for smooth deceleration
        var easedProgress = 1 - Math.Pow(1 - progress, 3);

        // Interpolate color
        _currentColor = InterpolateColor(_animationStartColor, _targetColor, (float)easedProgress);

        // Interpolate opacity
        _currentOpacity = (float)(_animationStartOpacity + (_targetOpacity - _animationStartOpacity) * easedProgress);

        // Check if animation complete
        if (progress >= 1.0)
        {
            _currentColor = _targetColor;
            _currentOpacity = _targetOpacity;
            _isAnimating = false;
        }
    }

    private static Color InterpolateColor(Color from, Color to, float t)
    {
        return Color.FromArgb(
            (byte)(from.A + (to.A - from.A) * t),
            (byte)(from.R + (to.R - from.R) * t),
            (byte)(from.G + (to.G - from.G) * t),
            (byte)(from.B + (to.B - from.B) * t));
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Immediately sets the glow state without animation.
    /// </summary>
    public void SetImmediateState(Color color, bool isGlowing)
    {
        _currentColor = color;
        _targetColor = color;
        _currentOpacity = isGlowing ? 1f : 0f;
        _targetOpacity = _currentOpacity;
        _isAnimating = false;
        GlowCanvas?.Invalidate();
    }

    /// <summary>
    /// Forces a redraw of the glow effect.
    /// </summary>
    public void Invalidate()
    {
        GlowCanvas?.Invalidate();
    }

    #endregion
}
