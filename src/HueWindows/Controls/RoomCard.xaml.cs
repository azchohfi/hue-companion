using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using HueWindows.Core.ViewModels;
using Windows.Foundation;
using Windows.UI;

namespace HueWindows.Controls;

/// <summary>
/// Widget-like room card control for the dashboard.
/// Supports tap to navigate and drag to adjust brightness.
/// </summary>
public sealed partial class RoomCard : UserControl
{
    private bool _isDragging;
    private Point _dragStartPoint;
    private double _startBrightness;
    private bool _hasMovedEnough;

    // Minimum drag distance before we consider it a drag (vs tap)
    private const double DragThreshold = 10;

    // Vertical pixels per 1% brightness change
    private const double PixelsPerPercent = 3;

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
        if (e.PropertyName == nameof(RoomCardViewModel.IsOn) ||
            e.PropertyName == nameof(RoomCardViewModel.BackgroundColorRgb))
        {
            UpdateOnOffState();
            UpdateToggleColor();
        }
    }

    private void UpdateOnOffState()
    {
        if (ViewModel == null) return;

        VisualStateManager.GoToState(this, ViewModel.IsOn ? "On" : "Off", true);
    }

    private void UpdateToggleColor()
    {
        try
        {
            if (ViewModel == null || RoomToggle == null) return;

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
            // Silently handle any resource loading errors
        }
    }

    private void CardRoot_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (ViewModel == null) return;

        var point = e.GetCurrentPoint(CardRoot);

        // Only handle left button / touch
        if (!point.Properties.IsLeftButtonPressed) return;

        _isDragging = true;
        _hasMovedEnough = false;
        _dragStartPoint = point.Position;
        _startBrightness = ViewModel.Brightness;

        // Capture pointer for tracking outside control bounds
        CardRoot.CapturePointer(e.Pointer);

        e.Handled = true;
    }

    private void CardRoot_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging || ViewModel == null) return;

        var currentPoint = e.GetCurrentPoint(CardRoot).Position;

        // Calculate distance moved
        var deltaY = _dragStartPoint.Y - currentPoint.Y;
        var totalDistance = Math.Sqrt(
            Math.Pow(currentPoint.X - _dragStartPoint.X, 2) +
            Math.Pow(currentPoint.Y - _dragStartPoint.Y, 2));

        // Check if we've moved enough to be considered a drag
        if (totalDistance > DragThreshold)
        {
            _hasMovedEnough = true;
            VisualStateManager.GoToState(this, "Dragging", true);

            // Calculate brightness change (moving up increases brightness)
            var brightnessChange = deltaY / (PixelsPerPercent * 100);
            var newBrightness = Math.Clamp(_startBrightness + brightnessChange, 0.0, 1.0);

            // Update the view model (this triggers UI update via binding)
            ViewModel.SetBrightnessCommand.Execute(newBrightness);
        }

        e.Handled = true;
    }

    private void CardRoot_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        EndDrag(e.Pointer);
        e.Handled = true;
    }

    private void CardRoot_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        EndDrag(null);
    }

    private void EndDrag(Pointer? pointer)
    {
        if (!_isDragging) return;

        _isDragging = false;

        if (pointer != null)
        {
            CardRoot.ReleasePointerCapture(pointer);
        }

        VisualStateManager.GoToState(this, "Normal", true);
    }

    private void CardRoot_Tapped(object sender, TappedRoutedEventArgs e)
    {
        // Only navigate if we didn't drag
        if (!_hasMovedEnough && ViewModel != null)
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
