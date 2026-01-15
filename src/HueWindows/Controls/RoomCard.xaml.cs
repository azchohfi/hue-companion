using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using HueWindows.Constants;
using HueWindows.Core.ViewModels;
using HueWindows.Utilities;
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

    public IRoomCardViewModel? ViewModel => DataContext as IRoomCardViewModel;

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
        var colors = ViewModel.LightColors;
        var borderBrush = colors.Count > 0
            ? BrushFactory.CreateDiagonalGradient(colors)
            : BrushFactory.CreateDiagonalGradient(new[] { (_accentColor.R, _accentColor.G, _accentColor.B) });

        // Set the brush FIRST, then animate opacity
        OutlineBorder.BorderBrush = borderBrush;

        // Animate border opacity
        var targetOpacity = isActive ? 1.0 : 0.0;
        AnimationHelper.AnimateOpacity(OutlineBorder, targetOpacity);
    }

    private void UpdateToggleColor(bool isActive)
    {
        try
        {
            if (RoomToggle == null) return;

            Brush fillBrush;

            if (isActive && ViewModel != null && ViewModel.LightColors.Count > 1)
            {
                // Create gradient for toggle (horizontal)
                fillBrush = BrushFactory.CreateHorizontalGradient(ViewModel.LightColors);
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

        if (isActive && ViewModel != null)
        {
            var colors = ViewModel.LightColors;
            if (colors.Count > 1)
            {
                // Create gradient for icon matching toggle/border
                RoomIcon.Foreground = BrushFactory.CreateDiagonalGradient(colors);
            }
            else
            {
                RoomIcon.Foreground = new SolidColorBrush(_accentColor);
            }
        }
        else
        {
            RoomIcon.Foreground = new SolidColorBrush(Color.FromArgb(AppConstants.Colors.InactiveIconAlpha, 255, 255, 255));
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
            BrightnessFill.Background = new SolidColorBrush(Color.FromArgb(AppConstants.Colors.InactiveBrightnessBarAlpha, 255, 255, 255));
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

        if (Math.Abs(_cumulativeDeltaY) > AppConstants.BrightnessDrag.DragThreshold)
        {
            if (!_isDragging)
            {
                _isDragging = true;
                VisualStateManager.GoToState(this, "Dragging", true);
            }

            var brightnessChange = -_cumulativeDeltaY / (AppConstants.BrightnessDrag.PixelsPerPercent * 100);
            var newBrightness = Math.Clamp(_startBrightness + brightnessChange, 0.0, 1.0);
            _pendingBrightness = newBrightness;

            var now = DateTime.UtcNow;
            if ((now - _lastBrightnessUpdate).TotalMilliseconds >= AppConstants.BrightnessDrag.ThrottleMs)
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
        if (e.OriginalSource is DependencyObject obj && obj.IsDescendantOf(RoomToggle))
        {
            return;
        }

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
