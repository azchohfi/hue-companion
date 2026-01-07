using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using HueWindows.Core.ViewModels;
using Windows.UI;

namespace HueWindows.Controls;

/// <summary>
/// Widget-like room card control for the dashboard.
/// Supports tap to navigate and drag to adjust brightness.
/// </summary>
public sealed partial class RoomCard : UserControl
{
    private bool _isDragging;
    private double _startBrightness;
    private double _cumulativeDeltaY;
    private DateTime _lastBrightnessUpdate = DateTime.MinValue;
    private double _pendingBrightness;

    // Minimum drag distance before we consider it a drag (vs tap)
    private const double DragThreshold = 10;

    // Vertical pixels per 1% brightness change
    private const double PixelsPerPercent = 3;

    // Minimum time between brightness updates (throttle)
    private const int ThrottleMs = 100;

    public RoomCardViewModel? ViewModel => DataContext as RoomCardViewModel;

    private bool _isLoaded;

    public RoomCard()
    {
        this.InitializeComponent();
        this.DataContextChanged += OnDataContextChanged;
        this.Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        UpdateOnOffState();
        UpdateToggleColor();
    }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (ViewModel != null)
        {
            // Subscribe to property changes to update visual states
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;

            // Only update if the control is already loaded
            if (_isLoaded)
            {
                UpdateOnOffState();
                UpdateToggleColor();
            }
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Only update visual state when on/off changes (not during drag brightness changes)
        if (e.PropertyName == nameof(RoomCardViewModel.IsOn))
        {
            // Ensure UI updates happen on the UI thread
            if (DispatcherQueue?.HasThreadAccess == true)
            {
                UpdateOnOffState();
                UpdateToggleColor();
            }
            else
            {
                DispatcherQueue?.TryEnqueue(() =>
                {
                    UpdateOnOffState();
                    UpdateToggleColor();
                });
            }
        }
    }

    private void UpdateOnOffState()
    {
        if (!_isLoaded || ViewModel == null) return;

        VisualStateManager.GoToState(this, ViewModel.IsOn ? "On" : "Off", true);
    }

    private void UpdateToggleColor()
    {
        try
        {
            if (!_isLoaded || ViewModel == null || RoomToggle == null) return;

            var lightColors = ViewModel.LightColors;
            if (lightColors.Count > 0 && ViewModel.IsOn)
            {
                // Use the first color for the toggle "on" state
                var (r, g, b) = lightColors[0];
                var roomColor = Color.FromArgb(255, r, g, b);
                var roomColorBrush = new SolidColorBrush(roomColor);

                // Create slightly lighter/darker variants for hover/pressed states
                var hoverColor = Color.FromArgb(255,
                    (byte)Math.Min(255, r + 20),
                    (byte)Math.Min(255, g + 20),
                    (byte)Math.Min(255, b + 20));
                var pressedColor = Color.FromArgb(255,
                    (byte)Math.Max(0, r - 20),
                    (byte)Math.Max(0, g - 20),
                    (byte)Math.Max(0, b - 20));

                // Override toggle switch resources for this instance
                RoomToggle.Resources["ToggleSwitchFillOn"] = roomColorBrush;
                RoomToggle.Resources["ToggleSwitchFillOnPointerOver"] = new SolidColorBrush(hoverColor);
                RoomToggle.Resources["ToggleSwitchFillOnPressed"] = new SolidColorBrush(pressedColor);
            }
            else
            {
                // Reset to default accent colors
                RoomToggle.Resources.Remove("ToggleSwitchFillOn");
                RoomToggle.Resources.Remove("ToggleSwitchFillOnPointerOver");
                RoomToggle.Resources.Remove("ToggleSwitchFillOnPressed");
            }
        }
        catch
        {
            // Silently handle any resource errors
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

        // Accumulate vertical movement
        _cumulativeDeltaY += e.Delta.Translation.Y;

        // Check if we've moved enough to be considered a drag
        if (Math.Abs(_cumulativeDeltaY) > DragThreshold)
        {
            if (!_isDragging)
            {
                _isDragging = true;
                VisualStateManager.GoToState(this, "Dragging", true);
            }

            // Calculate brightness change (moving up increases brightness, hence negative deltaY)
            var brightnessChange = -_cumulativeDeltaY / (PixelsPerPercent * 100);
            var newBrightness = Math.Clamp(_startBrightness + brightnessChange, 0.0, 1.0);
            _pendingBrightness = newBrightness;

            // Throttle updates to bridge
            var now = DateTime.UtcNow;
            if ((now - _lastBrightnessUpdate).TotalMilliseconds >= ThrottleMs)
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
            // Send final brightness value
            ViewModel.SetBrightnessCommand.Execute(_pendingBrightness);
        }

        _isDragging = false;
        VisualStateManager.GoToState(this, "Normal", true);
    }

    private void CardRoot_Tapped(object sender, TappedRoutedEventArgs e)
    {
        // Only navigate if we didn't drag
        if (!_isDragging && ViewModel != null)
        {
            ViewModel.TapRoomCommand.Execute(null);
        }

        e.Handled = true;
    }

    private void CardRoot_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging)
        {
            VisualStateManager.GoToState(this, "Hover", true);
        }
    }

    private void CardRoot_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging)
        {
            VisualStateManager.GoToState(this, "Default", true);
        }
    }
}
