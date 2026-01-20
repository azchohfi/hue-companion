using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using System.Numerics;
using Windows.UI;

namespace HueWindows.Controls;

/// <summary>
/// A pure Win2D panel that renders beautiful gaussian blur glow effects.
/// This uses CanvasControl directly for the highest quality blur rendering.
/// 
/// Benefits over Composition API approach:
/// - True GPU-accelerated gaussian blur via Win2D
/// - Direct control over blur parameters and multi-pass rendering
/// - Better color accuracy and gradient support
/// - Simpler resource management (CanvasControl handles device lifecycle)
/// </summary>
public sealed partial class Win2DGlowPanel : UserControl
{
    private Color _glowColor = Colors.Transparent;
    private Color _targetColor = Colors.Transparent;
    private float _blurAmount = 15f;
    private float _cornerRadius = 16f;
    private float _glowExtent = 20f;
    private float _glowOpacity = 0f;
    private float _targetOpacity = 0f;
    private bool _isAnimating;
    private DispatcherTimer? _animationTimer;
    private const float AnimationStep = 0.08f; // ~12 frames at 60fps = 200ms
    private const float ColorAnimationStep = 0.1f;

    /// <summary>
    /// Gets or sets the glow color.
    /// </summary>
    public Color GlowColor
    {
        get => _glowColor;
        set
        {
            if (_glowColor != value)
            {
                _targetColor = value;
                StartColorAnimation();
            }
        }
    }

    /// <summary>
    /// Gets or sets the blur amount. Higher values = softer, more diffuse glow.
    /// Recommended: 12-20 for a natural light emission effect.
    /// </summary>
    public float BlurAmount
    {
        get => _blurAmount;
        set
        {
            if (_blurAmount != value)
            {
                _blurAmount = value;
                GlowCanvas?.Invalidate();
            }
        }
    }

    /// <summary>
    /// Gets or sets the corner radius to match the target element's rounded corners.
    /// </summary>
    public float CornerRadius
    {
        get => _cornerRadius;
        set
        {
            if (_cornerRadius != value)
            {
                _cornerRadius = value;
                GlowCanvas?.Invalidate();
            }
        }
    }

    /// <summary>
    /// Gets or sets how far the glow extends beyond the card bounds (in pixels).
    /// This should match the padding on the parent container.
    /// </summary>
    public float GlowExtent
    {
        get => _glowExtent;
        set
        {
            if (_glowExtent != value)
            {
                _glowExtent = value;
                GlowCanvas?.Invalidate();
            }
        }
    }

    public Win2DGlowPanel()
    {
        this.InitializeComponent();
        this.Loaded += OnLoaded;
        this.Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Initialize animation timer
        _animationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16) // ~60fps
        };
        _animationTimer.Tick += AnimationTimer_Tick;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _animationTimer?.Stop();
        _animationTimer = null;
    }

    /// <summary>
    /// Shows the glow effect with smooth animation.
    /// </summary>
    /// <param name="color">The glow color (typically the light/room accent color).</param>
    /// <param name="animate">Whether to animate the transition.</param>
    public void Show(Color color, bool animate = true)
    {
        _targetColor = color;
        _targetOpacity = 1f;

        if (animate)
        {
            StartAnimation();
        }
        else
        {
            _glowColor = color;
            _glowOpacity = 1f;
            GlowCanvas?.Invalidate();
        }
    }

    /// <summary>
    /// Hides the glow effect with smooth animation.
    /// </summary>
    /// <param name="animate">Whether to animate the transition.</param>
    public void Hide(bool animate = true)
    {
        _targetOpacity = 0f;

        if (animate)
        {
            StartAnimation();
        }
        else
        {
            _glowOpacity = 0f;
            GlowCanvas?.Invalidate();
        }
    }

    /// <summary>
    /// Updates the glow color with smooth color transition animation.
    /// </summary>
    public void UpdateColor(Color newColor)
    {
        if (_glowColor != newColor)
        {
            _targetColor = newColor;
            StartColorAnimation();
        }
    }

    private void StartAnimation()
    {
        if (!_isAnimating && _animationTimer != null)
        {
            _isAnimating = true;
            _animationTimer.Start();
        }
    }

    private void StartColorAnimation()
    {
        // For color changes, also start the animation timer
        StartAnimation();
    }

    private void AnimationTimer_Tick(object? sender, object e)
    {
        bool needsInvalidate = false;
        bool animationComplete = true;

        // Animate opacity
        if (Math.Abs(_glowOpacity - _targetOpacity) > 0.01f)
        {
            _glowOpacity = Lerp(_glowOpacity, _targetOpacity, AnimationStep);
            needsInvalidate = true;
            animationComplete = false;
        }
        else if (_glowOpacity != _targetOpacity)
        {
            _glowOpacity = _targetOpacity;
            needsInvalidate = true;
        }

        // Animate color (when showing or changing color)
        if (_glowOpacity > 0 && _glowColor != _targetColor)
        {
            _glowColor = LerpColor(_glowColor, _targetColor, ColorAnimationStep);
            needsInvalidate = true;
            animationComplete = false;
        }

        if (needsInvalidate)
        {
            GlowCanvas?.Invalidate();
        }

        if (animationComplete)
        {
            _isAnimating = false;
            _animationTimer?.Stop();
        }
    }

    private static float Lerp(float a, float b, float t)
    {
        return a + (b - a) * t;
    }

    private static Color LerpColor(Color a, Color b, float t)
    {
        return Color.FromArgb(
            (byte)(a.A + (b.A - a.A) * t),
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t));
    }

    private void GlowCanvas_CreateResources(CanvasControl sender, CanvasCreateResourcesEventArgs args)
    {
        // Resources are created on demand during Draw
        // This callback can be used for pre-caching if needed
    }

    private void GlowCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        var session = args.DrawingSession;
        var size = sender.Size;

        // Clear to transparent
        session.Clear(Colors.Transparent);

        // Don't draw if opacity is zero or size is invalid
        if (_glowOpacity <= 0.01f || size.Width <= 0 || size.Height <= 0)
            return;

        // Calculate the card rectangle (inside the glow extent padding)
        var cardRect = new Windows.Foundation.Rect(
            _glowExtent,
            _glowExtent,
            size.Width - (_glowExtent * 2),
            size.Height - (_glowExtent * 2));

        if (cardRect.Width <= 0 || cardRect.Height <= 0)
            return;

        // Create glow color with current opacity
        var glowColor = Color.FromArgb(
            (byte)(_glowOpacity * 255),
            _glowColor.R,
            _glowColor.G,
            _glowColor.B);

        // Draw multi-pass glow for natural light emission effect
        // Each pass has different blur and opacity for smooth falloff
        DrawMultiPassGlow(session, cardRect, glowColor);
    }

    private void DrawMultiPassGlow(CanvasDrawingSession session, Windows.Foundation.Rect cardRect, Color baseColor)
    {
        // Multi-pass approach creates a more natural, diffuse glow
        // Similar to how real light sources emit: intense near the source, falling off smoothly
        var passes = new[]
        {
            // (blur multiplier, opacity multiplier, expand amount)
            (1.0f, 0.6f, 0f),      // Inner glow - sharpest, most intense
            (1.5f, 0.4f, 2f),      // Mid glow
            (2.0f, 0.25f, 4f),     // Outer glow - softest, most diffuse
            (2.5f, 0.15f, 6f),     // Far outer glow - very subtle
        };

        foreach (var (blurMult, opacityMult, expand) in passes)
        {
            // Expand the rect slightly for outer passes
            var expandedRect = new Windows.Foundation.Rect(
                cardRect.X - expand,
                cardRect.Y - expand,
                cardRect.Width + (expand * 2),
                cardRect.Height + (expand * 2));

            var passColor = Color.FromArgb(
                (byte)(baseColor.A * opacityMult),
                baseColor.R,
                baseColor.G,
                baseColor.B);

            // Create a command list to draw the rounded rect
            using var commandList = new CanvasCommandList(session);
            using (var cmdSession = commandList.CreateDrawingSession())
            {
                // Draw rounded rectangle as the glow source
                cmdSession.FillRoundedRectangle(
                    expandedRect,
                    _cornerRadius + expand,
                    _cornerRadius + expand,
                    passColor);
            }

            // Apply gaussian blur
            var blurEffect = new GaussianBlurEffect
            {
                Source = commandList,
                BlurAmount = _blurAmount * blurMult,
                BorderMode = EffectBorderMode.Soft,
                Optimization = EffectOptimization.Quality
            };

            session.DrawImage(blurEffect);
        }
    }
}
