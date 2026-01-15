using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using HueWindows.Constants;
using HueWindows.Core.ViewModels;
using HueWindows.Utilities;
using Windows.UI;

namespace HueWindows.Controls;

/// <summary>
/// Light card control for the room detail page.
/// Displays a single light with colored border effect and toggle.
/// </summary>
public sealed partial class LightCard : UserControl
{
    private bool _isLoaded;
    private Color _accentColor = Colors.White;
    private Color _currentIconColor = Colors.Gray;
    private SolidColorBrush? _iconBrush;
    private SolidColorBrush? _toggleBrush;
    private bool _useFirstBorder = true; // Toggle between two borders for cross-fade

    public LightItemViewModel? ViewModel => DataContext as LightItemViewModel;

    /// <summary>
    /// Event raised when the user requests to add this light to the dashboard.
    /// </summary>
    public event EventHandler<Guid>? AddToDashboardRequested;

    public LightCard()
    {
        this.InitializeComponent();
        this.DataContextChanged += OnDataContextChanged;
        this.Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        UpdateActiveState();
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
        if (e.PropertyName == nameof(LightItemViewModel.IsOn) ||
            e.PropertyName == nameof(LightItemViewModel.CurrentColorRgb))
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
    }

    private void UpdateActiveState()
    {
        if (!_isLoaded || ViewModel == null) return;

        var isActive = ViewModel.IsOn;
        var colorRgb = ViewModel.CurrentColorRgb;

        // Get light color
        if (colorRgb.HasValue && isActive)
        {
            var (r, g, b) = colorRgb.Value;
            _accentColor = Color.FromArgb(255, r, g, b);
        }
        else
        {
            _accentColor = Colors.Gray;
        }

        UpdateBorderEffect(isActive);
        UpdateToggleColor(isActive);
        UpdateIconColor(isActive);
    }

    private void UpdateBorderEffect(bool isActive)
    {
        if (!isActive)
        {
            // Fade out both borders
            AnimationHelper.AnimateOpacity(OutlineBorder, 0.0);
            AnimationHelper.AnimateOpacity(OutlineBorder2, 0.0);
            return;
        }

        // Cross-fade between two borders for smooth color transitions
        var newBorder = _useFirstBorder ? OutlineBorder : OutlineBorder2;
        var oldBorder = _useFirstBorder ? OutlineBorder2 : OutlineBorder;

        // Set new color on the incoming border
        newBorder.BorderBrush = new SolidColorBrush(_accentColor);

        // Cross-fade: fade in new, fade out old
        AnimationHelper.AnimateOpacity(newBorder, 1.0);
        AnimationHelper.AnimateOpacity(oldBorder, 0.0);

        // Toggle for next update
        _useFirstBorder = !_useFirstBorder;
    }

    private void UpdateToggleColor(bool isActive)
    {
        if (LightToggle == null) return;

        var targetColor = isActive ? _accentColor : Colors.Transparent;

        // Initialize brush if needed
        if (_toggleBrush == null)
        {
            _toggleBrush = new SolidColorBrush(Colors.Transparent);
            LightToggle.Background = _toggleBrush;
        }

        // Animate color change
        AnimationHelper.AnimateColor(_toggleBrush, ((SolidColorBrush)LightToggle.Background).Color, targetColor);
    }

    private void UpdateIconColor(bool isActive)
    {
        if (LightIcon == null) return;

        var targetColor = isActive
            ? _accentColor
            : Color.FromArgb(AppConstants.Colors.InactiveIconAlpha, 255, 255, 255);

        // Initialize brush if needed
        if (_iconBrush == null)
        {
            _iconBrush = new SolidColorBrush(_currentIconColor);
            LightIcon.Foreground = _iconBrush;
        }

        // Animate color change
        AnimationHelper.AnimateColor(_iconBrush, _currentIconColor, targetColor);
        _currentIconColor = targetColor;
    }

    private void CardRoot_Tapped(object sender, TappedRoutedEventArgs e)
    {
        // Prevent navigation if clicking the toggle
        if (e.OriginalSource is DependencyObject obj && obj.IsDescendantOf(LightToggle))
        {
            return;
        }

        ViewModel?.TapLightCommand.Execute(null);
        e.Handled = true;
    }

    private void LightToggle_Tapped(object sender, TappedRoutedEventArgs e)
    {
        // Explicitly toggle - the binding handles the state change
        if (ViewModel != null)
        {
            ViewModel.IsOn = !ViewModel.IsOn;
        }
        e.Handled = true;
    }

    private void CardRoot_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        VisualStateManager.GoToState(this, "Hover", true);
    }

    private void CardRoot_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        VisualStateManager.GoToState(this, "Default", true);
    }

    private void AddToDashboard_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null)
        {
            AddToDashboardRequested?.Invoke(this, ViewModel.LightId);
        }
    }

}
