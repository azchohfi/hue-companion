using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
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
        LinearGradientBrush borderBrush;
        var colors = ViewModel.LightColors;

        if (colors.Count > 1)
        {
            borderBrush = new LinearGradientBrush();
            borderBrush.StartPoint = new Windows.Foundation.Point(0, 0);
            borderBrush.EndPoint = new Windows.Foundation.Point(1, 1);

            for (int i = 0; i < colors.Count; i++)
            {
                var (r, g, b) = colors[i];
                var color = Color.FromArgb(255, r, g, b);
                borderBrush.GradientStops.Add(new GradientStop
                {
                    Color = color,
                    Offset = (double)i / (colors.Count - 1)
                });
            }
        }
        else if (colors.Count == 1)
        {
            var (r, g, b) = colors[0];
            var color = Color.FromArgb(255, r, g, b);
            borderBrush = new LinearGradientBrush();
            borderBrush.GradientStops.Add(new GradientStop { Color = color, Offset = 0 });
            borderBrush.GradientStops.Add(new GradientStop { Color = color, Offset = 1 });
        }
        else
        {
            // Fallback when no colors available
            borderBrush = new LinearGradientBrush();
            borderBrush.GradientStops.Add(new GradientStop { Color = _accentColor, Offset = 0 });
            borderBrush.GradientStops.Add(new GradientStop { Color = _accentColor, Offset = 1 });
        }

        // Set the brush FIRST, then animate opacity
        OutlineBorder.BorderBrush = borderBrush;

        // Animate border opacity
        var targetOpacity = isActive ? 1.0 : 0.0;
        AnimateBorderOpacity(targetOpacity);
    }

    private void AnimateBorderOpacity(double targetOpacity)
    {
        var animation = new DoubleAnimation
        {
            To = targetOpacity,
            Duration = new Duration(TimeSpan.FromMilliseconds(300)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        Storyboard.SetTarget(animation, OutlineBorder);
        Storyboard.SetTargetProperty(animation, "Opacity");
        storyboard.Begin();
    }

    private void UpdateToggleColor(bool isActive)
    {
        try
        {
            if (RoomToggle == null) return;

            Brush fillBrush;
            
            if (isActive && ViewModel != null && ViewModel.LightColors.Count > 1)
            {
                 // Create gradient for toggle
                var gradient = new LinearGradientBrush();
                gradient.StartPoint = new Windows.Foundation.Point(0, 0);
                gradient.EndPoint = new Windows.Foundation.Point(1, 0); // Horizontal for toggle
                
                var colors = ViewModel.LightColors;
                for (int i = 0; i < colors.Count; i++)
                {
                    var (r, g, b) = colors[i];
                    var color = Color.FromArgb(255, r, g, b);
                    gradient.GradientStops.Add(new GradientStop 
                    { 
                        Color = color, 
                        Offset = (double)i / (colors.Count - 1) 
                    });
                }
                fillBrush = gradient;
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
        if (e.OriginalSource is DependencyObject obj && IsDescendantOf(obj, RoomToggle))
        {
            return;
        }

        if (!_isDragging && ViewModel != null)
        {
            ViewModel.TapRoomCommand.Execute(null);
        }

        e.Handled = true;
    }

    private bool IsDescendantOf(DependencyObject? current, DependencyObject target)
    {
        while (current != null)
        {
            if (current == target) return true;
            try 
            {
                current = VisualTreeHelper.GetParent(current);
            }
            catch
            {
                // In some cases (like popups) GetParent might fail or return null
                return false;
            }
        }
        return false;
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
