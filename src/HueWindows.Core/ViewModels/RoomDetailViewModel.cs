using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HueWindows.Core.Models;
using HueWindows.Core.Services.Interfaces;
using HueWindows.Core.Utilities;

namespace HueWindows.Core.ViewModels;

/// <summary>
/// ViewModel for the room detail page showing lights and scenes.
/// Works for both rooms and zones.
/// </summary>
public partial class RoomDetailViewModel : ObservableObject, IDisposable
{
    private readonly IMultiBridgeService _multiBridgeService;
    private readonly IAnimationService _animationService;
    private readonly ISceneStorageService _sceneStorageService;
    private readonly IRoomSceneAssignmentService _assignmentService;
    private readonly ISettingsService _settingsService;
    private IHueBridgeService? _bridgeService; // Set when loading room based on BridgeId
    private Guid _groupId;
    private LightGroupType _groupType = LightGroupType.Room;
    private string? _bridgeId; // Track which bridge this room belongs to
    private RoomArchetype _roomArchetype;

    [ObservableProperty]
    private string _roomName = string.Empty;

    [ObservableProperty]
    private string _roomIconGlyph = "\uE781";

    [ObservableProperty]
    private string _groupTypeLabel = "Room";

    [ObservableProperty]
    private bool _isOn;

    [ObservableProperty]
    private double _brightness;

    [ObservableProperty]
    private ObservableCollection<LightItemViewModel> _lights = new();

    [ObservableProperty]
    private ObservableCollection<UnifiedSceneItemViewModel> _unifiedScenes = new();

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
        IMultiBridgeService multiBridgeService,
        IAnimationService animationService,
        ISceneStorageService sceneStorageService,
        IRoomSceneAssignmentService assignmentService,
        ISettingsService settingsService)
    {
        _multiBridgeService = multiBridgeService;
        _animationService = animationService;
        _sceneStorageService = sceneStorageService;
        _assignmentService = assignmentService;
        _settingsService = settingsService;

        _animationService.RoomAnimationChanged += OnRoomAnimationChanged;
    }

    /// <summary>
    /// Sets the bridge ID for this room, allowing lookup of the correct bridge service.
    /// Call this before LoadRoomAsync when navigating with bridge context.
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
        RoomName = group.DisplayName;
        _roomArchetype = group.Archetype;
        RoomIconGlyph = RoomIconHelper.GetIconForRoom(_groupId, _roomArchetype, _settingsService.Settings.CustomRoomIcons);
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

        // Load unified scenes (native + assigned animated)
        await LoadUnifiedScenesAsync(groupId, groupType);

        // Update animation state for this room
        IsAnimationRunning = _animationService.IsAnimationRunning(groupId);
        RunningAnimationName = _animationService.GetRunningScene(groupId)?.Name;

        IsLoading = false;

        // Notify SupportsColor after lights are loaded so binding updates
        OnPropertyChanged(nameof(SupportsColor));
    }

    /// <summary>
    /// Loads all scenes (native + assigned animated) into the unified collection.
    /// </summary>
    private async Task LoadUnifiedScenesAsync(Guid groupId, LightGroupType groupType)
    {
        UnifiedScenes.Clear();

        if (_bridgeService == null) return;

        // 1. Load native Hue scenes
        var scenesResult = groupType == LightGroupType.Room
            ? await _bridgeService.GetScenesForRoomAsync(groupId)
            : await _bridgeService.GetScenesForZoneAsync(groupId);

        if (scenesResult.IsSuccess)
        {
            foreach (var scene in scenesResult.Value!)
            {
                var sceneVm = new SceneItemViewModel(scene, _bridgeService);
                sceneVm.SceneActivated += OnSceneActivated;
                var unified = new UnifiedSceneItemViewModel(sceneVm);
                UnifiedScenes.Add(unified);
            }
        }

        // 2. Load assigned animated scenes for this room
        var assignedIds = await _assignmentService.GetAssignedScenesAsync(groupId);

        var allAnimatedScenes = new List<AnimatedSceneModel>();
        var builtInResult = await _sceneStorageService.LoadBuiltInScenesAsync();
        if (builtInResult.IsSuccess && builtInResult.Value != null)
            allAnimatedScenes.AddRange(builtInResult.Value);
        var userResult = await _sceneStorageService.LoadUserScenesAsync();
        if (userResult.IsSuccess && userResult.Value != null)
            allAnimatedScenes.AddRange(userResult.Value);

        foreach (var id in assignedIds)
        {
            var animScene = allAnimatedScenes.FirstOrDefault(s => s.Id == id);
            if (animScene != null)
            {
                var unified = new UnifiedSceneItemViewModel(animScene);
                unified.AnimatedSceneRequested += OnAnimatedSceneRequested;
                UnifiedScenes.Add(unified);
            }
        }
    }

    private async void OnAnimatedSceneRequested(object? sender, AnimatedSceneModel scene)
    {
        await _animationService.StartSceneAsync(scene.Id, _groupId);
    }

    /// <summary>
    /// Removes an animated scene from this room.
    /// </summary>
    [RelayCommand]
    private async Task RemoveSceneFromRoomAsync(UnifiedSceneItemViewModel scene)
    {
        if (scene.AnimatedScene == null) return;
        await _assignmentService.RemoveSceneFromRoomAsync(_groupId, scene.AnimatedScene.Id);
        UnifiedScenes.Remove(scene);
    }

    partial void OnIsOnChanged(bool value)
    {
        // Update individual light states to match room state (UI only, no API calls)
        foreach (var light in Lights)
        {
            light.UpdateFromBridge(value, light.Brightness, light.CurrentColor);
        }

        OnPropertyChanged(nameof(LightColors));

        if (_bridgeService == null) return;

        if (_groupType == LightGroupType.Room)
            _bridgeService.SetRoomOnAsync(_groupId, value).FireAndForget();
        else
            _bridgeService.SetZoneOnAsync(_groupId, value).FireAndForget();
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

        if (_bridgeService == null) return;

        if (_groupType == LightGroupType.Room)
            await _bridgeService.SetRoomBrightnessAsync(_groupId, Brightness);
        else
            await _bridgeService.SetZoneBrightnessAsync(_groupId, Brightness);
    }

    [RelayCommand]
    private async Task SetRoomColorFromRgbAsync((byte R, byte G, byte B) rgb)
    {
        var color = HueColor.FromRgb(rgb.R, rgb.G, rgb.B);

        if (_bridgeService != null)
        {
            if (_groupType == LightGroupType.Room)
                await _bridgeService.SetRoomColorAsync(_groupId, color);
            else
                await _bridgeService.SetZoneColorAsync(_groupId, color);
        }

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
        if (string.IsNullOrWhiteSpace(sceneName) || _bridgeService == null)
            return;

        var isZone = _groupType == LightGroupType.Zone;
        var result = await _bridgeService.CreateSceneFromCurrentStateAsync(_groupId, sceneName, isZone);

        if (result.IsSuccess)
        {
            // Refresh unified scenes list
            await LoadUnifiedScenesAsync(_groupId, _groupType);
        }
        else
        {
            ErrorMessage = result.Error;
        }
    }

    [RelayCommand]
    private async Task DeleteSceneAsync(UnifiedSceneItemViewModel scene)
    {
        if (_bridgeService == null || !scene.IsNative || scene.NativeSceneId == null) return;

        var result = await _bridgeService.DeleteSceneAsync(scene.NativeSceneId.Value);
        if (result.IsSuccess)
        {
            UnifiedScenes.Remove(scene);
        }
        else
        {
            ErrorMessage = result.Error;
        }
    }

    private async void OnSceneActivated(object? sender, Guid sceneId)
    {
        try
        {
            // Update active scene visual state across all unified scenes
            foreach (var unified in UnifiedScenes)
            {
                if (unified.IsNative)
                {
                    unified.IsActive = unified.NativeSceneId == sceneId;
                }
            }

            if (sender is SceneItemViewModel activeScene)
            {
                ActiveScene = activeScene;
            }

            // Refresh light colors after scene activation (brief delay for bridge to update)
            await Task.Delay(500);
            await RefreshLightColorsAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RoomDetailViewModel] OnSceneActivated failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Refreshes light colors from the bridge without reloading the entire room.
    /// </summary>
    private async Task RefreshLightColorsAsync()
    {
        if (_bridgeService == null) return;

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
    /// Event raised when the custom icon is changed so other views can update.
    /// </summary>
    public event EventHandler<string>? CustomIconChanged;

    /// <summary>
    /// Gets the current group ID.
    /// </summary>
    public Guid GroupId => _groupId;

    /// <summary>
    /// Gets the current group type (Room or Zone).
    /// </summary>
    public LightGroupType GroupType => _groupType;

    [RelayCommand]
    private async Task SetCustomIconAsync(string glyph)
    {
        _settingsService.Settings.CustomRoomIcons[_groupId.ToString()] = glyph;
        await _settingsService.SaveAsync();
        RoomIconGlyph = glyph;
        CustomIconChanged?.Invoke(this, glyph);
    }

    [RelayCommand]
    private async Task ResetIconAsync()
    {
        _settingsService.Settings.CustomRoomIcons.Remove(_groupId.ToString());
        await _settingsService.SaveAsync();
        var defaultGlyph = RoomIconHelper.GetIconForArchetype(_roomArchetype);
        RoomIconGlyph = defaultGlyph;
        CustomIconChanged?.Invoke(this, defaultGlyph);
    }

    /// <summary>
    /// Applies a native Hue effect to a specific light.
    /// </summary>
    public async Task ApplyEffectToLightAsync(Guid lightId, string effect, double speed, double brightness)
    {
        if (_bridgeService == null) return;
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

    public void Dispose()
    {
        _animationService.RoomAnimationChanged -= OnRoomAnimationChanged;
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
        _bridgeService.SetLightOnAsync(LightId, value).FireAndForget();
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
        // Capture sync context to ensure event fires on UI thread
        var syncContext = SynchronizationContext.Current;
        try
        {
            await _bridgeService.ActivateSceneAsync(SceneId);

            // Fire event on original (UI) thread to avoid cross-thread XAML updates
            if (syncContext != null)
            {
                syncContext.Post(_ => SceneActivated?.Invoke(this, SceneId), null);
            }
            else
            {
                SceneActivated?.Invoke(this, SceneId);
            }
        }
        catch (Exception)
        {
            // Silently fail - the bridge might be temporarily unavailable
            // UI will update when bridge connection is restored via event stream
        }
    }
}
