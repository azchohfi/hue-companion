using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using HueWindows.Core.ViewModels;
using Windows.Foundation;

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

    public RoomCard()
    {
        this.InitializeComponent();
        this.DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (ViewModel != null)
        {
            // Subscribe to property changes to update visual states
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            UpdateOnOffState();
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RoomCardViewModel.IsOn))
        {
            UpdateOnOffState();
        }
    }

    private void UpdateOnOffState()
    {
        if (ViewModel == null) return;

        VisualStateManager.GoToState(this, ViewModel.IsOn ? "On" : "Off", true);
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
}
