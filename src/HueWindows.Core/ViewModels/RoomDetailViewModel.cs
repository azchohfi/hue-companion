using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HueWindows.Core.Models;
using HueWindows.Core.Services.Interfaces;

namespace HueWindows.Core.ViewModels;

/// <summary>
/// ViewModel for the room detail page showing lights and scenes.
/// </summary>
public partial class RoomDetailViewModel : ObservableObject
{
    private readonly IHueBridgeService _bridgeService;
    private Guid _roomId;

    [ObservableProperty]
    private string _roomName = string.Empty;

    [ObservableProperty]
    private bool _isOn;

    [ObservableProperty]
    private double _brightness;

    [ObservableProperty]
    private ObservableCollection<LightItemViewModel> _lights = new();

    [ObservableProperty]
    private ObservableCollection<SceneItemViewModel> _scenes = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private SceneItemViewModel? _activeScene;

    /// <summary>
    /// Event raised when a light is selected for detail view.
    /// </summary>
    public event EventHandler<Guid>? LightSelected;

    public int BrightnessPercent => (int)(Brightness * 100);

    public RoomDetailViewModel(IHueBridgeService bridgeService)
    {
        _bridgeService = bridgeService;
    }

    public async Task LoadRoomAsync(Guid roomId)
    {
        _roomId = roomId;
        IsLoading = true;

        try
        {
            var room = await _bridgeService.GetRoomAsync(roomId);
            if (room == null) return;

            RoomName = room.Name;
            IsOn = room.IsOn;
            Brightness = room.Brightness;

            // Load lights
            Lights.Clear();
            foreach (var light in room.Lights)
            {
                var lightVm = new LightItemViewModel(light, _bridgeService);
                lightVm.LightTapped += (s, id) => LightSelected?.Invoke(this, id);
                Lights.Add(lightVm);
            }

            // Load scenes
            var scenes = await _bridgeService.GetScenesForRoomAsync(roomId);
            Scenes.Clear();
            foreach (var scene in scenes)
            {
                var sceneVm = new SceneItemViewModel(scene, _bridgeService);
                sceneVm.SceneActivated += OnSceneActivated;
                Scenes.Add(sceneVm);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnIsOnChanged(bool value)
    {
        _ = _bridgeService.SetRoomOnAsync(_roomId, value);
    }

    [RelayCommand]
    private async Task SetBrightnessAsync(double brightness)
    {
        Brightness = Math.Clamp(brightness, 0.0, 1.0);
        OnPropertyChanged(nameof(BrightnessPercent));

        await _bridgeService.SetRoomBrightnessAsync(_roomId, Brightness);
    }

    private void OnSceneActivated(object? sender, Guid sceneId)
    {
        // Update active scene visual state
        foreach (var scene in Scenes)
        {
            scene.IsActive = scene.SceneId == sceneId;
        }

        if (sender is SceneItemViewModel activeScene)
        {
            ActiveScene = activeScene;
        }
    }
}

/// <summary>
/// ViewModel for a light item in the room detail view.
/// </summary>
public partial class LightItemViewModel : ObservableObject
{
    private readonly IHueBridgeService _bridgeService;
    private readonly LightModel _light;

    public Guid LightId => _light.Id;

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private bool _isOn;

    [ObservableProperty]
    private double _brightness;

    [ObservableProperty]
    private bool _supportsColor;

    [ObservableProperty]
    private HueColor? _currentColor;

    /// <summary>
    /// Event raised when the light is tapped for detail view.
    /// </summary>
    public event EventHandler<Guid>? LightTapped;

    public int BrightnessPercent => (int)(Brightness * 100);

    /// <summary>
    /// Opacity for the light icon (1.0 when on, 0.4 when off).
    /// </summary>
    public double IconOpacity => IsOn ? 1.0 : 0.4;

    public LightItemViewModel(LightModel light, IHueBridgeService bridgeService)
    {
        _light = light;
        _bridgeService = bridgeService;

        _name = light.Name;
        _isOn = light.IsOn;
        _brightness = light.Brightness;
        _supportsColor = light.SupportsColor;
        _currentColor = light.CurrentColor;
    }

    partial void OnIsOnChanged(bool value)
    {
        OnPropertyChanged(nameof(IconOpacity));
        _ = _bridgeService.SetLightOnAsync(LightId, value);
    }

    [RelayCommand]
    private async Task SetBrightnessAsync(double brightness)
    {
        Brightness = Math.Clamp(brightness, 0.0, 1.0);
        OnPropertyChanged(nameof(BrightnessPercent));

        await _bridgeService.SetLightBrightnessAsync(LightId, Brightness);
    }

    [RelayCommand]
    private void TapLight()
    {
        LightTapped?.Invoke(this, LightId);
    }
}

/// <summary>
/// ViewModel for a scene item in the room detail view.
/// </summary>
public partial class SceneItemViewModel : ObservableObject
{
    private readonly IHueBridgeService _bridgeService;
    private readonly SceneModel _scene;

    public Guid SceneId => _scene.Id;

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private HueColor? _previewColor;

    [ObservableProperty]
    private bool _isActive;

    /// <summary>
    /// Event raised when the scene is activated.
    /// </summary>
    public event EventHandler<Guid>? SceneActivated;

    public SceneItemViewModel(SceneModel scene, IHueBridgeService bridgeService)
    {
        _scene = scene;
        _bridgeService = bridgeService;

        _name = scene.Name;
        _previewColor = scene.PreviewColor;
    }

    [RelayCommand]
    private async Task ActivateAsync()
    {
        await _bridgeService.ActivateSceneAsync(SceneId);
        SceneActivated?.Invoke(this, SceneId);
    }
}
