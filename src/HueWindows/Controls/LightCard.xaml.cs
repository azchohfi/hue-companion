using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using HueWindows.Constants;
using HueWindows.Core.Models;
using HueWindows.Core.ViewModels;
using HueWindows.Utilities;
using Windows.UI;

namespace HueWindows.Controls;

/// <summary>
/// Light card control for the room detail page.
/// Displays a single light with colored border effect and toggle.
/// </summary>
public sealed partial class LightCard : UserControl
{
    private bool _isLoaded;
    private bool _isHovering;
    private Color _accentColor = Colors.White;
    private Color _currentIconColor = Colors.Gray;
    private Color _currentColorButtonColor = Colors.Transparent;
    private SolidColorBrush? _iconBrush;
    private SolidColorBrush? _toggleBrush;
    private SolidColorBrush? _colorButtonBrush;
    private bool _useFirstBorder = true; // Toggle between two borders for cross-fade
    private LightItemViewModel? _currentViewModel;
    private DispatcherTimer? _brightnessDebounceTimer;
    private double _pendingBrightness;
    private bool _isUpdatingSlider;

    public LightItemViewModel? ViewModel => DataContext as LightItemViewModel;

    /// <summary>
    /// Event raised when the user requests to add this light to the dashboard.
    /// </summary>
    public event EventHandler<Guid>? AddToDashboardRequested;

    public LightCard()
    {
        this.InitializeComponent();
        this.DataContextChanged += OnDataContextChanged;
        this.Loaded += OnLoaded;
        this.Unloaded += OnUnloaded;
        this.ActualThemeChanged += OnActualThemeChanged;
    }

    private void OnActualThemeChanged(FrameworkElement sender, object args)
    {
        ApplyThemeBackground();
        // Re-update icon and color button colors for new theme
        if (ViewModel != null)
        {
            UpdateIconColor(ViewModel.IsOn);
            UpdateColorButtonColor(ViewModel.IsOn);
        }
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
        => CardGradientHelper.CreateCardGradient(isDark);

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
        UpdateActiveState();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_currentViewModel != null)
        {
            _currentViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            _currentViewModel = null;
        }
    }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (_currentViewModel != null)
        {
            _currentViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        _currentViewModel = ViewModel;

        if (_currentViewModel != null)
        {
            _currentViewModel.PropertyChanged += ViewModel_PropertyChanged;

            if (_isLoaded)
            {
                UpdateActiveState();
            }
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LightItemViewModel.IsOn) ||
            e.PropertyName == nameof(LightItemViewModel.CurrentColorRgb) ||
            e.PropertyName == nameof(LightItemViewModel.ColorModeLabel))
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
        else if (e.PropertyName == nameof(LightItemViewModel.BrightnessPercent))
        {
            // Update slider from external changes (bridge events) without triggering debounce
            if (DispatcherQueue?.HasThreadAccess == true)
            {
                UpdateSliderFromViewModel();
            }
            else
            {
                DispatcherQueue?.TryEnqueue(UpdateSliderFromViewModel);
            }
        }
    }

    private void UpdateSliderFromViewModel()
    {
        if (ViewModel == null) return;
        _isUpdatingSlider = true;
        BrightnessSlider.Value = ViewModel.BrightnessPercent;
        _isUpdatingSlider = false;
    }

    private void UpdateActiveState()
    {
        if (!_isLoaded || ViewModel == null) return;

        var isActive = ViewModel.IsOn;
        var colorRgb = ViewModel.CurrentColorRgb;

        // Get light color
        if (colorRgb.HasValue && isActive)
        {
            var (r, g, b) = colorRgb.Value;
            _accentColor = Color.FromArgb(255, r, g, b);
        }
        else
        {
            _accentColor = Colors.Gray;
        }

        UpdateBorderEffect(isActive);
        UpdateToggleColor(isActive);
        UpdateIconColor(isActive);
        UpdateColorButtonColor(isActive);
        UpdateColorModeLabel();
    }

    private void UpdateColorModeLabel()
    {
        var label = ViewModel?.ColorModeLabel ?? string.Empty;
        var hasLabel = !string.IsNullOrEmpty(label);
        ColorModeText.Visibility = hasLabel ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateBorderEffect(bool isActive)
    {
        if (!isActive)
        {
            // Fade out both borders
            AnimationHelper.AnimateOpacity(OutlineBorder, 0.0);
            AnimationHelper.AnimateOpacity(OutlineBorder2, 0.0);
            return;
        }

        // Cross-fade between two borders for smooth color transitions
        var newBorder = _useFirstBorder ? OutlineBorder : OutlineBorder2;
        var oldBorder = _useFirstBorder ? OutlineBorder2 : OutlineBorder;

        // Set new color on the incoming border
        newBorder.BorderBrush = new SolidColorBrush(_accentColor);

        // Cross-fade: fade in new, fade out old
        AnimationHelper.AnimateOpacity(newBorder, 1.0);
        AnimationHelper.AnimateOpacity(oldBorder, 0.0);

        // Toggle for next update
        _useFirstBorder = !_useFirstBorder;
    }

    private void UpdateToggleColor(bool isActive)
    {
        if (LightToggle == null) return;

        var targetColor = isActive ? _accentColor : Colors.Transparent;

        // Initialize brush if needed
        if (_toggleBrush == null)
        {
            _toggleBrush = new SolidColorBrush(Colors.Transparent);
            LightToggle.Background = _toggleBrush;
        }

        // Animate color change
        AnimationHelper.AnimateColor(_toggleBrush, ((SolidColorBrush)LightToggle.Background).Color, targetColor);
    }

    private void UpdateIconColor(bool isActive)
    {
        if (LightIcon == null) return;

        // Use theme-aware inactive color
        var isDark = ActualTheme == ElementTheme.Dark;
        var targetColor = isActive
            ? _accentColor
            : isDark
                ? Color.FromArgb(AppConstants.Colors.InactiveIconAlpha, 255, 255, 255)
                : Color.FromArgb(AppConstants.Colors.InactiveIconAlpha, 0, 0, 0);

        // Initialize brush if needed
        if (_iconBrush == null)
        {
            _iconBrush = new SolidColorBrush(_currentIconColor);
            LightIcon.Foreground = _iconBrush;
        }

        // Animate color change
        AnimationHelper.AnimateColor(_iconBrush, _currentIconColor, targetColor);
        _currentIconColor = targetColor;
    }

    private void UpdateColorButtonColor(bool isActive)
    {
        if (ColorButtonContent == null) return;

        // Use theme-aware inactive color
        var isDark = ActualTheme == ElementTheme.Dark;
        var targetColor = isActive
            ? _accentColor
            : isDark
                ? Color.FromArgb(64, 255, 255, 255)
                : Color.FromArgb(64, 0, 0, 0);

        // Initialize brush if needed
        if (_colorButtonBrush == null)
        {
            _colorButtonBrush = new SolidColorBrush(_currentColorButtonColor);
            ColorButtonContent.Background = _colorButtonBrush;
        }

        // Animate color change
        AnimationHelper.AnimateColor(_colorButtonBrush, _currentColorButtonColor, targetColor);
        _currentColorButtonColor = targetColor;
    }

    private void ColorPickerFlyout_Opening(object sender, object e)
    {
        if (ViewModel == null) return;

        // Set initial color when flyout opens
        var colorRgb = ViewModel.CurrentColorRgb;
        if (colorRgb.HasValue)
        {
            var (r, g, b) = colorRgb.Value;
            ColorFlyout.InitialColor = Color.FromArgb(255, r, g, b);
        }

        // Animate the flyout entrance
        ColorFlyout.AnimateEntrance();
    }

    private void ColorFlyout_ColorChanged(object sender, Color color)
    {
        ViewModel?.SetColorFromRgbCommand.Execute((color.R, color.G, color.B));
    }

    private void CardRoot_Tapped(object sender, TappedRoutedEventArgs e)
    {
        // Prevent navigation if clicking the toggle, color button, or brightness slider
        if (e.OriginalSource is DependencyObject obj)
        {
            if (obj.IsDescendantOf(LightToggle))
                return;
            if (ColorSplitButton != null && obj.IsDescendantOf(ColorSplitButton))
                return;
            if (obj.IsDescendantOf(BrightnessSlider))
                return;
        }

        ViewModel?.TapLightCommand.Execute(null);
        e.Handled = true;
    }

    private void BrightnessSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingSlider || ViewModel == null) return;

        _pendingBrightness = e.NewValue / 100.0;

        if (_brightnessDebounceTimer == null)
        {
            _brightnessDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            _brightnessDebounceTimer.Tick += (s, args) =>
            {
                _brightnessDebounceTimer.Stop();
                ViewModel?.SetBrightnessCommand.Execute(_pendingBrightness);
            };
        }

        _brightnessDebounceTimer.Stop();
        _brightnessDebounceTimer.Start();
    }

    private void LightToggle_Tapped(object sender, TappedRoutedEventArgs e)
    {
        // Explicitly toggle - the binding handles the state change
        if (ViewModel != null)
        {
            ViewModel.IsOn = !ViewModel.IsOn;
        }
        e.Handled = true;
    }

    private void CardRoot_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        _isHovering = true;
        ApplyThemeBackground();
        VisualStateManager.GoToState(this, "Hover", true);
    }

    private void CardRoot_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        _isHovering = false;
        ApplyThemeBackground();
        VisualStateManager.GoToState(this, "Default", true);
    }

    private void AddToDashboard_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null)
        {
            AddToDashboardRequested?.Invoke(this, ViewModel.LightId);
        }
    }

    // Shared clipboard for copy/paste color between light cards
    private static HueColor? _clipboardColor;

    private void ContextMenu_Opening(object sender, object e)
    {
        // Show paste option only when clipboard has a color and light supports color
        PasteColorMenuItem.Visibility = (_clipboardColor != null && ViewModel?.SupportsColor == true)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void Identify_Click(object sender, RoutedEventArgs e)
    {
        ViewModel?.IdentifyCommand.Execute(null);
    }

    private void CopyColor_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.CurrentColor != null)
        {
            _clipboardColor = ViewModel.CurrentColor;
        }
    }

    private void PasteColor_Click(object sender, RoutedEventArgs e)
    {
        if (_clipboardColor != null)
        {
            ViewModel?.PasteColorCommand.Execute(_clipboardColor);
        }
    }

}
