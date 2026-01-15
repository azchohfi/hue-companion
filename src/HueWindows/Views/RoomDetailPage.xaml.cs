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
using HueWindows.Core.Models;
using HueWindows.Core.Services.Interfaces;
using HueWindows.Core.ViewModels;
using HueWindows.Helpers;
using Windows.UI;
using PinnedItemType = HueWindows.Core.Models.PinnedItemType;

namespace HueWindows.Views;

/// <summary>
/// Page showing room/zone details with lights and scenes.
/// </summary>
public sealed partial class RoomDetailPage : Page
{
    public RoomDetailViewModel ViewModel { get; }
    private readonly IPinnedItemsService _pinnedItemsService;
    private bool _isLoaded;
    private Color _accentColor = Colors.White;
    private SolidColorBrush? _roomIconBrush;
    private SolidColorBrush? _brightnessIconBrush;
    private SolidColorBrush? _toggleBrush;
    private SolidColorBrush? _colorButtonBrush;
    private Color _currentRoomIconColor = Colors.Gray;
    private Color _currentBrightnessIconColor = Colors.Gray;
    private Color _currentToggleColor = Colors.Transparent;
    private Color _currentColorButtonColor = Colors.Gray;
    private bool _useFirstBorder = true; // Toggle between two borders for cross-fade
    private bool _isUpdatingSlider; // Prevent feedback loops when animating slider
    private DispatcherTimer? _brightnessDebounceTimer; // Debounce brightness changes
    private double _pendingBrightness; // Pending brightness value to send

    public RoomDetailPage()
    {
        ViewModel = App.Services.GetRequiredService<RoomDetailViewModel>();
        _pinnedItemsService = App.Services.GetRequiredService<IPinnedItemsService>();
        ViewModel.LightSelected += OnLightSelected;
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;

        this.InitializeComponent();
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        UpdateHeaderActiveState();
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RoomDetailViewModel.IsOn) ||
            e.PropertyName == nameof(RoomDetailViewModel.LightColors))
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
        else if (e.PropertyName == nameof(RoomDetailViewModel.BrightnessPercent))
        {
            if (DispatcherQueue?.HasThreadAccess == true)
            {
                AnimateSliderToValue(ViewModel.BrightnessPercent);
            }
            else
            {
                DispatcherQueue?.TryEnqueue(() => AnimateSliderToValue(ViewModel.BrightnessPercent));
            }
        }
    }

    private void UpdateHeaderActiveState()
    {
        if (!_isLoaded) return;

        var isActive = ViewModel.IsOn;
        var colors = ViewModel.LightColors;

        // Get primary accent color
        if (colors.Count > 0 && isActive)
        {
            var (r, g, b) = colors[0];
            _accentColor = Color.FromArgb(255, r, g, b);
        }
        else
        {
            _accentColor = Colors.Gray;
        }

        UpdateBorderEffect(isActive, colors);
        UpdateToggleColor(isActive, colors);
        UpdateRoomIconColor(isActive, colors);
        UpdateBrightnessIconColor(isActive, colors);
        UpdateColorButtonColor(isActive, colors);
    }

    private void UpdateBorderEffect(bool isActive, List<(byte R, byte G, byte B)> colors)
    {
        if (!isActive)
        {
            // Fade out borders
            AnimateBorderOpacity(OutlineBorder, 0.0);
            AnimateBorderOpacity(OutlineBorder2, 0.0);
            return;
        }

        // Cross-fade between two borders for smooth color transitions
        var newBorder = _useFirstBorder ? OutlineBorder : OutlineBorder2;
        var oldBorder = _useFirstBorder ? OutlineBorder2 : OutlineBorder;

        // Set new gradient on the incoming border
        newBorder.BorderBrush = CreateGradientBrush(colors);

        // Cross-fade: fade in new, fade out old
        AnimateBorderOpacity(newBorder, 1.0);
        AnimateBorderOpacity(oldBorder, 0.0);

        // Toggle for next update
        _useFirstBorder = !_useFirstBorder;
    }

    private LinearGradientBrush CreateGradientBrush(List<(byte R, byte G, byte B)> colors)
    {
        var brush = new LinearGradientBrush();
        brush.StartPoint = new Windows.Foundation.Point(0, 0);
        brush.EndPoint = new Windows.Foundation.Point(1, 1);

        if (colors.Count > 1)
        {
            for (int i = 0; i < colors.Count; i++)
            {
                var (r, g, b) = colors[i];
                brush.GradientStops.Add(new GradientStop
                {
                    Color = Color.FromArgb(255, r, g, b),
                    Offset = (double)i / (colors.Count - 1)
                });
            }
        }
        else if (colors.Count == 1)
        {
            var (r, g, b) = colors[0];
            var color = Color.FromArgb(255, r, g, b);
            brush.GradientStops.Add(new GradientStop { Color = color, Offset = 0 });
            brush.GradientStops.Add(new GradientStop { Color = color, Offset = 1 });
        }
        else
        {
            brush.GradientStops.Add(new GradientStop { Color = _accentColor, Offset = 0 });
            brush.GradientStops.Add(new GradientStop { Color = _accentColor, Offset = 1 });
        }

        return brush;
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

    private void UpdateToggleColor(bool isActive, List<(byte R, byte G, byte B)> colors)
    {
        if (RoomToggle == null) return;

        var targetColor = isActive
            ? (colors.Count > 0 ? Color.FromArgb(255, colors[0].R, colors[0].G, colors[0].B) : _accentColor)
            : Colors.Transparent;

        // Use solid color with animation for simplicity
        if (_toggleBrush == null)
        {
            _toggleBrush = new SolidColorBrush(_currentToggleColor);
            RoomToggle.Background = _toggleBrush;
        }

        AnimateSolidBrushColor(_toggleBrush, _currentToggleColor, targetColor);
        _currentToggleColor = targetColor;
    }

    private void UpdateRoomIconColor(bool isActive, List<(byte R, byte G, byte B)> colors)
    {
        if (RoomIcon == null) return;

        var targetColor = isActive
            ? (colors.Count > 0 ? Color.FromArgb(255, colors[0].R, colors[0].G, colors[0].B) : _accentColor)
            : Color.FromArgb(AppConstants.Colors.InactiveIconAlpha, 255, 255, 255);

        // For simplicity, use solid color with animation (gradient icons are complex to animate)
        if (_roomIconBrush == null)
        {
            _roomIconBrush = new SolidColorBrush(_currentRoomIconColor);
            RoomIcon.Foreground = _roomIconBrush;
        }

        AnimateSolidBrushColor(_roomIconBrush, _currentRoomIconColor, targetColor);
        _currentRoomIconColor = targetColor;
    }

    private void UpdateBrightnessIconColor(bool isActive, List<(byte R, byte G, byte B)> colors)
    {
        if (BrightnessIcon == null) return;

        var targetColor = isActive
            ? (colors.Count > 0 ? Color.FromArgb(255, colors[0].R, colors[0].G, colors[0].B) : _accentColor)
            : Color.FromArgb(AppConstants.Colors.InactiveIconAlpha, 255, 255, 255);

        // For simplicity, use solid color with animation
        if (_brightnessIconBrush == null)
        {
            _brightnessIconBrush = new SolidColorBrush(_currentBrightnessIconColor);
            BrightnessIcon.Foreground = _brightnessIconBrush;
        }

        AnimateSolidBrushColor(_brightnessIconBrush, _currentBrightnessIconColor, targetColor);
        _currentBrightnessIconColor = targetColor;
    }

    private void UpdateColorButtonColor(bool isActive, List<(byte R, byte G, byte B)> colors)
    {
        if (ColorButtonContent == null) return;

        var targetColor = isActive && colors.Count > 0
            ? Color.FromArgb(255, colors[0].R, colors[0].G, colors[0].B)
            : Color.FromArgb(64, 255, 255, 255); // #40FFFFFF when inactive

        if (_colorButtonBrush == null)
        {
            _colorButtonBrush = new SolidColorBrush(_currentColorButtonColor);
            ColorButtonContent.Background = _colorButtonBrush;
        }

        AnimateSolidBrushColor(_colorButtonBrush, _currentColorButtonColor, targetColor);
        _currentColorButtonColor = targetColor;
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

        // Support both simple Guid (room) and NavigationTag (room or zone)
        if (e.Parameter is NavigationTag navTag)
        {
            await ViewModel.LoadRoomAsync(navTag.Id, navTag.Type);
        }
        else if (e.Parameter is Guid roomId)
        {
            await ViewModel.LoadRoomAsync(roomId, LightGroupType.Room);
        }

        // Update colors and slider after room data is loaded
        if (_isLoaded)
        {
            UpdateHeaderActiveState();
        }

        // Set initial slider value (no animation for initial load)
        if (BrightnessSlider != null)
        {
            _isUpdatingSlider = true;
            BrightnessSlider.Value = ViewModel.BrightnessPercent;
            _isUpdatingSlider = false;
        }
    }

    private void OnLightSelected(object? sender, Guid lightId)
    {
        var navigationService = App.Services.GetRequiredService<INavigationService>();
        navigationService.NavigateTo<LightDetailPage>(lightId);
    }

    private void RoomToggle_Tapped(object sender, TappedRoutedEventArgs e)
    {
        ViewModel.IsOn = !ViewModel.IsOn;
        e.Handled = true;
    }

    private void BrightnessSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        // Skip if we're programmatically animating the slider
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

        // Reset timer on each change
        _brightnessDebounceTimer.Stop();
        _brightnessDebounceTimer.Start();
    }

    private void AnimateSliderToValue(double targetValue)
    {
        if (BrightnessSlider == null) return;

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
        Storyboard.SetTarget(animation, BrightnessSlider);
        Storyboard.SetTargetProperty(animation, "Value");
        storyboard.Completed += (s, e) => _isUpdatingSlider = false;
        storyboard.Begin();
    }

    /// <summary>
    /// Helper to check if scenes exist.
    /// </summary>
    public Visibility HasScenes(int count)
    {
        return count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Helper to check if error message exists.
    /// </summary>
    public bool HasErrorMessage(string? message)
    {
        return !string.IsNullOrEmpty(message);
    }

    /// <summary>
    /// Handles adding a light to the custom dashboard.
    /// </summary>
    private async void OnLightAddToDashboardRequested(object? sender, Guid lightId)
    {
        await _pinnedItemsService.PinAsync(lightId, PinnedItemType.Light);
    }

    private void LightsRepeater_ElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
    {
        if (args.Element is Controls.LightCard lightCard)
        {
            lightCard.AddToDashboardRequested += OnLightAddToDashboardRequested;
        }
    }

    private void ColorPickerFlyout_Opening(object sender, object e)
    {
        // Set initial color when flyout opens
        var colors = ViewModel.LightColors;
        if (colors.Count > 0)
        {
            var (r, g, b) = colors[0];
            ColorFlyout.InitialColor = Color.FromArgb(255, r, g, b);
        }
    }

    private void ColorFlyout_ColorChanged(object sender, Color color)
    {
        ViewModel.SetRoomColorFromRgbCommand.Execute((color.R, color.G, color.B));
    }
}
