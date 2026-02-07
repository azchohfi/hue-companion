using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using HueWindows.Constants;
using HueWindows.Core.ViewModels;
using HueWindows.Utilities;
using Windows.UI;

namespace HueWindows.Views;

/// <summary>
/// Page for controlling an individual light.
/// </summary>
public sealed partial class LightDetailPage : Page
{
    public LightDetailViewModel ViewModel { get; }

    private bool _isLoaded;
    private bool _isUpdatingColor;
    private bool _isUpdatingSlider;
    private Color _accentColor = Colors.White;
    private SolidColorBrush? _lightIconBrush;
    private SolidColorBrush? _brightnessIconBrush;
    private SolidColorBrush? _colorIconBrush;
    private SolidColorBrush? _temperatureIconBrush;
    private SolidColorBrush? _toggleBrush;
    private Color _currentLightIconColor = Colors.Gray;
    private Color _currentBrightnessIconColor = Colors.Gray;
    private Color _currentColorIconColor = Colors.Gray;
    private Color _currentTemperatureIconColor = Colors.Gray;
    private Color _currentToggleColor = Colors.Transparent;
    private bool _useFirstBorder = true;
    private DispatcherTimer? _brightnessDebounceTimer;
    private DispatcherTimer? _temperatureDebounceTimer;
    private DispatcherTimer? _colorDebounceTimer;
    private double _pendingBrightness;
    private int _pendingTemperature;
    private (byte R, byte G, byte B) _pendingColor;

    public LightDetailPage()
    {
        ViewModel = App.Services.GetRequiredService<LightDetailViewModel>();

        this.InitializeComponent();
        this.ActualThemeChanged += OnActualThemeChanged;
    }

    private void OnActualThemeChanged(FrameworkElement sender, object args)
    {
        ApplyThemeBackground();
        // Re-update colors for new theme
        if (_isLoaded)
        {
            UpdateHeaderActiveState();
        }
    }

    private void ApplyThemeBackground()
    {
        var isDark = ActualTheme == ElementTheme.Dark;
        MainContainer.Background = CreateCardGradient(isDark);
    }

    private static LinearGradientBrush CreateCardGradient(bool isDark)
        => CardGradientHelper.CreateCardGradient(isDark);

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        ApplyThemeBackground();
        UpdateHeaderActiveState();
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LightDetailViewModel.IsOn) ||
            e.PropertyName == nameof(LightDetailViewModel.CurrentColor))
        {
            if (DispatcherQueue?.HasThreadAccess == true)
            {
                UpdateHeaderActiveState();
            }
            else
            {
                DispatcherQueue?.TryEnqueue(UpdateHeaderActiveState);
            }
        }
        else if (e.PropertyName == nameof(LightDetailViewModel.BrightnessPercent))
        {
            if (DispatcherQueue?.HasThreadAccess == true)
            {
                AnimateSliderToValue(BrightnessSlider, ViewModel.BrightnessPercent);
            }
            else
            {
                DispatcherQueue?.TryEnqueue(() => AnimateSliderToValue(BrightnessSlider, ViewModel.BrightnessPercent));
            }
        }
    }

    private void UpdateHeaderActiveState()
    {
        if (!_isLoaded) return;

        var isActive = ViewModel.IsOn;
        var color = ViewModel.CurrentColor;

        // Get accent color from light
        if (color != null && isActive)
        {
            var rgb = color.ToRgb(1.0);
            _accentColor = Color.FromArgb(255, rgb.R, rgb.G, rgb.B);
        }
        else
        {
            _accentColor = Colors.Gray;
        }

        UpdateBorderEffect(isActive);
        UpdateToggleColor(isActive);
        UpdateLightIconColor(isActive);
        UpdateBrightnessIconColor(isActive);
        UpdateColorIconColor(isActive);
        UpdateTemperatureIconColor(isActive);
    }

    private void UpdateBorderEffect(bool isActive)
    {
        if (!isActive)
        {
            AnimateBorderOpacity(OutlineBorder, 0.0);
            AnimateBorderOpacity(OutlineBorder2, 0.0);
            return;
        }

        var newBorder = _useFirstBorder ? OutlineBorder : OutlineBorder2;
        var oldBorder = _useFirstBorder ? OutlineBorder2 : OutlineBorder;

        newBorder.BorderBrush = new SolidColorBrush(_accentColor);

        AnimateBorderOpacity(newBorder, 1.0);
        AnimateBorderOpacity(oldBorder, 0.0);

        _useFirstBorder = !_useFirstBorder;
    }

    private void AnimateBorderOpacity(Border border, double targetOpacity)
    {
        var animation = new DoubleAnimation
        {
            To = targetOpacity,
            Duration = new Duration(TimeSpan.FromMilliseconds(AppConstants.Animation.StandardDurationMs)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        Storyboard.SetTarget(animation, border);
        Storyboard.SetTargetProperty(animation, "Opacity");
        storyboard.Begin();
    }

    private void UpdateToggleColor(bool isActive)
    {
        if (LightToggle == null) return;

        var targetColor = isActive ? _accentColor : Colors.Transparent;

        if (_toggleBrush == null)
        {
            _toggleBrush = new SolidColorBrush(_currentToggleColor);
            LightToggle.Background = _toggleBrush;
        }

        AnimateSolidBrushColor(_toggleBrush, _currentToggleColor, targetColor);
        _currentToggleColor = targetColor;
    }

    private void UpdateLightIconColor(bool isActive)
    {
        if (LightIcon == null) return;

        // Use theme-aware inactive color
        var isDark = ActualTheme == ElementTheme.Dark;
        var inactiveColor = isDark
            ? Color.FromArgb(AppConstants.Colors.InactiveIconAlpha, 255, 255, 255)
            : Color.FromArgb(AppConstants.Colors.InactiveIconAlpha, 0, 0, 0);

        var targetColor = isActive ? _accentColor : inactiveColor;

        if (_lightIconBrush == null)
        {
            _lightIconBrush = new SolidColorBrush(_currentLightIconColor);
            LightIcon.Foreground = _lightIconBrush;
        }

        AnimateSolidBrushColor(_lightIconBrush, _currentLightIconColor, targetColor);
        _currentLightIconColor = targetColor;
    }

    private void UpdateBrightnessIconColor(bool isActive)
    {
        if (BrightnessIcon == null) return;

        // Use theme-aware inactive color
        var isDark = ActualTheme == ElementTheme.Dark;
        var inactiveColor = isDark
            ? Color.FromArgb(AppConstants.Colors.InactiveIconAlpha, 255, 255, 255)
            : Color.FromArgb(AppConstants.Colors.InactiveIconAlpha, 0, 0, 0);

        var targetColor = isActive ? _accentColor : inactiveColor;

        if (_brightnessIconBrush == null)
        {
            _brightnessIconBrush = new SolidColorBrush(_currentBrightnessIconColor);
            BrightnessIcon.Foreground = _brightnessIconBrush;
        }

        AnimateSolidBrushColor(_brightnessIconBrush, _currentBrightnessIconColor, targetColor);
        _currentBrightnessIconColor = targetColor;
    }

    private void UpdateColorIconColor(bool isActive)
    {
        if (ColorIcon == null) return;

        // Use theme-aware inactive color
        var isDark = ActualTheme == ElementTheme.Dark;
        var inactiveColor = isDark
            ? Color.FromArgb(AppConstants.Colors.InactiveIconAlpha, 255, 255, 255)
            : Color.FromArgb(AppConstants.Colors.InactiveIconAlpha, 0, 0, 0);

        var targetColor = isActive ? _accentColor : inactiveColor;

        if (_colorIconBrush == null)
        {
            _colorIconBrush = new SolidColorBrush(_currentColorIconColor);
            ColorIcon.Foreground = _colorIconBrush;
        }

        AnimateSolidBrushColor(_colorIconBrush, _currentColorIconColor, targetColor);
        _currentColorIconColor = targetColor;
    }

    private void UpdateTemperatureIconColor(bool isActive)
    {
        if (TemperatureIcon == null) return;

        // Use theme-aware inactive color
        var isDark = ActualTheme == ElementTheme.Dark;
        var inactiveColor = isDark
            ? Color.FromArgb(AppConstants.Colors.InactiveIconAlpha, 255, 255, 255)
            : Color.FromArgb(AppConstants.Colors.InactiveIconAlpha, 0, 0, 0);

        var targetColor = isActive ? _accentColor : inactiveColor;

        if (_temperatureIconBrush == null)
        {
            _temperatureIconBrush = new SolidColorBrush(_currentTemperatureIconColor);
            TemperatureIcon.Foreground = _temperatureIconBrush;
        }

        AnimateSolidBrushColor(_temperatureIconBrush, _currentTemperatureIconColor, targetColor);
        _currentTemperatureIconColor = targetColor;
    }

    private void AnimateSolidBrushColor(SolidColorBrush brush, Color from, Color to)
    {
        var animation = new ColorAnimation
        {
            From = from,
            To = to,
            Duration = new Duration(TimeSpan.FromMilliseconds(AppConstants.Animation.StandardDurationMs)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            EnableDependentAnimation = true
        };

        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        Storyboard.SetTarget(animation, brush);
        Storyboard.SetTargetProperty(animation, "Color");
        storyboard.Begin();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        ViewModel.PropertyChanged += ViewModel_PropertyChanged;

        if (e.Parameter is Guid lightId)
        {
            await ViewModel.LoadLightAsync(lightId);

            // Set initial color picker value if color is available
            if (ViewModel.CurrentColor != null)
            {
                var rgb = ViewModel.CurrentColor.ToRgb(1.0);
                _isUpdatingColor = true;
                LightColorPicker.Color = Color.FromArgb(255, rgb.R, rgb.G, rgb.B);
                _isUpdatingColor = false;
            }

            // Set initial slider value
            if (BrightnessSlider != null)
            {
                _isUpdatingSlider = true;
                BrightnessSlider.Value = ViewModel.BrightnessPercent;
                _isUpdatingSlider = false;
            }
        }

        if (_isLoaded)
        {
            UpdateHeaderActiveState();
        }
    }

    private void LightToggle_Tapped(object sender, TappedRoutedEventArgs e)
    {
        ViewModel.IsOn = !ViewModel.IsOn;
        e.Handled = true;
    }

    private void BrightnessSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingSlider) return;

        // Debounce: only send command after user stops dragging for 150ms
        _pendingBrightness = e.NewValue / 100.0;

        if (_brightnessDebounceTimer == null)
        {
            _brightnessDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(150)
            };
            _brightnessDebounceTimer.Tick += (s, args) =>
            {
                _brightnessDebounceTimer.Stop();
                ViewModel.SetBrightnessCommand.Execute(_pendingBrightness);
            };
        }

        _brightnessDebounceTimer.Stop();
        _brightnessDebounceTimer.Start();
    }

    private void ColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
    {
        if (_isUpdatingColor) return;

        // Debounce color changes
        var color = args.NewColor;
        _pendingColor = (color.R, color.G, color.B);

        if (_colorDebounceTimer == null)
        {
            _colorDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(150)
            };
            _colorDebounceTimer.Tick += (s, e) =>
            {
                _colorDebounceTimer.Stop();
                ViewModel.SetColorFromRgbCommand.Execute(_pendingColor);
            };
        }

        _colorDebounceTimer.Stop();
        _colorDebounceTimer.Start();
    }

    private void TemperatureSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        // Debounce temperature changes
        _pendingTemperature = (int)e.NewValue;

        if (_temperatureDebounceTimer == null)
        {
            _temperatureDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(150)
            };
            _temperatureDebounceTimer.Tick += (s, args) =>
            {
                _temperatureDebounceTimer.Stop();
                ViewModel.SetColorTemperatureCommand.Execute(_pendingTemperature);
            };
        }

        _temperatureDebounceTimer.Stop();
        _temperatureDebounceTimer.Start();
    }

    private void AnimateSliderToValue(Slider slider, double targetValue)
    {
        if (slider == null) return;

        _isUpdatingSlider = true;

        var animation = new DoubleAnimation
        {
            To = targetValue,
            Duration = new Duration(TimeSpan.FromMilliseconds(AppConstants.Animation.StandardDurationMs)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            EnableDependentAnimation = true
        };

        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        Storyboard.SetTarget(animation, slider);
        Storyboard.SetTargetProperty(animation, "Value");
        storyboard.Completed += (s, e) => _isUpdatingSlider = false;
        storyboard.Begin();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);

        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;

        _brightnessDebounceTimer?.Stop();
        _colorDebounceTimer?.Stop();
        _temperatureDebounceTimer?.Stop();
    }

    /// <summary>
    /// Helper to check if both color modes are supported.
    /// </summary>
    public Visibility BothColorModesSupported()
    {
        return ViewModel.SupportsColor && ViewModel.SupportsColorTemperature
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    /// <summary>
    /// Helper to show color picker.
    /// </summary>
    public Visibility ShowColorPicker()
    {
        return ViewModel.SupportsColor && ViewModel.IsColorMode
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    /// <summary>
    /// Helper to show temperature picker.
    /// </summary>
    public Visibility ShowTemperaturePicker()
    {
        return ViewModel.SupportsColorTemperature && !ViewModel.IsColorMode
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    /// <summary>
    /// Helper to invert a boolean.
    /// </summary>
    public bool InvertBool(bool value) => !value;

    /// <summary>
    /// Helper to check if error message exists.
    /// </summary>
    public bool HasErrorMessage(string? message) => !string.IsNullOrEmpty(message);
}
