using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HueWindows.Core.Models;
using HueWindows.Core.Services.Interfaces;

namespace HueWindows.Core.ViewModels;

/// <summary>
/// ViewModel for the room detail page showing lights and scenes.
/// Works for both rooms and zones.
/// </summary>
public partial class RoomDetailViewModel : ObservableObject
{
    private readonly IHueBridgeService _bridgeService;
    private readonly IAnimationService _animationService;
    private readonly ISceneStorageService _sceneStorageService;
    private readonly IRoomSceneAssignmentService _assignmentService;
    private Guid _groupId;
    private LightGroupType _groupType = LightGroupType.Room;

    [ObservableProperty]
    private string _roomName = string.Empty;

    [ObservableProperty]
    private string _groupTypeLabel = "Room";

    [ObservableProperty]
    private bool _isOn;

    [ObservableProperty]
    private double _brightness;

    [ObservableProperty]
    private ObservableCollection<LightItemViewModel> _lights = new();

    [ObservableProperty]
    private ObservableCollection<SceneItemViewModel> _scenes = new();

    [ObservableProperty]
    private ObservableCollection<AnimatedSceneModel> _animatedScenes = new();

    [ObservableProperty]
    private ObservableCollection<AnimatedSceneModel> _pinnedAnimations = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private SceneItemViewModel? _activeScene;

    [ObservableProperty]
    private bool _isAnimationRunning;

    [ObservableProperty]
    private string? _runningAnimationName;

    /// <summary>
    /// Gets all available native Hue effects.
    /// </summary>
    public IReadOnlyList<NativeEffectInfo> NativeEffects => NativeEffectInfo.All;

    /// <summary>
    /// Event raised when a light is selected for detail view.
    /// </summary>
    public event EventHandler<Guid>? LightSelected;

    public int BrightnessPercent => (int)(Brightness * 100);

    /// <summary>
    /// Gets whether any light in the room supports color.
    /// </summary>
    public bool SupportsColor => Lights.Any(l => l.SupportsColor);

    /// <summary>
    /// Gets all unique light colors in the room as RGB values for gradient display.
    /// </summary>
    public List<(byte R, byte G, byte B)> LightColors => IsOn
        ? Lights.Where(l => l.IsOn && l.CurrentColorRgb.HasValue)
                .Select(l => l.CurrentColorRgb!.Value)
                .Distinct()
                .ToList()
        : new();

    public RoomDetailViewModel(
        IHueBridgeService bridgeService,
        IAnimationService animationService,
        ISceneStorageService sceneStorageService,
        IRoomSceneAssignmentService assignmentService)
    {
        _bridgeService = bridgeService;
        _animationService = animationService;
        _sceneStorageService = sceneStorageService;
        _assignmentService = assignmentService;

        _animationService.RoomAnimationChanged += OnRoomAnimationChanged;
    }

    private void OnRoomAnimationChanged(object? sender, RoomAnimationChangedEventArgs e)
    {
        // Only update if this is our room
        if (e.RoomId != _groupId) return;

        IsAnimationRunning = e.IsRunning;
        RunningAnimationName = e.Scene?.Name;
    }

    [ObservableProperty]
    private string? _errorMessage;

    public async Task LoadRoomAsync(Guid groupId, LightGroupType groupType = LightGroupType.Room)
    {
        _groupId = groupId;
        _groupType = groupType;
        GroupTypeLabel = groupType == LightGroupType.Room ? "Room" : "Zone";
        IsLoading = true;
        ErrorMessage = null;

        // Load room or zone based on type
        var groupResult = groupType == LightGroupType.Room
            ? await _bridgeService.GetRoomAsync(groupId)
            : await _bridgeService.GetZoneAsync(groupId);

        if (groupResult.IsFailure)
        {
            ErrorMessage = groupResult.Error;
            IsLoading = false;
            return;
        }

        var group = groupResult.Value!;
        RoomName = group.Name;
        IsOn = group.IsOn;
        Brightness = group.Brightness;

        // Load lights
        Lights.Clear();
        foreach (var light in group.Lights)
        {
            var lightVm = new LightItemViewModel(light, _bridgeService);
            lightVm.LightTapped += (s, id) => LightSelected?.Invoke(this, id);
            lightVm.PropertyChanged += OnLightPropertyChanged;
            Lights.Add(lightVm);
        }

        // Load scenes (from room or zone)
        var scenesResult = groupType == LightGroupType.Room
            ? await _bridgeService.GetScenesForRoomAsync(groupId)
            : await _bridgeService.GetScenesForZoneAsync(groupId);

        Scenes.Clear();
        if (scenesResult.IsSuccess)
        {
            foreach (var scene in scenesResult.Value!)
            {
                var sceneVm = new SceneItemViewModel(scene, _bridgeService);
                sceneVm.SceneActivated += OnSceneActivated;
                Scenes.Add(sceneVm);
            }
        }

        // Load animated scenes
        AnimatedScenes.Clear();
        var animatedResult = await _animationService.GetAllScenesAsync();
        if (animatedResult.IsSuccess && animatedResult.Value != null)
        {
            foreach (var scene in animatedResult.Value.Take(6)) // Show first 6 for quick-pick
            {
                AnimatedScenes.Add(scene);
            }
        }

        // Load pinned animations for this room
        await LoadPinnedAnimationsAsync();

        // Update animation state for this room
        IsAnimationRunning = _animationService.IsAnimationRunning(groupId);
        RunningAnimationName = _animationService.GetRunningScene(groupId)?.Name;

        IsLoading = false;

        // Notify SupportsColor after lights are loaded so binding updates
        OnPropertyChanged(nameof(SupportsColor));
    }

    /// <summary>
    /// Reloads just the scenes list without reloading lights.
    /// </summary>
    private async Task LoadScenesAsync()
    {
        var scenesResult = _groupType == LightGroupType.Room
            ? await _bridgeService.GetScenesForRoomAsync(_groupId)
            : await _bridgeService.GetScenesForZoneAsync(_groupId);

        Scenes.Clear();
        if (scenesResult.IsSuccess)
        {
            foreach (var scene in scenesResult.Value!)
            {
                var sceneVm = new SceneItemViewModel(scene, _bridgeService);
                sceneVm.SceneActivated += OnSceneActivated;
                Scenes.Add(sceneVm);
            }
        }
    }

    /// <summary>
    /// Loads pinned animations for the current room.
    /// </summary>
    private async Task LoadPinnedAnimationsAsync()
    {
        var assignedIds = await _assignmentService.GetAssignedScenesAsync(_groupId);

        // Load all available scenes (both built-in and user)
        var allScenes = new List<AnimatedSceneModel>();
        var builtInResult = await _sceneStorageService.LoadBuiltInScenesAsync();
        if (builtInResult.IsSuccess && builtInResult.Value != null)
        {
            allScenes.AddRange(builtInResult.Value);
        }
        var userResult = await _sceneStorageService.LoadUserScenesAsync();
        if (userResult.IsSuccess && userResult.Value != null)
        {
            allScenes.AddRange(userResult.Value);
        }

        PinnedAnimations.Clear();
        foreach (var id in assignedIds)
        {
            var scene = allScenes.FirstOrDefault(s => s.Id == id);
            if (scene != null)
            {
                PinnedAnimations.Add(scene);
            }
        }
    }

    /// <summary>
    /// Unpins an animation from this room.
    /// </summary>
    [RelayCommand]
    private async Task UnpinAnimationAsync(AnimatedSceneModel scene)
    {
        await _assignmentService.RemoveSceneFromRoomAsync(_groupId, scene.Id);
        PinnedAnimations.Remove(scene);
    }

    partial void OnIsOnChanged(bool value)
    {
        // Update individual light states to match room state (UI only, no API calls)
        foreach (var light in Lights)
        {
            light.UpdateFromBridge(value, light.Brightness, light.CurrentColor);
        }

        OnPropertyChanged(nameof(LightColors));

        if (_groupType == LightGroupType.Room)
            _ = _bridgeService.SetRoomOnAsync(_groupId, value);
        else
            _ = _bridgeService.SetZoneOnAsync(_groupId, value);
    }

    partial void OnBrightnessChanged(double value)
    {
        // Notify BrightnessPercent when Brightness changes (e.g., from scene activation)
        OnPropertyChanged(nameof(BrightnessPercent));
    }

    [RelayCommand]
    private async Task SetBrightnessAsync(double brightness)
    {
        Brightness = Math.Clamp(brightness, 0.0, 1.0);
        OnPropertyChanged(nameof(BrightnessPercent));

        if (_groupType == LightGroupType.Room)
            await _bridgeService.SetRoomBrightnessAsync(_groupId, Brightness);
        else
            await _bridgeService.SetZoneBrightnessAsync(_groupId, Brightness);
    }

    [RelayCommand]
    private async Task SetRoomColorFromRgbAsync((byte R, byte G, byte B) rgb)
    {
        var color = HueColor.FromRgb(rgb.R, rgb.G, rgb.B);

        if (_groupType == LightGroupType.Room)
            await _bridgeService.SetRoomColorAsync(_groupId, color);
        else
            await _bridgeService.SetZoneColorAsync(_groupId, color);

        // Update light viewmodels to reflect new color
        foreach (var light in Lights.Where(l => l.SupportsColor))
        {
            light.UpdateFromBridge(light.IsOn, light.Brightness, color);
        }

        OnPropertyChanged(nameof(LightColors));
    }

    [RelayCommand]
    private async Task SaveAsSceneAsync(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return;

        var isZone = _groupType == LightGroupType.Zone;
        var result = await _bridgeService.CreateSceneFromCurrentStateAsync(_groupId, sceneName, isZone);

        if (result.IsSuccess)
        {
            // Refresh scenes list
            await LoadScenesAsync();
        }
        else
        {
            ErrorMessage = result.Error;
        }
    }

    [RelayCommand]
    private async Task DeleteSceneAsync(SceneItemViewModel scene)
    {
        var result = await _bridgeService.DeleteSceneAsync(scene.SceneId);
        if (result.IsSuccess)
        {
            Scenes.Remove(scene);
        }
        else
        {
            ErrorMessage = result.Error;
        }
    }

    private async void OnSceneActivated(object? sender, Guid sceneId)
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

        // Refresh light colors after scene activation (brief delay for bridge to update)
        await Task.Delay(500);
        await RefreshLightColorsAsync();
    }

    /// <summary>
    /// Refreshes light colors from the bridge without reloading the entire room.
    /// </summary>
    private async Task RefreshLightColorsAsync()
    {
        var groupResult = _groupType == LightGroupType.Room
            ? await _bridgeService.GetRoomAsync(_groupId)
            : await _bridgeService.GetZoneAsync(_groupId);

        if (groupResult.IsFailure) return;

        var group = groupResult.Value!;

        // Update existing light viewmodels with fresh color data
        foreach (var lightVm in Lights)
        {
            var freshLight = group.Lights.FirstOrDefault(l => l.Id == lightVm.LightId);
            if (freshLight != null)
            {
                lightVm.UpdateFromBridge(freshLight.IsOn, freshLight.Brightness, freshLight.CurrentColor);
            }
        }

        // Update room state
        IsOn = group.IsOn;
        Brightness = group.Brightness;
    }

    private void OnLightPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LightItemViewModel.IsOn) ||
            e.PropertyName == nameof(LightItemViewModel.CurrentColorRgb))
        {
            OnPropertyChanged(nameof(LightColors));
        }
    }

    [RelayCommand]
    private async Task StartAnimatedSceneAsync(AnimatedSceneModel scene)
    {
        await _animationService.StartSceneAsync(scene.Id, _groupId);
    }

    [RelayCommand]
    private async Task StopAnimationAsync()
    {
        await _animationService.StopSceneInRoomAsync(_groupId);
    }

    /// <summary>
    /// Applies a native Hue effect to a specific light.
    /// </summary>
    public async Task ApplyEffectToLightAsync(Guid lightId, string effect, double speed, double brightness)
    {
        await _bridgeService.ApplyEffectAsync(lightId, effect, speed, brightness);
    }

    /// <summary>
    /// Saves a user-created scene.
    /// </summary>
    public async Task SaveUserSceneAsync(AnimatedSceneModel scene)
    {
        var result = await _sceneStorageService.SaveSceneAsync(scene);
        if (!result.IsSuccess)
        {
            ErrorMessage = result.Error ?? "Failed to save scene";
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

    /// <summary>
    /// Gets the current light color as RGB tuple for UI binding.
    /// Returns null if light is off or has no color.
    /// </summary>
    public (byte R, byte G, byte B)? CurrentColorRgb
    {
        get
        {
            if (!IsOn || CurrentColor == null) return null;
            return CurrentColor.ToRgb(1.0);
        }
    }

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
        OnPropertyChanged(nameof(CurrentColorRgb));
        _ = _bridgeService.SetLightOnAsync(LightId, value);
    }

    partial void OnCurrentColorChanged(HueColor? value)
    {
        OnPropertyChanged(nameof(CurrentColorRgb));
    }

    /// <summary>
    /// Updates light state from bridge data without triggering API calls.
    /// </summary>
    public void UpdateFromBridge(bool isOn, double brightness, HueColor? color)
    {
        // Use SetProperty to update fields directly and notify, avoiding OnXxxChanged partial methods
        // that would trigger API calls
        if (_isOn != isOn)
        {
            _isOn = isOn;
            OnPropertyChanged(nameof(IsOn));
            OnPropertyChanged(nameof(IconOpacity));
            OnPropertyChanged(nameof(CurrentColorRgb));
        }

        if (Math.Abs(_brightness - brightness) > 0.001)
        {
            _brightness = brightness;
            OnPropertyChanged(nameof(Brightness));
            OnPropertyChanged(nameof(BrightnessPercent));
        }

        // Color can use the property setter since it doesn't trigger API calls
        CurrentColor = color;
    }

    [RelayCommand]
    private async Task SetBrightnessAsync(double brightness)
    {
        Brightness = Math.Clamp(brightness, 0.0, 1.0);
        OnPropertyChanged(nameof(BrightnessPercent));

        await _bridgeService.SetLightBrightnessAsync(LightId, Brightness);
    }

    [RelayCommand]
    private async Task SetColorFromRgbAsync((byte R, byte G, byte B) rgb)
    {
        if (!SupportsColor) return;

        var color = HueColor.FromRgb(rgb.R, rgb.G, rgb.B);
        CurrentColor = color;

        await _bridgeService.SetLightColorAsync(LightId, color);
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
    /// The palette colors as RGB tuples for UI binding.
    /// </summary>
    public List<(byte R, byte G, byte B)> PaletteColorsRgb { get; }

    /// <summary>
    /// Whether this scene has palette colors to display.
    /// </summary>
    public bool HasPaletteColors => PaletteColorsRgb.Count > 0;

    /// <summary>
    /// Number of palette colors available.
    /// </summary>
    public int ColorCount => PaletteColorsRgb.Count;

    /// <summary>
    /// First palette color hex string (or empty if none).
    /// </summary>
    public string Color1Hex => PaletteColorsRgb.Count > 0 ? ToHex(PaletteColorsRgb[0]) : string.Empty;

    /// <summary>
    /// Second palette color hex string (or empty if none).
    /// </summary>
    public string Color2Hex => PaletteColorsRgb.Count > 1 ? ToHex(PaletteColorsRgb[1]) : string.Empty;

    /// <summary>
    /// Third palette color hex string (or empty if none).
    /// </summary>
    public string Color3Hex => PaletteColorsRgb.Count > 2 ? ToHex(PaletteColorsRgb[2]) : string.Empty;

    /// <summary>
    /// Fourth palette color hex string (or empty if none).
    /// </summary>
    public string Color4Hex => PaletteColorsRgb.Count > 3 ? ToHex(PaletteColorsRgb[3]) : string.Empty;

    private static string ToHex((byte R, byte G, byte B) color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

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

        // Convert HueColors to RGB for UI binding
        PaletteColorsRgb = scene.PaletteColors
            .Select(c => c.ToRgb(1.0))
            .ToList();
    }

    [RelayCommand]
    private async Task ActivateAsync()
    {
        try
        {
            await _bridgeService.ActivateSceneAsync(SceneId);
            SceneActivated?.Invoke(this, SceneId);
        }
        catch (Exception)
        {
            // Silently fail - the bridge might be temporarily unavailable
            // UI will update when bridge connection is restored via event stream
        }
    }
}
