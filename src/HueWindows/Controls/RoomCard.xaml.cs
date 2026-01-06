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

    // Store default brushes for restoration when room is off
    private Brush? _defaultBackgroundBrush;
    private bool _isHovering;

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
        UpdateColors();
    }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (ViewModel != null)
        {
            // Subscribe to property changes to update visual states
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;

            // Only update colors if the control is already loaded
            if (_isLoaded)
            {
                UpdateOnOffState();
                UpdateColors();
            }
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RoomCardViewModel.IsOn))
        {
            UpdateOnOffState();
            UpdateColors();
        }
        else if (e.PropertyName == nameof(RoomCardViewModel.BackgroundColorRgb) ||
                 e.PropertyName == nameof(RoomCardViewModel.UseBlackText))
        {
            UpdateColors();
        }
    }

    private void UpdateOnOffState()
    {
        if (ViewModel == null) return;

        VisualStateManager.GoToState(this, ViewModel.IsOn ? "On" : "Off", true);
    }

    private void UpdateColors()
    {
        try
        {
            if (ViewModel == null) return;

            // Check if UI is ready
            if (CardRoot == null || RoomNameText == null)
            {
                return;
            }

            // Store default background brush on first call - use a safe fallback
            if (_defaultBackgroundBrush == null)
            {
                try
                {
                    _defaultBackgroundBrush = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"];
                }
                catch
                {
                    _defaultBackgroundBrush = new SolidColorBrush(Color.FromArgb(255, 40, 40, 40));
                }
            }

            var lightColors = ViewModel.LightColors;
            if (lightColors.Count > 0 && ViewModel.IsOn)
            {
                // Create gradient or solid background based on number of colors
                if (lightColors.Count == 1)
                {
                    var (r, g, b) = lightColors[0];
                    var bgColor = _isHovering
                        ? Color.FromArgb(210, (byte)Math.Min(255, r + 15), (byte)Math.Min(255, g + 15), (byte)Math.Min(255, b + 15))
                        : Color.FromArgb(230, r, g, b);
                    CardRoot.Background = new SolidColorBrush(bgColor);
                }
                else
                {
                    // Create a horizontal linear gradient with all colors
                    var gradient = new LinearGradientBrush
                    {
                        StartPoint = new Windows.Foundation.Point(0, 0.5),
                        EndPoint = new Windows.Foundation.Point(1, 0.5)
                    };

                    byte alpha = _isHovering ? (byte)210 : (byte)230;
                    for (int i = 0; i < lightColors.Count; i++)
                    {
                        var (r, g, b) = lightColors[i];
                        var color = _isHovering
                            ? Color.FromArgb(alpha, (byte)Math.Min(255, r + 15), (byte)Math.Min(255, g + 15), (byte)Math.Min(255, b + 15))
                            : Color.FromArgb(alpha, r, g, b);

                        double offset = lightColors.Count == 1 ? 0.5 : (double)i / (lightColors.Count - 1);
                        gradient.GradientStops.Add(new GradientStop { Color = color, Offset = offset });
                    }

                    CardRoot.Background = gradient;
                }

                // Update text colors based on contrast
                if (ViewModel.UseBlackText)
                {
                    var blackBrush = new SolidColorBrush(Colors.Black);
                    var darkGrayBrush = new SolidColorBrush(Color.FromArgb(255, 40, 40, 40));

                    RoomNameText.Foreground = blackBrush;
                    LightCountText.Foreground = darkGrayBrush;
                    BrightnessText.Foreground = darkGrayBrush;
                    RoomIcon.Foreground = blackBrush;
                }
                else
                {
                    var whiteBrush = new SolidColorBrush(Colors.White);
                    var lightGrayBrush = new SolidColorBrush(Color.FromArgb(255, 200, 200, 200));

                    RoomNameText.Foreground = whiteBrush;
                    LightCountText.Foreground = lightGrayBrush;
                    BrightnessText.Foreground = lightGrayBrush;
                    RoomIcon.Foreground = whiteBrush;
                }
            }
            else
            {
                // Restore default theme colors
                CardRoot.Background = _defaultBackgroundBrush
                    ?? (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"];

                // Reset text to theme defaults
                RoomNameText.ClearValue(TextBlock.ForegroundProperty);
                LightCountText.Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
                BrightnessText.Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
                RoomIcon.ClearValue(FontIcon.ForegroundProperty);
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
            _isHovering = true;
            ApplyHoverEffect(true);
        }
    }

    private void CardRoot_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging)
        {
            _isHovering = false;
            ApplyHoverEffect(false);
        }
    }

    private void ApplyHoverEffect(bool isHovering)
    {
        if (ViewModel == null || CardRoot == null) return;

        // Just call UpdateColors which handles both normal and hover states
        UpdateColors();
    }
}
