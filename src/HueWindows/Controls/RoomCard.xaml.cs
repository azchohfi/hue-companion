using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using HueWindows.Core.ViewModels;
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

    // Current accent color
    private Color _accentColor = Colors.White;

    // Constants
    private const double DragThreshold = 10;
    private const double PixelsPerPercent = 3;
    private const int ThrottleMs = 100;

    public RoomCardViewModel? ViewModel => DataContext as RoomCardViewModel;

    public RoomCard()
    {
        this.InitializeComponent();
        this.DataContextChanged += OnDataContextChanged;
        this.Loaded += OnLoaded;
        this.SizeChanged += OnSizeChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        UpdateActiveState();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateBrightnessBar();
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
            e.PropertyName == nameof(RoomCardViewModel.BackgroundColorRgb))
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

        // Update visual state
        VisualStateManager.GoToState(this, isActive ? "Active" : "Inactive", true);

        // Update glow
        UpdateGlowEffect(isActive);

        // Update toggle color
        UpdateToggleColor(isActive);

        // Update icon color
        UpdateIconColor(isActive);

        // Update brightness bar
        UpdateBrightnessBar();
    }

    private void UpdateGlowEffect(bool isActive)
    {
        if (isActive)
        {
            // Simple colored border for the glow effect
            GlowBorder.BorderBrush = new SolidColorBrush(_accentColor);
            GlowBorder.Opacity = 0.8;
        }
        else
        {
            GlowBorder.Opacity = 0;
        }
    }

    private void UpdateToggleColor(bool isActive)
    {
        try
        {
            if (RoomToggle == null) return;

            var roomColorBrush = new SolidColorBrush(_accentColor);

            if (isActive)
            {
                var hoverColor = Color.FromArgb(255,
                    (byte)Math.Min(255, _accentColor.R + 20),
                    (byte)Math.Min(255, _accentColor.G + 20),
                    (byte)Math.Min(255, _accentColor.B + 20));
                var pressedColor = Color.FromArgb(255,
                    (byte)Math.Max(0, _accentColor.R - 20),
                    (byte)Math.Max(0, _accentColor.G - 20),
                    (byte)Math.Max(0, _accentColor.B - 20));

                RoomToggle.Resources["ToggleSwitchFillOn"] = roomColorBrush;
                RoomToggle.Resources["ToggleSwitchFillOnPointerOver"] = new SolidColorBrush(hoverColor);
                RoomToggle.Resources["ToggleSwitchFillOnPressed"] = new SolidColorBrush(pressedColor);
            }
            else
            {
                RoomToggle.Resources.Remove("ToggleSwitchFillOn");
                RoomToggle.Resources.Remove("ToggleSwitchFillOnPointerOver");
                RoomToggle.Resources.Remove("ToggleSwitchFillOnPressed");
            }

            // Force immediate visual update by finding and setting the track fill directly
            // Resources only apply on next state change, so we need to update the actual element
            RoomToggle.ApplyTemplate();
            var track = FindDescendant<Rectangle>(RoomToggle, "SwitchKnobBounds");
            if (track != null && isActive)
            {
                track.Fill = roomColorBrush;
            }
        }
        catch
        {
            // Silently handle any resource errors
        }
    }

    private static T? FindDescendant<T>(DependencyObject parent, string name) where T : FrameworkElement
    {
        int childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T element && element.Name == name)
            {
                return element;
            }
            var result = FindDescendant<T>(child, name);
            if (result != null)
            {
                return result;
            }
        }
        return null;
    }

    private void UpdateIconColor(bool isActive)
    {
        if (RoomIcon == null) return;

        if (isActive)
        {
            RoomIcon.Foreground = new SolidColorBrush(_accentColor);
        }
        else
        {
            RoomIcon.Foreground = new SolidColorBrush(Color.FromArgb(128, 255, 255, 255));
        }
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
            BrightnessFill.Background = new SolidColorBrush(Color.FromArgb(96, 255, 255, 255));
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

        if (Math.Abs(_cumulativeDeltaY) > DragThreshold)
        {
            if (!_isDragging)
            {
                _isDragging = true;
                VisualStateManager.GoToState(this, "Dragging", true);
            }

            var brightnessChange = -_cumulativeDeltaY / (PixelsPerPercent * 100);
            var newBrightness = Math.Clamp(_startBrightness + brightnessChange, 0.0, 1.0);
            _pendingBrightness = newBrightness;

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
            ViewModel.SetBrightnessCommand.Execute(_pendingBrightness);
        }

        _isDragging = false;
        VisualStateManager.GoToState(this, "Normal", true);
    }

    private void CardRoot_Tapped(object sender, TappedRoutedEventArgs e)
    {
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
