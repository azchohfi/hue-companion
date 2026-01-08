using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using HueWindows.Core.ViewModels;
using Windows.UI;

namespace HueWindows.Views;

/// <summary>
/// Page for controlling an individual light.
/// </summary>
public sealed partial class LightDetailPage : Page
{
    public LightDetailViewModel ViewModel { get; }

    private bool _isUpdatingColor;
#pragma warning disable CS0649 // Field is assigned dynamically
    private bool _isUpdatingTemperature;
#pragma warning restore CS0649

    public LightDetailPage()
    {
        ViewModel = App.Services.GetRequiredService<LightDetailViewModel>();

        this.InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is Guid lightId)
        {
            await ViewModel.LoadLightAsync(lightId);

            // Set initial color picker value if color is available
            if (ViewModel.CurrentColor != null)
            {
                var rgb = ViewModel.CurrentColor.ToRgb(1.0);
                _isUpdatingColor = true;
                LightColorPicker.Color = Color.FromArgb(255, rgb.R, rgb.G, rgb.B);
                _isUpdatingColor = false;
            }
        }
    }

    private void BrightnessSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (Math.Abs(e.NewValue - e.OldValue) > 0.5)
        {
            ViewModel.SetBrightnessCommand.Execute(e.NewValue / 100.0);
        }
    }

    private void ColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
    {
        if (_isUpdatingColor) return;

        var color = args.NewColor;
        ViewModel.SetColorFromRgbCommand.Execute((color.R, color.G, color.B));
    }

    private void TemperatureSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingTemperature) return;

        if (Math.Abs(e.NewValue - e.OldValue) > 1)
        {
            ViewModel.SetColorTemperatureCommand.Execute((int)e.NewValue);
        }
    }

    /// <summary>
    /// Helper to check if both color modes are supported.
    /// </summary>
    public Visibility BothColorModesSupported()
    {
        return ViewModel.SupportsColor && ViewModel.SupportsColorTemperature
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    /// <summary>
    /// Helper to show color picker.
    /// </summary>
    public Visibility ShowColorPicker()
    {
        return ViewModel.SupportsColor && ViewModel.IsColorMode
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    /// <summary>
    /// Helper to show temperature picker.
    /// </summary>
    public Visibility ShowTemperaturePicker()
    {
        return ViewModel.SupportsColorTemperature && !ViewModel.IsColorMode
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    /// <summary>
    /// Helper to invert a boolean.
    /// </summary>
    public bool InvertBool(bool value) => !value;

    /// <summary>
    /// Helper to check if error message exists.
    /// </summary>
    public bool HasErrorMessage(string? message) => !string.IsNullOrEmpty(message);
}
