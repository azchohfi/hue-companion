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
using HueWindows.Utilities;
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
    private Color _currentRoomIconColor = Colors.Transparent;
    private Color _currentBrightnessIconColor = Colors.Transparent;
    private Color _currentToggleColor = Colors.Transparent;
    private Color _currentColorButtonColor = Colors.Transparent;
    private bool _useFirstBorder = true; // Toggle between two borders for cross-fade
    private bool _isUpdatingSlider; // Prevent feedback loops when animating slider
    private DispatcherTimer? _brightnessDebounceTimer; // Debounce brightness changes
    private double _pendingBrightness; // Pending brightness value to send
    private readonly Dictionary<Guid, UIElement> _sceneElements = new(); // Track scene UI elements for animation
    private int _lightEntranceIndex; // Track stagger index for light cards
    private bool _useInstantColorUpdate; // Use instant update (no animation) when initial state was set from navigation params

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
        // Don't call UpdateHeaderActiveState here - let property changes from ViewModel drive updates
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
        var instant = _useInstantColorUpdate;

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

        UpdateBorderEffect(isActive, colors, instant);
        UpdateToggleColor(isActive, colors, instant);
        UpdateRoomIconColor(isActive, colors, instant);
        UpdateBrightnessIconColor(isActive, colors, instant);
        UpdateColorButtonColor(isActive, colors, instant);
    }

    private void UpdateBorderEffect(bool isActive, List<(byte R, byte G, byte B)> colors, bool instant = false)
    {
        if (!isActive)
        {
            // Fade out borders
            if (instant)
            {
                OutlineBorder.Opacity = 0.0;
                OutlineBorder2.Opacity = 0.0;
            }
            else
            {
                AnimateBorderOpacity(OutlineBorder, 0.0);
                AnimateBorderOpacity(OutlineBorder2, 0.0);
            }
            return;
        }

        // Set gradient on the primary border
        OutlineBorder.BorderBrush = CreateGradientBrush(colors);

        if (instant)
        {
            // Instant update - no cross-fade
            OutlineBorder.Opacity = 1.0;
            OutlineBorder2.Opacity = 0.0;
        }
        else
        {
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

    private void UpdateToggleColor(bool isActive, List<(byte R, byte G, byte B)> colors, bool instant = false)
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

        if (instant)
        {
            _toggleBrush.Color = targetColor;
        }
        else
        {
            AnimateSolidBrushColor(_toggleBrush, _currentToggleColor, targetColor);
        }
        _currentToggleColor = targetColor;
    }

    private void UpdateRoomIconColor(bool isActive, List<(byte R, byte G, byte B)> colors, bool instant = false)
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

        if (instant)
        {
            _roomIconBrush.Color = targetColor;
        }
        else
        {
            AnimateSolidBrushColor(_roomIconBrush, _currentRoomIconColor, targetColor);
        }
        _currentRoomIconColor = targetColor;
    }

    private void UpdateBrightnessIconColor(bool isActive, List<(byte R, byte G, byte B)> colors, bool instant = false)
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

        if (instant)
        {
            _brightnessIconBrush.Color = targetColor;
        }
        else
        {
            AnimateSolidBrushColor(_brightnessIconBrush, _currentBrightnessIconColor, targetColor);
        }
        _currentBrightnessIconColor = targetColor;
    }

    private void UpdateColorButtonColor(bool isActive, List<(byte R, byte G, byte B)> colors, bool instant = false)
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

        if (instant)
        {
            _colorButtonBrush.Color = targetColor;
        }
        else
        {
            AnimateSolidBrushColor(_colorButtonBrush, _currentColorButtonColor, targetColor);
        }
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

        // Reset animation indices for entrance animations
        _lightEntranceIndex = 0;
        _sceneElements.Clear();

        // Try to receive connected animation from RoomCard
        var connectedAnimation = ConnectedAnimationService.GetForCurrentView()
            .GetAnimation("RoomCardToHeader");
        connectedAnimation?.TryStart(HeaderContainer);

        // Handle different navigation parameter types
        Guid roomId;
        LightGroupType groupType;

        if (e.Parameter is RoomNavigationParams navParams)
        {
            // New navigation with initial state - set colors immediately
            roomId = navParams.Id;
            groupType = navParams.Type;
            SetInitialVisualState(navParams.IsOn, navParams.InitialColors);
        }
        else if (e.Parameter is NavigationTag navTag)
        {
            roomId = navTag.Id;
            groupType = navTag.Type;
        }
        else if (e.Parameter is Guid id)
        {
            roomId = id;
            groupType = LightGroupType.Room;
        }
        else
        {
            return; // Invalid parameter
        }

        // Load room data
        await ViewModel.LoadRoomAsync(roomId, groupType);

        // Set initial slider value (no animation for initial load)
        if (BrightnessSlider != null)
        {
            _isUpdatingSlider = true;
            BrightnessSlider.Value = ViewModel.BrightnessPercent;
            _isUpdatingSlider = false;
        }

        // Force update colors now that data is loaded (with instant mode if set)
        _isLoaded = true; // Ensure we can update
        UpdateHeaderActiveState();

        // Clear the instant update flag now that initial load is complete
        _useInstantColorUpdate = false;
    }

    /// <summary>
    /// Sets the initial visual state immediately without animation.
    /// Used to preserve color continuity during page transitions.
    /// </summary>
    private void SetInitialVisualState(bool isOn, List<(byte R, byte G, byte B)> colors)
    {
        // Enable instant mode for subsequent updates during load
        _useInstantColorUpdate = true;

        // Set accent color
        if (colors.Count > 0 && isOn)
        {
            var (r, g, b) = colors[0];
            _accentColor = Color.FromArgb(255, r, g, b);
        }
        else
        {
            _accentColor = Colors.Gray;
        }

        // Set border immediately
        if (isOn && colors.Count > 0)
        {
            OutlineBorder.BorderBrush = CreateGradientBrush(colors);
            OutlineBorder.Opacity = 1.0;
        }
        else
        {
            OutlineBorder.Opacity = 0.0;
        }
        OutlineBorder2.Opacity = 0.0;

        // Set icon color
        var iconColor = isOn && colors.Count > 0
            ? _accentColor
            : Color.FromArgb(AppConstants.Colors.InactiveIconAlpha, 255, 255, 255);

        _currentRoomIconColor = iconColor;
        _roomIconBrush = new SolidColorBrush(iconColor);
        RoomIcon.Foreground = _roomIconBrush;

        _currentBrightnessIconColor = iconColor;
        _brightnessIconBrush = new SolidColorBrush(iconColor);
        BrightnessIcon.Foreground = _brightnessIconBrush;

        // Set toggle color
        _currentToggleColor = isOn && colors.Count > 0 ? _accentColor : Colors.Transparent;
        _toggleBrush = new SolidColorBrush(_currentToggleColor);
        RoomToggle.Background = _toggleBrush;

        // Set color button
        _currentColorButtonColor = isOn && colors.Count > 0
            ? _accentColor
            : Color.FromArgb(64, 255, 255, 255);
        _colorButtonBrush = new SolidColorBrush(_currentColorButtonColor);
        ColorButtonContent.Background = _colorButtonBrush;
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

        // Staggered entrance animation
        var element = args.Element;
        var index = _lightEntranceIndex++;
        element.Opacity = 0;

        var delay = TimeSpan.FromMilliseconds(index * AppConstants.Animation.EntranceStaggerDelayMs);
        DispatcherQueue.TryEnqueue(async () =>
        {
            await Task.Delay(delay);
            AnimationHelper.AnimateOpacity(element, 1.0);
        });
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

    private void ScenesRepeater_ElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
    {
        if (args.Element is FrameworkElement element && element.DataContext is SceneItemViewModel sceneVm)
        {
            // Track element for animation
            _sceneElements[sceneVm.SceneId] = element;

            // Subscribe to activation for pulse animation
            sceneVm.SceneActivated += OnSceneActivatedForPulse;
        }
    }

    private void OnSceneActivatedForPulse(object? sender, Guid sceneId)
    {
        if (_sceneElements.TryGetValue(sceneId, out var element))
        {
            AnimateScenePulse(element);
        }
    }

    private void AnimateScenePulse(UIElement element)
    {
        if (element.RenderTransform is not ScaleTransform scaleTransform)
            return;

        var scaleXAnimation = new DoubleAnimation
        {
            From = 1.0,
            To = AppConstants.Animation.PulseScaleFactor,
            Duration = new Duration(TimeSpan.FromMilliseconds(AppConstants.Animation.FastDurationMs)),
            AutoReverse = true,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        var scaleYAnimation = new DoubleAnimation
        {
            From = 1.0,
            To = AppConstants.Animation.PulseScaleFactor,
            Duration = new Duration(TimeSpan.FromMilliseconds(AppConstants.Animation.FastDurationMs)),
            AutoReverse = true,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        var storyboard = new Storyboard();
        storyboard.Children.Add(scaleXAnimation);
        storyboard.Children.Add(scaleYAnimation);
        Storyboard.SetTarget(scaleXAnimation, scaleTransform);
        Storyboard.SetTargetProperty(scaleXAnimation, "ScaleX");
        Storyboard.SetTarget(scaleYAnimation, scaleTransform);
        Storyboard.SetTargetProperty(scaleYAnimation, "ScaleY");
        storyboard.Begin();
    }
}
