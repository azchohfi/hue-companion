using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HueWindows.Core.Models;
using HueWindows.Core.Services.Interfaces;

namespace HueWindows.Core.ViewModels;

/// <summary>
/// ViewModel for the individual light detail/control page.
/// </summary>
public partial class LightDetailViewModel : ObservableObject
{
    private readonly IHueBridgeService _bridgeService;
    private Guid _lightId;

    [ObservableProperty]
    private string _lightName = string.Empty;

    [ObservableProperty]
    private bool _isOn;

    [ObservableProperty]
    private double _brightness;

    [ObservableProperty]
    private bool _supportsColor;

    [ObservableProperty]
    private bool _supportsColorTemperature;

    [ObservableProperty]
    private HueColor? _currentColor;

    [ObservableProperty]
    private int _colorTemperature = 350; // Default to middle of range

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isColorMode = true; // true = color, false = temperature

    public int BrightnessPercent => (int)(Brightness * 100);

    /// <summary>
    /// Color temperature range: 153 (cool/6500K) to 500 (warm/2000K).
    /// </summary>
    public int MinColorTemp => 153;
    public int MaxColorTemp => 500;

    public LightDetailViewModel(IHueBridgeService bridgeService)
    {
        _bridgeService = bridgeService;
    }

    public async Task LoadLightAsync(Guid lightId)
    {
        _lightId = lightId;
        IsLoading = true;

        try
        {
            var light = await _bridgeService.GetLightAsync(lightId);
            if (light == null) return;

            LightName = light.Name;
            IsOn = light.IsOn;
            Brightness = light.Brightness;
            SupportsColor = light.SupportsColor;
            SupportsColorTemperature = light.SupportsColorTemperature;
            CurrentColor = light.CurrentColor;

            if (light.ColorTemperature.HasValue)
            {
                ColorTemperature = light.ColorTemperature.Value;
            }

            // Default to color mode if supported, otherwise temperature
            IsColorMode = SupportsColor;
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnIsOnChanged(bool value)
    {
        _ = _bridgeService.SetLightOnAsync(_lightId, value);
    }

    [RelayCommand]
    private async Task SetBrightnessAsync(double brightness)
    {
        Brightness = Math.Clamp(brightness, 0.0, 1.0);
        OnPropertyChanged(nameof(BrightnessPercent));

        await _bridgeService.SetLightBrightnessAsync(_lightId, Brightness);

        // Auto-turn on if brightness > 0
        if (Brightness > 0 && !IsOn)
        {
            _isOn = true;
            OnPropertyChanged(nameof(IsOn));
        }
    }

    [RelayCommand]
    private async Task SetColorAsync(HueColor color)
    {
        CurrentColor = color;
        IsColorMode = true;

        await _bridgeService.SetLightColorAsync(_lightId, color);
    }

    [RelayCommand]
    private async Task SetColorFromRgbAsync((byte R, byte G, byte B) rgb)
    {
        var color = HueColor.FromRgb(rgb.R, rgb.G, rgb.B);
        await SetColorAsync(color);
    }

    [RelayCommand]
    private async Task SetColorTemperatureAsync(int mirek)
    {
        ColorTemperature = Math.Clamp(mirek, MinColorTemp, MaxColorTemp);
        IsColorMode = false;

        await _bridgeService.SetLightTemperatureAsync(_lightId, ColorTemperature);
    }

    [RelayCommand]
    private void SwitchToColorMode()
    {
        IsColorMode = true;
    }

    [RelayCommand]
    private void SwitchToTemperatureMode()
    {
        IsColorMode = false;
    }
}
