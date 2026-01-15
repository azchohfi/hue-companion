using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.UI;

namespace HueWindows.Controls;

/// <summary>
/// A flyout control containing a ring-style color picker with debounced color changes.
/// </summary>
public sealed partial class ColorPickerFlyout : UserControl
{
    private DispatcherTimer? _debounceTimer;
    private Color _pendingColor;
    private bool _isUpdatingColor;

    /// <summary>
    /// Event raised when the color changes (debounced).
    /// </summary>
    public event EventHandler<Color>? ColorChanged;

    /// <summary>
    /// Gets or sets the initial color to display.
    /// </summary>
    public Color InitialColor
    {
        get => (Color)GetValue(InitialColorProperty);
        set => SetValue(InitialColorProperty, value);
    }

    public static readonly DependencyProperty InitialColorProperty =
        DependencyProperty.Register(nameof(InitialColor), typeof(Color), typeof(ColorPickerFlyout),
            new PropertyMetadata(Colors.White, OnInitialColorChanged));

    private static void OnInitialColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ColorPickerFlyout flyout && e.NewValue is Color color)
        {
            flyout.SetColorWithoutEvent(color);
        }
    }

    public ColorPickerFlyout()
    {
        this.InitializeComponent();
    }

    private void SetColorWithoutEvent(Color color)
    {
        _isUpdatingColor = true;
        ColorPicker.Color = color;
        _isUpdatingColor = false;
    }

    private void ColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
    {
        if (_isUpdatingColor) return;

        _pendingColor = args.NewColor;

        if (_debounceTimer == null)
        {
            _debounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(150)
            };
            _debounceTimer.Tick += (s, e) =>
            {
                _debounceTimer.Stop();
                ColorChanged?.Invoke(this, _pendingColor);
            };
        }

        _debounceTimer.Stop();
        _debounceTimer.Start();
    }
}
