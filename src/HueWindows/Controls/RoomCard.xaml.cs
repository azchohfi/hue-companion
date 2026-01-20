using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using HueWindows.Constants;
using HueWindows.Core.ViewModels;
using HueWindows.Utilities;
using System.Numerics;
using Windows.UI;

namespace HueWindows.Controls;

/// <summary>
/// Widget-like room card control for the dashboard.
/// Supports tap to navigate and drag to adjust brightness.
/// Features Composition-based glow effect when active.
/// </summary>
public sealed partial class RoomCard : UserControl
{
    private bool _isDragging;
    private double _startBrightness;
    private double _cumulativeDeltaY;
    private DateTime _lastBrightnessUpdate = DateTime.MinValue;
    private double _pendingBrightness;
    private bool _isLoaded;
    private bool _isHovering;

    // Current accent color
    private Color _accentColor = Colors.White;
    private SolidColorBrush? _iconGlowBrush;
    private Color _currentIconGlowColor = Colors.Transparent;

    // Composition shadow
    private SpriteVisual? _shadowVisual;
    private DropShadow? _dropShadow;

    public IRoomCardViewModel? ViewModel => DataContext as IRoomCardViewModel;

    public RoomCard()
    {
        this.InitializeComponent();
        this.DataContextChanged += OnDataContextChanged;
        this.Loaded += OnLoaded;
        this.SizeChanged += OnSizeChanged;
        this.ActualThemeChanged += OnActualThemeChanged;
    }

    private void OnActualThemeChanged(FrameworkElement sender, object args)
    {
        ApplyThemeBackground();
    }

    private void ApplyThemeBackground()
    {
        var isDark = ActualTheme == ElementTheme.Dark;

        if (_isHovering)
        {
            CardRoot.Background = CreateHoverGradient(isDark);
        }
        else
        {
            CardRoot.Background = CreateCardGradient(isDark);
        }
    }

    private static LinearGradientBrush CreateCardGradient(bool isDark)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Windows.Foundation.Point(0, 0),
            EndPoint = new Windows.Foundation.Point(1, 1)
        };

        if (isDark)
        {
            brush.GradientStops.Add(new GradientStop { Color = ColorHelper.FromArgb(255, 26, 26, 30), Offset = 0 });
            brush.GradientStops.Add(new GradientStop { Color = ColorHelper.FromArgb(255, 37, 37, 40), Offset = 0.5 });
            brush.GradientStops.Add(new GradientStop { Color = ColorHelper.FromArgb(255, 30, 30, 34), Offset = 1 });
        }
        else
        {
            brush.GradientStops.Add(new GradientStop { Color = ColorHelper.FromArgb(255, 245, 245, 245), Offset = 0 });
            brush.GradientStops.Add(new GradientStop { Color = ColorHelper.FromArgb(255, 250, 250, 250), Offset = 0.5 });
            brush.GradientStops.Add(new GradientStop { Color = ColorHelper.FromArgb(255, 248, 248, 248), Offset = 1 });
        }

        return brush;
    }

    private static LinearGradientBrush CreateHoverGradient(bool isDark)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Windows.Foundation.Point(0, 0),
            EndPoint = new Windows.Foundation.Point(1, 1)
        };

        if (isDark)
        {
            brush.GradientStops.Add(new GradientStop { Color = ColorHelper.FromArgb(255, 34, 34, 38), Offset = 0 });
            brush.GradientStops.Add(new GradientStop { Color = ColorHelper.FromArgb(255, 45, 45, 48), Offset = 0.5 });
            brush.GradientStops.Add(new GradientStop { Color = ColorHelper.FromArgb(255, 38, 38, 41), Offset = 1 });
        }
        else
        {
            brush.GradientStops.Add(new GradientStop { Color = ColorHelper.FromArgb(255, 237, 237, 237), Offset = 0 });
            brush.GradientStops.Add(new GradientStop { Color = ColorHelper.FromArgb(255, 240, 240, 240), Offset = 0.5 });
            brush.GradientStops.Add(new GradientStop { Color = ColorHelper.FromArgb(255, 238, 238, 238), Offset = 1 });
        }

        return brush;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        ApplyThemeBackground();
        SetupDropShadow();
        UpdateActiveState();
    }

    private void SetupDropShadow()
    {
        var visual = ElementCompositionPreview.GetElementVisual(CardRoot);
        var compositor = visual.Compositor;

        // Create drop shadow
        _dropShadow = compositor.CreateDropShadow();
        _dropShadow.BlurRadius = 20;
        _dropShadow.Opacity = 0.3f;
        _dropShadow.Color = Colors.Black;
        _dropShadow.Offset = new Vector3(0, 4, 0);

        // Create sprite visual to host the shadow
        _shadowVisual = compositor.CreateSpriteVisual();
        _shadowVisual.Shadow = _dropShadow;
        _shadowVisual.Size = new Vector2((float)CardRoot.ActualWidth, (float)CardRoot.ActualHeight);

        // Insert shadow behind the card content
        ElementCompositionPreview.SetElementChildVisual(OuterContainer, _shadowVisual);
    }

    private void UpdateShadowSize()
    {
        if (_shadowVisual != null && CardRoot.ActualWidth > 0 && CardRoot.ActualHeight > 0)
        {
            _shadowVisual.Size = new Vector2((float)CardRoot.ActualWidth, (float)CardRoot.ActualHeight);
        }
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateBrightnessBar();
        UpdateShadowSize();
    }



    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (ViewModel != null)
        {
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;

            if (_isLoaded)
            {
                UpdateActiveState();
            }
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RoomCardViewModel.IsOn) ||
            e.PropertyName == nameof(RoomCardViewModel.BackgroundColorRgb) ||
            e.PropertyName == nameof(RoomCardViewModel.LightColors))
        {
            if (DispatcherQueue?.HasThreadAccess == true)
            {
                UpdateActiveState();
            }
            else
            {
                DispatcherQueue?.TryEnqueue(UpdateActiveState);
            }
        }
        else if (e.PropertyName == nameof(RoomCardViewModel.BrightnessPercent))
        {
            if (DispatcherQueue?.HasThreadAccess == true)
            {
                UpdateBrightnessBar();
            }
            else
            {
                DispatcherQueue?.TryEnqueue(UpdateBrightnessBar);
            }
        }
    }

    private void UpdateActiveState()
    {
        if (!_isLoaded || ViewModel == null) return;

        var isActive = ViewModel.IsOn;

        // Get room color
        var lightColors = ViewModel.LightColors;
        if (lightColors.Count > 0 && isActive)
        {
            var (r, g, b) = lightColors[0];
            _accentColor = Color.FromArgb(255, r, g, b);
        }
        else
        {
            _accentColor = Colors.Gray;
        }

        // Update glow and outline (Set properties before animation starts)
        UpdateBorderEffect(isActive);

        // Update visual state (Triggers animation)
        VisualStateManager.GoToState(this, isActive ? "Active" : "Inactive", true);

        // Update toggle color
        UpdateToggleColor(isActive);

        // Update icon color
        UpdateIconColor(isActive);

        // Update brightness bar
        UpdateBrightnessBar();
    }

    private void UpdateBorderEffect(bool isActive)
    {
        if (ViewModel == null) return;

        // Build the gradient brush from light colors
        var colors = ViewModel.LightColors;
        var borderBrush = colors.Count > 0
            ? BrushFactory.CreateDiagonalGradient(colors)
            : BrushFactory.CreateDiagonalGradient(new[] { (_accentColor.R, _accentColor.G, _accentColor.B) });

        // Create semi-transparent versions for outer glow layers
        var glowColor2 = Color.FromArgb(100, _accentColor.R, _accentColor.G, _accentColor.B);
        var glowColor3 = Color.FromArgb(50, _accentColor.R, _accentColor.G, _accentColor.B);

        // Set brushes on all glow layers
        OutlineBorder.BorderBrush = borderBrush;
        GlowBorder2.BorderBrush = new SolidColorBrush(glowColor2);
        GlowBorder3.BorderBrush = new SolidColorBrush(glowColor3);

        // Animate border opacity for all layers
        var targetOpacity = isActive ? 1.0 : 0.0;
        AnimationHelper.AnimateOpacity(OutlineBorder, targetOpacity);
        AnimationHelper.AnimateOpacity(GlowBorder2, targetOpacity);
        AnimationHelper.AnimateOpacity(GlowBorder3, targetOpacity);
    }

    private void UpdateToggleColor(bool isActive)
    {
        try
        {
            if (RoomToggle == null) return;

            Brush fillBrush;

            if (isActive && ViewModel != null && ViewModel.LightColors.Count > 1)
            {
                // Create gradient for toggle (horizontal)
                fillBrush = BrushFactory.CreateHorizontalGradient(ViewModel.LightColors);
            }
            else
            {
                fillBrush = new SolidColorBrush(_accentColor);
            }

            if (isActive)
            {
                // New logic: Just set background and let the template bind to it
                RoomToggle.Background = fillBrush;
            }
            else
            {
                RoomToggle.Background = new SolidColorBrush(Colors.Transparent);
            }
            
            // Note: FindDescendant/ApplyTemplate no longer needed with new style
        }
        catch
        {
            // Silently handle any resource errors
        }
    }



    private void UpdateIconColor(bool isActive)
    {
        if (RoomIcon == null) return;

        if (isActive && ViewModel != null)
        {
            var colors = ViewModel.LightColors;
            if (colors.Count > 1)
            {
                // Create gradient for icon matching toggle/border
                RoomIcon.Foreground = BrushFactory.CreateDiagonalGradient(colors);
            }
            else
            {
                RoomIcon.Foreground = new SolidColorBrush(_accentColor);
            }
        }
        else
        {
            // Use theme-aware inactive color
            var isDark = ActualTheme == ElementTheme.Dark;
            var inactiveColor = isDark
                ? Color.FromArgb(AppConstants.Colors.InactiveIconAlpha, 255, 255, 255)
                : Color.FromArgb(AppConstants.Colors.InactiveIconAlpha, 0, 0, 0);
            RoomIcon.Foreground = new SolidColorBrush(inactiveColor);
        }

        // Animate icon glow
        if (IconGlow == null) return;

        var glowTarget = isActive
            ? Color.FromArgb(80, _accentColor.R, _accentColor.G, _accentColor.B)
            : Colors.Transparent;

        if (_iconGlowBrush == null)
        {
            _iconGlowBrush = new SolidColorBrush(_currentIconGlowColor);
            IconGlow.Background = _iconGlowBrush;
        }

        AnimationHelper.AnimateColor(_iconGlowBrush, _currentIconGlowColor, glowTarget);
        _currentIconGlowColor = glowTarget;
    }

    private void UpdateBrightnessBar()
    {
        if (BrightnessFill == null || ViewModel == null) return;

        var parentGrid = BrightnessFill.Parent as Grid;
        if (parentGrid == null) return;

        var totalWidth = parentGrid.ActualWidth;
        if (totalWidth <= 0) return;

        var fillWidth = totalWidth * (ViewModel.BrightnessPercent / 100.0);
        BrightnessFill.Width = fillWidth;

        // Update fill color based on active state
        if (ViewModel.IsOn)
        {
            BrightnessFill.Background = new SolidColorBrush(_accentColor);
        }
        else
        {
            // Use theme-aware inactive color
            var isDark = ActualTheme == ElementTheme.Dark;
            var inactiveColor = isDark
                ? Color.FromArgb(AppConstants.Colors.InactiveBrightnessBarAlpha, 255, 255, 255)
                : Color.FromArgb(AppConstants.Colors.InactiveBrightnessBarAlpha, 0, 0, 0);
            BrightnessFill.Background = new SolidColorBrush(inactiveColor);
        }
    }

    private void CardRoot_ManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e)
    {
        if (ViewModel == null) return;

        _isDragging = false;
        _cumulativeDeltaY = 0;
        _startBrightness = ViewModel.Brightness;
    }

    private void CardRoot_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
    {
        if (ViewModel == null) return;

        _cumulativeDeltaY += e.Delta.Translation.Y;

        if (Math.Abs(_cumulativeDeltaY) > AppConstants.BrightnessDrag.DragThreshold)
        {
            if (!_isDragging)
            {
                _isDragging = true;
                VisualStateManager.GoToState(this, "Dragging", true);
            }

            var brightnessChange = -_cumulativeDeltaY / (AppConstants.BrightnessDrag.PixelsPerPercent * 100);
            var newBrightness = Math.Clamp(_startBrightness + brightnessChange, 0.0, 1.0);
            _pendingBrightness = newBrightness;

            var now = DateTime.UtcNow;
            if ((now - _lastBrightnessUpdate).TotalMilliseconds >= AppConstants.BrightnessDrag.ThrottleMs)
            {
                _lastBrightnessUpdate = now;
                ViewModel.SetBrightnessCommand.Execute(newBrightness);
            }
        }

        e.Handled = true;
    }

    private void CardRoot_ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
    {
        if (_isDragging && ViewModel != null)
        {
            ViewModel.SetBrightnessCommand.Execute(_pendingBrightness);
        }

        _isDragging = false;
        VisualStateManager.GoToState(this, "Normal", true);
    }

    private void RoomToggle_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.IsOn = !ViewModel.IsOn;
        }
        e.Handled = true;
    }

    private void CardRoot_Tapped(object sender, TappedRoutedEventArgs e)
    {
        // Prevent navigation if clicking the toggle
        if (e.OriginalSource is DependencyObject obj && obj.IsDescendantOf(RoomToggle))
        {
            return;
        }

        if (!_isDragging && ViewModel != null)
        {
            // Prepare connected animation before navigation
            ConnectedAnimationService.GetForCurrentView()
                .PrepareToAnimate("RoomCardToHeader", CardRoot);

            ViewModel.TapRoomCommand.Execute(null);
        }

        e.Handled = true;
    }

    private void CardRoot_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging)
        {
            _isHovering = true;
            ApplyThemeBackground();
            VisualStateManager.GoToState(this, "Hover", true);
        }
    }

    private void CardRoot_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging)
        {
            _isHovering = false;
            ApplyThemeBackground();
            VisualStateManager.GoToState(this, "Default", true);
        }
    }

}
