using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.UI.Composition;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using System.Numerics;
using Windows.UI;

namespace HueWindows.Utilities;

/// <summary>
/// Helper class for creating true gaussian blur glow effects on UI elements.
/// Uses Win2D and Composition APIs for smooth, performant glow rendering.
/// </summary>
public sealed class GlowHelper : IDisposable
{
    private readonly UIElement _target;
    private readonly Compositor _compositor;
    private readonly CompositionGraphicsDevice _graphicsDevice;
    private readonly CanvasDevice _canvasDevice;
    
    private SpriteVisual? _glowVisual;
    private CompositionSurfaceBrush? _glowBrush;
    private CompositionDrawingSurface? _glowSurface;
    private ScalarKeyFrameAnimation? _fadeInAnimation;
    private ScalarKeyFrameAnimation? _fadeOutAnimation;
    private ColorKeyFrameAnimation? _colorAnimation;
    
    private Color _currentColor = Colors.Transparent;
    private float _blurAmount = 15f;
    private float _glowExtent = 20f;
    private float _cornerRadius = 16f;
    private bool _isDisposed;

    /// <summary>
    /// Gets or sets the blur amount for the glow effect.
    /// Higher values create a softer, more diffuse glow.
    /// </summary>
    public float BlurAmount
    {
        get => _blurAmount;
        set
        {
            if (_blurAmount != value)
            {
                _blurAmount = value;
                UpdateGlowSurface();
            }
        }
    }

    /// <summary>
    /// Gets or sets how far the glow extends beyond the element bounds.
    /// </summary>
    public float GlowExtent
    {
        get => _glowExtent;
        set
        {
            if (_glowExtent != value)
            {
                _glowExtent = value;
                UpdateGlowVisualSize();
            }
        }
    }

    /// <summary>
    /// Gets or sets the corner radius to match the target element.
    /// </summary>
    public float CornerRadius
    {
        get => _cornerRadius;
        set
        {
            if (_cornerRadius != value)
            {
                _cornerRadius = value;
                UpdateGlowSurface();
            }
        }
    }

    /// <summary>
    /// Creates a new GlowHelper for the specified target element.
    /// </summary>
    /// <param name="target">The UI element to add glow effect to.</param>
    public GlowHelper(UIElement target)
    {
        _target = target ?? throw new ArgumentNullException(nameof(target));
        
        // Get the compositor from the target element
        var targetVisual = ElementCompositionPreview.GetElementVisual(_target);
        _compositor = targetVisual.Compositor;
        
        // Create Win2D canvas device and composition graphics device
        _canvasDevice = CanvasDevice.GetSharedDevice();
        _graphicsDevice = CanvasComposition.CreateCompositionGraphicsDevice(_compositor, _canvasDevice);
        
        // Create animations
        CreateAnimations();
        
        // Initialize the glow visual
        InitializeGlowVisual();
        
        // Listen for size changes
        if (_target is FrameworkElement fe)
        {
            fe.SizeChanged += OnTargetSizeChanged;
        }
    }

    private void CreateAnimations()
    {
        // Fade in animation
        _fadeInAnimation = _compositor.CreateScalarKeyFrameAnimation();
        _fadeInAnimation.InsertKeyFrame(0f, 0f);
        _fadeInAnimation.InsertKeyFrame(1f, 1f);
        _fadeInAnimation.Duration = TimeSpan.FromMilliseconds(300);
        
        // Fade out animation
        _fadeOutAnimation = _compositor.CreateScalarKeyFrameAnimation();
        _fadeOutAnimation.InsertKeyFrame(0f, 1f);
        _fadeOutAnimation.InsertKeyFrame(1f, 0f);
        _fadeOutAnimation.Duration = TimeSpan.FromMilliseconds(300);
        
        // Color transition animation (template - actual colors set at runtime)
        _colorAnimation = _compositor.CreateColorKeyFrameAnimation();
        _colorAnimation.Duration = TimeSpan.FromMilliseconds(300);
    }

    private void InitializeGlowVisual()
    {
        // Create the glow visual as a sprite visual
        _glowVisual = _compositor.CreateSpriteVisual();
        _glowVisual.Opacity = 0f;
        
        // Position behind the target
        var targetVisual = ElementCompositionPreview.GetElementVisual(_target);
        
        // Insert the glow visual behind the target's content
        ElementCompositionPreview.SetElementChildVisual(_target, _glowVisual);
        
        UpdateGlowVisualSize();
    }

    private void UpdateGlowVisualSize()
    {
        if (_glowVisual == null || _target is not FrameworkElement fe) return;
        
        var width = (float)fe.ActualWidth;
        var height = (float)fe.ActualHeight;
        
        if (width <= 0 || height <= 0) return;
        
        // Make glow visual larger to extend beyond bounds
        var glowWidth = width + (_glowExtent * 2);
        var glowHeight = height + (_glowExtent * 2);
        
        _glowVisual.Size = new Vector2(glowWidth, glowHeight);
        // Offset to center the glow behind the element
        _glowVisual.Offset = new Vector3(-_glowExtent, -_glowExtent, 0);
        
        UpdateGlowSurface();
    }

    private void UpdateGlowSurface()
    {
        if (_glowVisual == null || _isDisposed) return;
        
        var size = _glowVisual.Size;
        if (size.X <= 0 || size.Y <= 0) return;
        
        try
        {
            // Create or recreate the drawing surface
            _glowSurface?.Dispose();
            _glowSurface = _graphicsDevice.CreateDrawingSurface(
                new Windows.Foundation.Size(size.X, size.Y),
                Microsoft.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized,
                Microsoft.Graphics.DirectX.DirectXAlphaMode.Premultiplied);
            
            // Draw the glow
            DrawGlow(_currentColor);
            
            // Create brush from surface
            _glowBrush?.Dispose();
            _glowBrush = _compositor.CreateSurfaceBrush(_glowSurface);
            _glowBrush.Stretch = CompositionStretch.Fill;
            
            _glowVisual.Brush = _glowBrush;
        }
        catch
        {
            // Handle graphics device issues gracefully
        }
    }

    private void DrawGlow(Color color)
    {
        if (_glowSurface == null || _isDisposed) return;
        
        try
        {
            using var session = CanvasComposition.CreateDrawingSession(_glowSurface);
            session.Clear(Colors.Transparent);
            
            var size = _glowVisual!.Size;
            
            // Create a rounded rectangle that represents the card shape
            // This will be the source for our blur
            var cardRect = new Windows.Foundation.Rect(
                _glowExtent,
                _glowExtent,
                size.X - (_glowExtent * 2),
                size.Y - (_glowExtent * 2));
            
            // Create glow color with boosted opacity for visibility
            var glowColor = Color.FromArgb(
                (byte)Math.Min(255, color.A + 100), // Boost alpha for glow visibility
                color.R,
                color.G,
                color.B);
            
            // Draw multiple passes with decreasing opacity and increasing blur
            // for a more natural light emission effect
            for (int i = 3; i >= 0; i--)
            {
                var layerOpacity = (float)(0.3 - (i * 0.05));
                var layerBlur = _blurAmount + (i * 5);
                var layerColor = Color.FromArgb(
                    (byte)(glowColor.A * layerOpacity),
                    glowColor.R,
                    glowColor.G,
                    glowColor.B);
                
                // Create command list for the shape
                using var commandList = new CanvasCommandList(session);
                using (var cmdSession = commandList.CreateDrawingSession())
                {
                    // Draw rounded rectangle
                    cmdSession.FillRoundedRectangle(
                        cardRect,
                        _cornerRadius,
                        _cornerRadius,
                        layerColor);
                }
                
                // Apply gaussian blur
                var blurEffect = new GaussianBlurEffect
                {
                    Source = commandList,
                    BlurAmount = layerBlur,
                    BorderMode = EffectBorderMode.Soft
                };
                
                session.DrawImage(blurEffect);
            }
        }
        catch
        {
            // Handle drawing errors gracefully
        }
    }

    private void OnTargetSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateGlowVisualSize();
    }

    /// <summary>
    /// Shows the glow effect with the specified color.
    /// </summary>
    /// <param name="color">The glow color.</param>
    /// <param name="animate">Whether to animate the transition.</param>
    public void Show(Color color, bool animate = true)
    {
        if (_glowVisual == null || _isDisposed) return;
        
        // Update glow color if changed
        if (_currentColor != color)
        {
            _currentColor = color;
            UpdateGlowSurface();
        }
        
        if (animate && _fadeInAnimation != null)
        {
            _glowVisual.StartAnimation("Opacity", _fadeInAnimation);
        }
        else
        {
            _glowVisual.Opacity = 1f;
        }
    }

    /// <summary>
    /// Hides the glow effect.
    /// </summary>
    /// <param name="animate">Whether to animate the transition.</param>
    public void Hide(bool animate = true)
    {
        if (_glowVisual == null || _isDisposed) return;
        
        if (animate && _fadeOutAnimation != null)
        {
            _glowVisual.StartAnimation("Opacity", _fadeOutAnimation);
        }
        else
        {
            _glowVisual.Opacity = 0f;
        }
    }

    /// <summary>
    /// Updates the glow color with smooth animation.
    /// </summary>
    /// <param name="newColor">The new glow color.</param>
    public void UpdateColor(Color newColor)
    {
        if (_glowVisual == null || _isDisposed || _currentColor == newColor) return;
        
        _currentColor = newColor;
        UpdateGlowSurface();
    }

    /// <summary>
    /// Releases all resources used by the GlowHelper.
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        
        if (_target is FrameworkElement fe)
        {
            fe.SizeChanged -= OnTargetSizeChanged;
        }
        
        _glowSurface?.Dispose();
        _glowBrush?.Dispose();
        _glowVisual?.Dispose();
        _fadeInAnimation?.Dispose();
        _fadeOutAnimation?.Dispose();
        _colorAnimation?.Dispose();
        // Note: Don't dispose _canvasDevice as it's shared
        // Note: Don't dispose _graphicsDevice as it may be shared
    }
}
