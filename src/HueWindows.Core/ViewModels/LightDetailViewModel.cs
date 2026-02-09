using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HueWindows.Core.Models;
using HueWindows.Core.Services.Interfaces;
using HueWindows.Core.Utilities;

namespace HueWindows.Core.ViewModels;

/// <summary>
/// ViewModel for the individual light detail/control page.
/// </summary>
public partial class LightDetailViewModel : ObservableObject
{
    private readonly IMultiBridgeService _multiBridgeService;
    private IHueBridgeService? _bridgeService;
    private Guid _lightId;
    private string? _bridgeId;

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

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _productName;

    [ObservableProperty]
    private string? _firmwareVersion;

    [ObservableProperty]
    private string? _modelId;

    [ObservableProperty]
    private LightArchetype _archetype;

    [ObservableProperty]
    private PowerOnPreset _powerOnPreset;

    [ObservableProperty]
    private double _powerOnBrightness = 1.0;

    [ObservableProperty]
    private HueColor? _powerOnColor;

    [ObservableProperty]
    private bool _hasPowerOnConfig;

    private Guid? _deviceId;

    public Guid LightId => _lightId;

    public int BrightnessPercent => (int)(Brightness * 100);

    /// <summary>
    /// Color temperature range: 153 (cool/6500K) to 500 (warm/2000K).
    /// </summary>
    public int MinColorTemp => 153;
    public int MaxColorTemp => 500;

    public LightDetailViewModel(IMultiBridgeService multiBridgeService)
    {
        _multiBridgeService = multiBridgeService;
    }

    /// <summary>
    /// Sets the bridge ID for this light, allowing lookup of the correct bridge service.
    /// Call this before LoadLightAsync when navigating with bridge context.
    /// </summary>
    public void SetBridgeId(string? bridgeId)
    {
        _bridgeId = bridgeId;
        if (bridgeId != null)
        {
            _bridgeService = _multiBridgeService.GetBridgeService(bridgeId);
        }
        else
        {
            _bridgeService = _multiBridgeService.GetDefaultBridgeService();
        }
    }

    public async Task LoadLightAsync(Guid lightId)
    {
        _lightId = lightId;
        IsLoading = true;
        ErrorMessage = null;

        // Ensure we have a bridge service (fallback to default if not set)
        if (_bridgeService == null)
        {
            _bridgeService = _multiBridgeService.GetDefaultBridgeService();
        }

        if (_bridgeService == null)
        {
            ErrorMessage = "No bridge connected. Please configure a bridge in Settings.";
            IsLoading = false;
            return;
        }

        var lightResult = await _bridgeService.GetLightAsync(lightId);

        if (lightResult.IsFailure)
        {
            ErrorMessage = lightResult.Error;
            IsLoading = false;
            return;
        }

        var light = lightResult.Value!;
        LightName = light.Name;
        _isOn = light.IsOn; // Use backing field to avoid triggering OnIsOnChanged → SetLightOnAsync
        OnPropertyChanged(nameof(IsOn));
        Brightness = light.Brightness;
        SupportsColor = light.SupportsColor;
        SupportsColorTemperature = light.SupportsColorTemperature;
        CurrentColor = light.CurrentColor;
        Archetype = light.Archetype;
        _deviceId = light.DeviceId;
        ProductName = light.ProductName;
        FirmwareVersion = light.FirmwareVersion;
        ModelId = light.ModelId;

        if (light.ColorTemperature.HasValue)
        {
            ColorTemperature = light.ColorTemperature.Value;
        }

        // Load power-on behavior
        if (light.PowerOnPreset.HasValue)
        {
            PowerOnPreset = light.PowerOnPreset.Value;
            HasPowerOnConfig = true;
            if (light.PowerOnBrightness.HasValue)
                PowerOnBrightness = light.PowerOnBrightness.Value;
            if (light.PowerOnColor != null)
                PowerOnColor = light.PowerOnColor;
        }

        // Default to color mode if supported, otherwise temperature
        IsColorMode = SupportsColor;

        IsLoading = false;
    }

    partial void OnIsOnChanged(bool value)
    {
        if (_bridgeService == null) return;
        _bridgeService.SetLightOnAsync(_lightId, value).FireAndForget();
    }

    [RelayCommand]
    private async Task SetBrightnessAsync(double brightness)
    {
        Brightness = Math.Clamp(brightness, 0.0, 1.0);
        OnPropertyChanged(nameof(BrightnessPercent));

        if (_bridgeService != null)
        {
            await _bridgeService.SetLightBrightnessAsync(_lightId, Brightness);
        }

        // Auto-turn on if brightness > 0
        // Set field directly to update UI without triggering OnIsOnChanged (which would send redundant API call)
        if (Brightness > 0 && !IsOn)
        {
#pragma warning disable MVVMTK0034
            _isOn = true;
#pragma warning restore MVVMTK0034
            OnPropertyChanged(nameof(IsOn));
        }
    }

    [RelayCommand]
    private async Task SetColorAsync(HueColor color)
    {
        CurrentColor = color;
        IsColorMode = true;

        if (_bridgeService != null)
        {
            await _bridgeService.SetLightColorAsync(_lightId, color);
        }
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

        if (_bridgeService != null)
        {
            await _bridgeService.SetLightTemperatureAsync(_lightId, ColorTemperature);
        }
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

    public async Task<Result> RenameAsync(string newName)
    {
        if (_bridgeService == null || _deviceId == null)
            return Result.Failure("Not connected.");

        var result = await _bridgeService.RenameLightAsync(_lightId, _deviceId.Value, newName);
        if (result.IsSuccess)
            LightName = newName;
        else
            ErrorMessage = result.Error;

        return result;
    }

    public async Task<Result> IdentifyAsync()
    {
        if (_bridgeService == null)
            return Result.Failure("Not connected.");

        return await _bridgeService.IdentifyLightAsync(_lightId);
    }

    [RelayCommand]
    private async Task SetPowerOnPresetAsync(PowerOnPreset preset)
    {
        if (_bridgeService == null) return;

        Result result;
        if (preset == PowerOnPreset.Custom)
        {
            result = await _bridgeService.SetPowerOnCustomAsync(_lightId, PowerOnBrightness, PowerOnColor);
        }
        else
        {
            result = await _bridgeService.SetPowerOnPresetAsync(_lightId, preset);
        }

        if (result.IsSuccess)
            PowerOnPreset = preset;
        else
            ErrorMessage = result.Error;
    }

    [RelayCommand]
    private async Task SetPowerOnCustomAsync()
    {
        if (_bridgeService == null) return;

        var result = await _bridgeService.SetPowerOnCustomAsync(_lightId, PowerOnBrightness, PowerOnColor);
        if (result.IsSuccess)
            PowerOnPreset = PowerOnPreset.Custom;
        else
            ErrorMessage = result.Error;
    }
}
