using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HueWindows.Core.Models;
using HueWindows.Core.Services;
using HueWindows.Core.Services.Interfaces;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HueWindows.Core.ViewModels;

/// <summary>
/// ViewModel for the Scene Builder page.
/// </summary>
public partial class SceneBuilderViewModel : ObservableObject
{
    private readonly IMultiBridgeService _multiBridgeService;
    private readonly IAnimationService _animationService;
    private readonly ISceneStorageService _storageService;
    private IHueBridgeService? _bridgeService; // Set based on selected room

    private string? _loadedSceneId;
    private bool _isLoadingScene; // Prevents default track creation during scene load
    private AnimatedSceneModel? _loadedSceneModel; // Stored to re-apply when room changes

    [ObservableProperty]
    private string _sceneName = "Untitled Scene";

    [ObservableProperty]
    private string _sceneDescription = "";

    [ObservableProperty]
    private double _durationSeconds = 16;

    [ObservableProperty]
    private ObservableCollection<RoomModel> _rooms = new();

    [ObservableProperty]
    private RoomModel? _selectedRoom;

    [ObservableProperty]
    private ObservableCollection<TrackViewModel> _tracks = new();

    [ObservableProperty]
    private ObservableCollection<EventTrackViewModel> _eventTracks = new();

    [ObservableProperty]
    private KeyframeViewModel? _selectedKeyframe;

    [ObservableProperty]
    private EventTrackViewModel? _selectedEventTrack;

    [ObservableProperty]
    private double _playheadPosition;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private bool _isLooping = true;

    [ObservableProperty]
    private double _zoomLevel = 50; // Pixels per second

    [ObservableProperty]
    private bool _isSnapEnabled = true; // Snap to grid on by default

    [ObservableProperty]
    private bool _hasUnsavedChanges;

    /// <summary>
    /// Undo/redo command history.
    /// </summary>
    public TimelineCommandHistory CommandHistory { get; } = new();

    /// <summary>
    /// Currently selected keyframes (for multi-select).
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<KeyframeViewModel> _selectedKeyframes = new();

    /// <summary>
    /// Clipboard for copy/paste operations. Stores track index to preserve track association.
    /// </summary>
    private List<(int TrackIndex, double TimeOffset, HueColor Color, double Brightness, TransitionStyle Transition)>? _clipboardKeyframes;

    /// <summary>
    /// Whether we're editing an existing scene (vs creating new).
    /// </summary>
    public bool IsEditingExistingScene => _loadedSceneId != null;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private DateTime _lastPlayheadUpdate = DateTime.MinValue;
    private const int PlayheadUpdateThrottleMs = 100; // Minimum time between updates

    /// <summary>
    /// Maximum number of colors to include in the scene palette preview.
    /// </summary>
    private const int MaxPaletteColors = 4;

    /// <summary>
    /// Gets the current snap interval based on zoom level.
    /// </summary>
    public double SnapInterval => ZoomLevel switch
    {
        < 40 => 2.0,    // Zoomed out - 2 second grid
        < 80 => 1.0,    // Medium zoom - 1 second grid
        < 150 => 0.5,   // Zoomed in - 0.5 second grid
        _ => 0.25       // Fully zoomed - 0.25 second grid
    };

    /// <summary>
    /// Snaps a time value to the nearest grid line if snap is enabled.
    /// </summary>
    public double SnapToGrid(double timeSeconds)
    {
        if (!IsSnapEnabled) return timeSeconds;
        var interval = SnapInterval;
        return Math.Round(timeSeconds / interval) * interval;
    }

    public SceneBuilderViewModel(
        IMultiBridgeService multiBridgeService,
        IAnimationService animationService,
        ISceneStorageService storageService)
    {
        _multiBridgeService = multiBridgeService ?? throw new ArgumentNullException(nameof(multiBridgeService));
        _animationService = animationService ?? throw new ArgumentNullException(nameof(animationService));
        _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
    }

    public async Task InitializeAsync()
    {
        await LoadRoomsAsync();
    }

    partial void OnSceneNameChanged(string value) => MarkDirty();
    partial void OnSceneDescriptionChanged(string value) => MarkDirty();

    private async Task LoadRoomsAsync()
    {
        // Load rooms and zones from all bridges
        var roomsResult = await _multiBridgeService.GetAllRoomsAsync();
        if (roomsResult.IsSuccess && roomsResult.Value != null)
        {
            Rooms = new ObservableCollection<RoomModel>(roomsResult.Value);
        }

        var zonesResult = await _multiBridgeService.GetAllZonesAsync();
        if (zonesResult.IsSuccess && zonesResult.Value != null)
        {
            foreach (var zone in zonesResult.Value)
            {
                Rooms.Add(zone);
            }
        }
    }

    /// <summary>
    /// Updates the bridge service based on the selected room's bridge ID.
    /// </summary>
    private void UpdateBridgeService()
    {
        if (SelectedRoom?.BridgeId != null)
        {
            _bridgeService = _multiBridgeService.GetBridgeService(SelectedRoom.BridgeId);
        }
        else
        {
            _bridgeService = _multiBridgeService.GetDefaultBridgeService();
        }
    }

    partial void OnSelectedRoomChanged(RoomModel? value)
    {
        // Update bridge service for the selected room
        UpdateBridgeService();

        // Don't create default tracks when loading a scene - LoadSceneFromModelAsync handles it
        if (_isLoadingScene)
            return;

        if (value != null)
        {
            // If we have a loaded scene, re-apply its tracks for the new room
            if (_loadedSceneModel != null)
            {
                ApplyLoadedSceneToRoom(value);
            }
            else
            {
                CreateTracksForRoom(value);
            }
        }
        else
        {
            Tracks.Clear();
        }
    }

    /// <summary>
    /// Applies the loaded scene's animations to a room's lights.
    /// </summary>
    private void ApplyLoadedSceneToRoom(RoomModel room)
    {
        Tracks.Clear();

        foreach (var animation in _loadedSceneModel!.Animations)
        {
            if (animation.Type == AnimationType.Keyframe)
            {
                if (animation.LightAssignment == LightAssignment.All)
                {
                    // Expand to one track per light
                    foreach (var light in room.Lights)
                    {
                        var track = new TrackViewModel
                        {
                            LightId = light.Id.ToString(),
                            DisplayName = light.Name,
                            Keyframes = new ObservableCollection<KeyframeViewModel>(
                                animation.Keyframes.Select(k => new KeyframeViewModel
                                {
                                    TimeSeconds = k.TimeSeconds,
                                    Color = k.Color ?? HueColors.WarmWhite,
                                    Brightness = k.Brightness ?? 1.0,
                                    Transition = k.TransitionStyle
                                })
                            )
                        };
                        Tracks.Add(track);
                    }
                }
                else
                {
                    // Load as single keyframe track (subset or specific light)
                    var lightIndex = animation.TargetLightIndices.Count > 0 ? animation.TargetLightIndices[0] : 0;
                    var light = lightIndex < room.Lights.Count ? room.Lights[lightIndex] : null;

                    var track = new TrackViewModel
                    {
                        LightId = light?.Id.ToString() ?? Guid.NewGuid().ToString(),
                        DisplayName = light?.Name ?? $"Track {lightIndex}",
                        Keyframes = new ObservableCollection<KeyframeViewModel>(
                            animation.Keyframes.Select(k => new KeyframeViewModel
                            {
                                TimeSeconds = k.TimeSeconds,
                                Color = k.Color ?? HueColors.WarmWhite,
                                Brightness = k.Brightness ?? 1.0,
                                Transition = k.TransitionStyle
                            })
                        )
                    };

                    Tracks.Add(track);
                }
            }
        }
    }

    private void CreateTracksForRoom(RoomModel room)
    {
        Tracks.Clear();

        foreach (var light in room.Lights)
        {
            // Use light's current color and brightness, or default to neutral white
            var initialColor = light.CurrentColor ?? HueColor.White;
            var initialBrightness = light.Brightness;

            var track = new TrackViewModel
            {
                LightId = light.Id.ToString(),
                DisplayName = light.Name,
                Keyframes = new ObservableCollection<KeyframeViewModel>
                {
                    // Default: two keyframes at start and end with light's CURRENT color
                    new KeyframeViewModel
                    {
                        TimeSeconds = 0,
                        Color = initialColor,
                        Brightness = initialBrightness,
                        Transition = TransitionStyle.EaseInOut
                    },
                    new KeyframeViewModel
                    {
                        TimeSeconds = DurationSeconds,
                        Color = initialColor,
                        Brightness = initialBrightness,
                        Transition = TransitionStyle.EaseInOut
                    }
                }
            };

            Tracks.Add(track);
        }
    }

    partial void OnDurationSecondsChanged(double value)
    {
        // Update the last keyframe of each track to match new duration
        foreach (var track in Tracks)
        {
            if (track.Keyframes.Count > 0)
            {
                var lastKeyframe = track.Keyframes[^1];
                if (lastKeyframe.TimeSeconds > value)
                {
                    lastKeyframe.TimeSeconds = value;
                }
            }
        }
        MarkDirty();
    }

    [RelayCommand]
    private void SelectKeyframe(KeyframeViewModel keyframe)
    {
        SelectedKeyframe = keyframe;
        SelectedEventTrack = null; // Clear event track selection
    }

    [RelayCommand]
    private void SelectEventTrack(EventTrackViewModel eventTrack)
    {
        SelectedEventTrack = eventTrack;
        SelectedKeyframe = null; // Clear keyframe selection
    }

    [RelayCommand]
    private void AddEventTrack(EventPreset preset)
    {
        var eventTrack = new EventTrackViewModel
        {
            Preset = preset,
            Frequency = 0.5
        };
        EventTracks.Add(eventTrack);
        SelectEventTrack(eventTrack);
        MarkDirty();
    }

    [RelayCommand]
    private void DeleteEventTrack(EventTrackViewModel eventTrack)
    {
        EventTracks.Remove(eventTrack);
        if (SelectedEventTrack == eventTrack)
        {
            SelectedEventTrack = null;
        }
        MarkDirty();
    }

    public void AddKeyframe(TrackViewModel track, double timeSeconds)
    {
        // Find the surrounding keyframes to interpolate color
        var prevKeyframe = track.Keyframes
            .Where(k => k.TimeSeconds <= timeSeconds)
            .OrderByDescending(k => k.TimeSeconds)
            .FirstOrDefault();

        var nextKeyframe = track.Keyframes
            .Where(k => k.TimeSeconds > timeSeconds)
            .OrderBy(k => k.TimeSeconds)
            .FirstOrDefault();

        // Interpolate color and brightness
        var newColor = prevKeyframe?.Color ?? HueColors.WarmWhite;
        var newBrightness = prevKeyframe?.Brightness ?? 1.0;

        if (prevKeyframe != null && nextKeyframe != null)
        {
            var t = (timeSeconds - prevKeyframe.TimeSeconds) /
                    (nextKeyframe.TimeSeconds - prevKeyframe.TimeSeconds);

            // HSV interpolation to avoid muddy colors
            newColor = ColorConverter.InterpolateHsv(
                prevKeyframe.Color, 
                nextKeyframe.Color, 
                t, 
                prevKeyframe.Brightness, 
                nextKeyframe.Brightness);
            newBrightness = prevKeyframe.Brightness + (nextKeyframe.Brightness - prevKeyframe.Brightness) * t;
        }

        var newKeyframe = new KeyframeViewModel
        {
            TimeSeconds = timeSeconds,
            Color = newColor,
            Brightness = newBrightness,
            Transition = TransitionStyle.EaseInOut
        };

        // Insert in sorted order
        var index = track.Keyframes.TakeWhile(k => k.TimeSeconds < timeSeconds).Count();
        track.Keyframes.Insert(index, newKeyframe);

        SelectedKeyframe = newKeyframe;
        MarkDirty();
    }

    [RelayCommand]
    private void DeleteKeyframe(KeyframeViewModel keyframe)
    {
        foreach (var track in Tracks)
        {
            if (track.Keyframes.Contains(keyframe))
            {
                // Don't delete first or last keyframes
                var isFirstKeyframe = track.Keyframes.OrderBy(k => k.TimeSeconds).First() == keyframe;
                var isLastKeyframe = track.Keyframes.OrderByDescending(k => k.TimeSeconds).First() == keyframe;

                if (!isFirstKeyframe && !isLastKeyframe)
                {
                    track.Keyframes.Remove(keyframe);
                    if (SelectedKeyframe == keyframe)
                    {
                        SelectedKeyframe = null;
                    }
                    MarkDirty();
                }
                break;
            }
        }
    }

    [RelayCommand]
    private void DuplicateKeyframe(KeyframeViewModel keyframe)
    {
        foreach (var track in Tracks)
        {
            var index = track.Keyframes.IndexOf(keyframe);
            if (index >= 0)
            {
                // Duplicate slightly to the right
                var newTime = keyframe.TimeSeconds + 1.0;
                if (newTime > DurationSeconds)
                {
                    newTime = keyframe.TimeSeconds - 1.0;
                }
                if (newTime < 0) newTime = 0;

                var duplicate = new KeyframeViewModel
                {
                    TimeSeconds = newTime,
                    Color = new HueColor(keyframe.Color.X, keyframe.Color.Y),
                    Brightness = keyframe.Brightness,
                    Transition = keyframe.Transition
                };

                // Insert in sorted order
                var insertIndex = track.Keyframes.TakeWhile(k => k.TimeSeconds < newTime).Count();
                track.Keyframes.Insert(insertIndex, duplicate);

                SelectedKeyframe = duplicate;
                MarkDirty();
                break;
            }
        }
    }

    /// <summary>
    /// Updates lights to reflect the current playhead position (live preview).
    /// Throttled to avoid spamming the bridge during scrubbing.
    /// </summary>
    public async Task UpdateLightsForPlayheadAsync()
    {
        if (Tracks.Count == 0)
            return;

        // Ensure we have a bridge service
        var bridgeService = _bridgeService ?? _multiBridgeService.GetDefaultBridgeService();
        if (bridgeService == null)
            return;

        // Note: Throttling is handled by the caller (SceneBuilderPage)
        // to avoid double-throttling which can cause missed updates

        foreach (var track in Tracks)
        {
            if (!Guid.TryParse(track.LightId, out var lightId))
                continue;

            var (color, brightness) = InterpolateAtTime(track, PlayheadPosition);

            // Send to light
            try
            {
                await bridgeService.SetLightColorAsync(lightId, color);
                await bridgeService.SetLightBrightnessAsync(lightId, brightness);
            }
            catch
            {
                // Continue with other lights if one fails
            }
        }
    }

    /// <summary>
    /// Updates a single light to match a keyframe (for editing preview).
    /// </summary>
    public async Task UpdateLightForKeyframeAsync(KeyframeViewModel keyframe)
    {
        // Ensure we have a bridge service
        var bridgeService = _bridgeService ?? _multiBridgeService.GetDefaultBridgeService();
        if (bridgeService == null) return;

        // Find which track this keyframe belongs to
        foreach (var track in Tracks)
        {
            if (track.Keyframes.Contains(keyframe))
            {
                if (Guid.TryParse(track.LightId, out var lightId))
                {
                    await bridgeService.SetLightColorAsync(lightId, keyframe.Color);
                    await bridgeService.SetLightBrightnessAsync(lightId, keyframe.Brightness);
                }
                break;
            }
        }
    }

    /// <summary>
    /// Fires an event effect on a specific light track.
    /// </summary>
    /// <param name="eventTrack">The event track to fire.</param>
    /// <param name="trackIndex">The index of the light track to affect.</param>
    public async Task FireEventAsync(EventTrackViewModel eventTrack, int trackIndex)
    {
        if (Tracks.Count == 0 || trackIndex < 0 || trackIndex >= Tracks.Count)
            return;

        // Ensure we have a bridge service
        var bridgeService = _bridgeService ?? _multiBridgeService.GetDefaultBridgeService();
        if (bridgeService == null)
            return;

        var track = Tracks[trackIndex];

        if (!Guid.TryParse(track.LightId, out var lightId))
            return;

        // Get event parameters based on preset
        var (flashColor, flashBrightness, returnBrightness) = eventTrack.Preset switch
        {
            EventPreset.LightningFlash => (new HueColor(0.31, 0.32), 1.0, 0.3),   // Cool white flash
            EventPreset.Sparkle => (new HueColor(0.33, 0.34), 1.0, 0.5),          // Bright white sparkle
            EventPreset.CandleFlicker => (new HueColor(0.57, 0.41), 0.6, 0.8),    // Warm orange dip
            _ => (new HueColor(0.31, 0.32), 1.0, 0.5)
        };

        // Flash the light
        await bridgeService.SetLightColorAsync(lightId, flashColor);
        await bridgeService.SetLightBrightnessAsync(lightId, flashBrightness);

        // Brief delay then return to interpolated state
        await Task.Delay(100);

        // Return to the current playhead state for this track
        var (currentColor, currentBrightness) = InterpolateAtTime(track, PlayheadPosition);
        await bridgeService.SetLightColorAsync(lightId, currentColor);
        await bridgeService.SetLightBrightnessAsync(lightId, currentBrightness);
    }

    private (HueColor color, double brightness) InterpolateAtTime(TrackViewModel track, double timeSeconds)
    {
        if (track.Keyframes.Count == 0)
            return (HueColors.WarmWhite, 1.0);

        // Find surrounding keyframes
        var sortedKeyframes = track.Keyframes.OrderBy(k => k.TimeSeconds).ToList();

        KeyframeViewModel? prev = null;
        KeyframeViewModel? next = null;

        foreach (var kf in sortedKeyframes)
        {
            if (kf.TimeSeconds <= timeSeconds)
                prev = kf;
            else if (next == null)
                next = kf;
        }

        // If no previous keyframe, use first
        if (prev == null)
            prev = sortedKeyframes.First();

        // If no next keyframe, use previous (hold)
        if (next == null)
            return (prev.Color, prev.Brightness);

        // Interpolate between prev and next
        var duration = next.TimeSeconds - prev.TimeSeconds;
        if (duration <= 0)
            return (prev.Color, prev.Brightness);

        var t = (timeSeconds - prev.TimeSeconds) / duration;

        // Apply easing based on transition style
        t = Easing.Apply(t, next.Transition);

        // Interpolate color using HSV to avoid muddy colors
        var color = ColorConverter.InterpolateHsv(prev.Color, next.Color, t, prev.Brightness, next.Brightness);
        var brightness = prev.Brightness + (next.Brightness - prev.Brightness) * t;

        return (color, brightness);
    }

    /// <summary>
    /// Saves the current scene to the user scenes folder.
    /// </summary>
    [RelayCommand]
    public async Task<Result> SaveSceneAsync()
    {
        if (SelectedRoom == null || Tracks.Count == 0)
        {
            return Result.Failure("No room or tracks to save");
        }

        var scene = BuildSceneModel();

        // Save using storage service
        var result = await _storageService.SaveSceneAsync(scene);

        if (result.IsSuccess)
        {
            _loadedSceneId = scene.Id;
            HasUnsavedChanges = false;
            OnPropertyChanged(nameof(IsEditingExistingScene));
        }

        return result;
    }

    /// <summary>
    /// Loads an existing scene for editing.
    /// </summary>
    public async Task<Result> LoadSceneAsync(string sceneId)
    {
        var sceneResult = await _animationService.GetSceneAsync(sceneId);
        if (sceneResult.IsFailure || sceneResult.Value == null)
        {
            return Result.Failure(sceneResult.Error ?? "Scene not found");
        }

        var scene = sceneResult.Value;

        // Don't allow editing built-in scenes
        if (scene.IsBuiltIn)
        {
            return Result.Failure("Cannot edit built-in scenes");
        }

        await LoadSceneFromModelAsync(scene);
        HasUnsavedChanges = false; // Just loaded, no changes yet
        return Result.Success();
    }

    /// <summary>
    /// Extracts palette colors from keyframes for preview display.
    /// </summary>
    private List<HueColor> ExtractPaletteColors()
    {
        var colors = new HashSet<(double, double)>();

        foreach (var track in Tracks)
        {
            foreach (var keyframe in track.Keyframes)
            {
                // Round to reduce near-duplicates
                var rounded = (Math.Round(keyframe.Color.X, 2), Math.Round(keyframe.Color.Y, 2));
                colors.Add(rounded);
            }
        }

        return colors
            .Take(MaxPaletteColors)
            .Select(c => new HueColor(c.Item1, c.Item2))
            .ToList();
    }

    /// <summary>
    /// Exports the current scene to JSON string.
    /// </summary>
    public Result<string> ExportToJson()
    {
        if (SelectedRoom == null || Tracks.Count == 0)
        {
            return Result<string>.Failure("No room or tracks to export");
        }

        try
        {
            var scene = BuildSceneModel();
            var json = JsonSerializer.Serialize(scene, _jsonOptions);
            return Result<string>.Success(json);
        }
        catch (Exception ex)
        {
            return Result<string>.Failure($"Failed to export scene: {ex.Message}");
        }
    }

    /// <summary>
    /// Imports a scene from JSON string.
    /// </summary>
    public async Task<Result> ImportFromJsonAsync(string json)
    {
        try
        {
            var scene = JsonSerializer.Deserialize<AnimatedSceneModel>(json, _jsonOptions);
            if (scene == null)
            {
                return Result.Failure("Failed to parse scene JSON");
            }

            // Validate the scene
            var validationResult = _storageService.ValidateScene(scene);
            if (validationResult.IsFailure)
            {
                return Result.Failure($"Invalid scene: {validationResult.Error}");
            }

            // Generate new ID for imported scene (treat as new)
            scene.Id = $"user_{Guid.NewGuid():N}";
            scene.IsBuiltIn = false;

            // Load into editor
            await LoadSceneFromModelAsync(scene);

            HasUnsavedChanges = true; // Imported scenes need to be saved
            return Result.Success();
        }
        catch (JsonException ex)
        {
            return Result.Failure($"Invalid JSON: {ex.Message}");
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to import scene: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets all user-created scenes for the scene picker.
    /// </summary>
    public async Task<Result<IReadOnlyList<AnimatedSceneModel>>> GetUserScenesAsync()
    {
        return await _storageService.LoadUserScenesAsync();
    }

    /// <summary>
    /// Resets the editor to a new blank scene.
    /// </summary>
    public void NewScene()
    {
        _loadedSceneId = null;
        _loadedSceneModel = null; // Clear stored scene so room changes create default tracks
        SceneName = "Untitled Scene";
        SceneDescription = "";
        DurationSeconds = 16;
        SelectedRoom = null;
        Tracks.Clear();
        EventTracks.Clear();
        SelectedKeyframe = null;
        SelectedEventTrack = null;
        PlayheadPosition = 0;
        IsPlaying = false;
        HasUnsavedChanges = false;
        OnPropertyChanged(nameof(IsEditingExistingScene));
    }

    /// <summary>
    /// Marks the scene as having unsaved changes.
    /// </summary>
    public void MarkDirty()
    {
        HasUnsavedChanges = true;
    }

    /// <summary>
    /// Builds an AnimatedSceneModel from the current editor state.
    /// </summary>
    private AnimatedSceneModel BuildSceneModel()
    {
        var sceneId = _loadedSceneId ?? $"user_{Guid.NewGuid():N}";

        var scene = new AnimatedSceneModel
        {
            Id = sceneId,
            Name = SceneName,
            Description = SceneDescription,
            Category = "Custom",
            DefaultTargeting = LightTargeting.Room,
            TargetId = SelectedRoom?.Id,
            IsBuiltIn = false,
            Version = "1.0",
            PaletteColors = ExtractPaletteColors(),
            Animations = new List<AnimationDefinition>()
        };

        // Convert each track to an AnimationDefinition
        for (int i = 0; i < Tracks.Count; i++)
        {
            var track = Tracks[i];
            var animation = new AnimationDefinition
            {
                Id = $"track_{i}",
                Name = track.DisplayName,
                Type = AnimationType.Keyframe,
                LightAssignment = LightAssignment.Subset,
                TargetLightIndices = new List<int> { i },
                DurationSeconds = DurationSeconds,
                RepeatMode = IsLooping ? RepeatMode.Loop : RepeatMode.Once,
                Keyframes = track.Keyframes
                    .OrderBy(k => k.TimeSeconds)
                    .Select(k => new AnimationKeyframe
                    {
                        TimeSeconds = k.TimeSeconds,
                        Color = k.Color,
                        Brightness = k.Brightness,
                        TransitionStyle = k.Transition
                    })
                    .ToList()
            };

            scene.Animations.Add(animation);
        }

        // Convert each event track to an AnimationDefinition
        foreach (var eventTrack in EventTracks)
        {
            scene.Animations.Add(eventTrack.ToAnimationDefinition());
        }

        return scene;
    }

    /// <summary>
    /// Loads an AnimatedSceneModel into the editor.
    /// </summary>
    private async Task LoadSceneFromModelAsync(AnimatedSceneModel scene)
    {
        _isLoadingScene = true;
        try
        {
            _loadedSceneId = scene.Id;
            _loadedSceneModel = scene; // Store for re-applying when room changes
            SceneName = scene.Name;
            SceneDescription = scene.Description;

            // Find the target room
            if (scene.TargetId.HasValue)
            {
                SelectedRoom = Rooms.FirstOrDefault(r => r.Id == scene.TargetId.Value);
            }

            // If room not found, leave it unselected (user can pick one)
            if (SelectedRoom == null && Rooms.Count > 0)
            {
                // Don't auto-select, let user choose
            }

            // Get duration from first keyframe animation
            var firstKeyframeAnim = scene.Animations.FirstOrDefault(a => a.Type == AnimationType.Keyframe);
            if (firstKeyframeAnim != null)
            {
                DurationSeconds = firstKeyframeAnim.DurationSeconds > 0 ? firstKeyframeAnim.DurationSeconds : 16;
                IsLooping = firstKeyframeAnim.RepeatMode == RepeatMode.Loop;
            }

            // Load tracks from animations
            Tracks.Clear();
            EventTracks.Clear();

            foreach (var animation in scene.Animations)
            {
                if (animation.Type == AnimationType.Event || animation.Type == AnimationType.NativeEffect)
                {
                    // Load as event track
                    var eventTrack = new EventTrackViewModel
                    {
                        Id = animation.Id,
                        DisplayName = animation.Name ?? "Event"
                    };

                    // Try to determine preset from EventPreset property, native effect, or trigger inference
                    if (!string.IsNullOrEmpty(animation.EventPreset) &&
                        Enum.TryParse<EventPreset>(animation.EventPreset, out var preset))
                    {
                        // Explicit preset stored in animation
                        eventTrack.Preset = preset;
                    }
                    else if (animation.Type == AnimationType.NativeEffect)
                    {
                        // Map native effect names to presets
                        eventTrack.Preset = animation.Name?.ToLowerInvariant() switch
                        {
                            "fire" or "candle" => EventPreset.CandleFlicker,
                            "sparkle" => EventPreset.Sparkle,
                            _ => EventPreset.Sparkle
                        };
                        eventTrack.DisplayName = animation.Name ?? "Effect";
                    }
                    else if (animation.EventPattern?.Triggers.Count > 0)
                    {
                        // Fallback: infer preset from trigger states for legacy scenes
                        var trigger = animation.EventPattern.Triggers[0];
                        if (trigger.States.Count > 0)
                        {
                            var firstState = trigger.States[0];
                            // Infer preset from color/brightness patterns
                            if (firstState.Color?.X < 0.35)
                                eventTrack.Preset = EventPreset.LightningFlash;
                            else if (firstState.Color?.X > 0.5)
                                eventTrack.Preset = EventPreset.CandleFlicker;
                            else
                                eventTrack.Preset = EventPreset.Sparkle;
                        }
                    }

                    // Infer frequency from intervals
                    if (animation.EventPattern != null)
                    {
                        var avgInterval = (animation.EventPattern.MinIntervalSeconds + animation.EventPattern.MaxIntervalSeconds) / 2;
                        eventTrack.Frequency = avgInterval switch
                        {
                            > 10 => 0.0,
                            > 4 => 0.5,
                            _ => 1.0
                        };
                    }

                    EventTracks.Add(eventTrack);
                }
                else if (animation.Type == AnimationType.Keyframe)
                {
                    // Check if this animation targets all lights
                    if (animation.LightAssignment == LightAssignment.All && SelectedRoom != null)
                    {
                        // Expand to one track per light in the room
                        foreach (var light in SelectedRoom.Lights)
                        {
                            var track = new TrackViewModel
                            {
                                LightId = light.Id.ToString(),
                                DisplayName = light.Name,
                                Keyframes = new ObservableCollection<KeyframeViewModel>(
                                    animation.Keyframes.Select(k => new KeyframeViewModel
                                    {
                                        TimeSeconds = k.TimeSeconds,
                                        Color = k.Color ?? HueColors.WarmWhite,
                                        Brightness = k.Brightness ?? 1.0,
                                        Transition = k.TransitionStyle
                                    })
                                )
                            };
                            Tracks.Add(track);
                        }
                    }
                    else
                    {
                        // Load as single keyframe track (subset or specific light)
                        var lightIndex = animation.TargetLightIndices.Count > 0 ? animation.TargetLightIndices[0] : 0;
                        var light = SelectedRoom != null && lightIndex < SelectedRoom.Lights.Count
                            ? SelectedRoom.Lights[lightIndex]
                            : null;

                        var track = new TrackViewModel
                        {
                            LightId = light?.Id.ToString() ?? Guid.NewGuid().ToString(),
                            DisplayName = light?.Name ?? $"Track {lightIndex}",
                            Keyframes = new ObservableCollection<KeyframeViewModel>(
                                animation.Keyframes.Select(k => new KeyframeViewModel
                                {
                                    TimeSeconds = k.TimeSeconds,
                                    Color = k.Color ?? HueColors.WarmWhite,
                                    Brightness = k.Brightness ?? 1.0,
                                    Transition = k.TransitionStyle
                                })
                            )
                        };

                        Tracks.Add(track);
                    }
                }
            }

            OnPropertyChanged(nameof(IsEditingExistingScene));
        }
        finally
        {
            _isLoadingScene = false;
        }
    }

    #region Undo/Redo Operations

    /// <summary>
    /// Undo the last timeline operation.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUndo))]
    public void Undo()
    {
        CommandHistory.Undo();
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    /// <summary>
    /// Redo the last undone operation.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRedo))]
    public void Redo()
    {
        CommandHistory.Redo();
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    public bool CanUndo => CommandHistory.CanUndo;
    public bool CanRedo => CommandHistory.CanRedo;

    /// <summary>
    /// Execute a command with undo support.
    /// </summary>
    public void ExecuteCommand(ITimelineCommand command)
    {
        CommandHistory.Execute(command);
        MarkDirty();
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    #endregion

    #region Multi-Select Operations

    /// <summary>
    /// Toggle selection of a keyframe (for multi-select).
    /// </summary>
    public void ToggleKeyframeSelection(KeyframeViewModel keyframe, bool isShiftClick)
    {
        if (isShiftClick)
        {
            // Add to selection
            if (SelectedKeyframes.Contains(keyframe))
            {
                SelectedKeyframes.Remove(keyframe);
            }
            else
            {
                SelectedKeyframes.Add(keyframe);
            }
        }
        else
        {
            // Replace selection
            SelectedKeyframes.Clear();
            SelectedKeyframes.Add(keyframe);
            SelectedKeyframe = keyframe;
        }
    }

    /// <summary>
    /// Select keyframes within a rectangular area.
    /// </summary>
    public void SelectKeyframesInRect(double startTime, double endTime, int startTrackIndex, int endTrackIndex)
    {
        SelectedKeyframes.Clear();

        var minTime = Math.Min(startTime, endTime);
        var maxTime = Math.Max(startTime, endTime);
        var minTrack = Math.Min(startTrackIndex, endTrackIndex);
        var maxTrack = Math.Max(startTrackIndex, endTrackIndex);

        for (int trackIndex = minTrack; trackIndex <= maxTrack && trackIndex < Tracks.Count; trackIndex++)
        {
            var track = Tracks[trackIndex];
            foreach (var keyframe in track.Keyframes)
            {
                if (keyframe.TimeSeconds >= minTime && keyframe.TimeSeconds <= maxTime)
                {
                    SelectedKeyframes.Add(keyframe);
                }
            }
        }
    }

    /// <summary>
    /// Clear all keyframe selections.
    /// </summary>
    public void ClearSelection()
    {
        SelectedKeyframes.Clear();
        SelectedKeyframe = null;
    }

    #endregion

    #region Copy/Paste Operations

    /// <summary>
    /// Copy selected keyframes to clipboard.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasSelectedKeyframes))]
    public void CopyKeyframes()
    {
        if (SelectedKeyframes.Count == 0) return;

        // Find the earliest time to use as reference point
        var minTime = SelectedKeyframes.Min(k => k.TimeSeconds);

        // Store keyframes with track index and relative time offsets
        _clipboardKeyframes = new List<(int, double, HueColor, double, TransitionStyle)>();

        foreach (var keyframe in SelectedKeyframes)
        {
            // Find which track this keyframe belongs to
            for (int trackIndex = 0; trackIndex < Tracks.Count; trackIndex++)
            {
                if (Tracks[trackIndex].Keyframes.Contains(keyframe))
                {
                    _clipboardKeyframes.Add((
                        TrackIndex: trackIndex,
                        TimeOffset: keyframe.TimeSeconds - minTime,
                        Color: keyframe.Color,
                        Brightness: keyframe.Brightness,
                        Transition: keyframe.Transition
                    ));
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Paste keyframes from clipboard to their original tracks.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanPaste))]
    public void PasteKeyframes()
    {
        if (_clipboardKeyframes == null || _clipboardKeyframes.Count == 0) return;

        var commands = new List<ITimelineCommand>();

        // Paste at playhead position
        var pasteTime = PlayheadPosition;

        foreach (var clipKeyframe in _clipboardKeyframes)
        {
            // Only paste if the track index is valid
            if (clipKeyframe.TrackIndex < 0 || clipKeyframe.TrackIndex >= Tracks.Count)
                continue;

            var track = Tracks[clipKeyframe.TrackIndex];
            var newTime = pasteTime + clipKeyframe.TimeOffset;

            // Ensure within bounds
            if (newTime < 0 || newTime > DurationSeconds) continue;

            var newKeyframe = new KeyframeViewModel
            {
                TimeSeconds = newTime,
                Color = clipKeyframe.Color ?? HueColors.WarmWhite,
                Brightness = clipKeyframe.Brightness,
                Transition = clipKeyframe.Transition
            };

            commands.Add(new AddKeyframeCommand(track, newKeyframe));
        }

        if (commands.Count > 0)
        {
            ExecuteCommand(new BatchCommand("Paste keyframes", commands));
        }
    }

    /// <summary>
    /// Delete selected keyframes.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasSelectedKeyframes))]
    public void DeleteSelectedKeyframes()
    {
        if (SelectedKeyframes.Count == 0) return;

        var commands = new List<(TrackViewModel Track, KeyframeViewModel Keyframe)>();

        foreach (var keyframe in SelectedKeyframes.ToList())
        {
            foreach (var track in Tracks)
            {
                if (track.Keyframes.Contains(keyframe))
                {
                    // Don't delete first or last keyframes (keyframes are maintained in sorted order)
                    var isFirstKeyframe = track.Keyframes.Count > 0 && track.Keyframes[0] == keyframe;
                    var isLastKeyframe = track.Keyframes.Count > 0 && track.Keyframes[^1] == keyframe;

                    if (!isFirstKeyframe && !isLastKeyframe)
                    {
                        commands.Add((track, keyframe));
                    }
                    break;
                }
            }
        }

        if (commands.Count > 0)
        {
            ExecuteCommand(new DeleteKeyframesCommand(commands));
            SelectedKeyframes.Clear();
        }
    }

    public bool HasSelectedKeyframes() => SelectedKeyframes.Count > 0;
    public bool CanPaste() => _clipboardKeyframes != null && _clipboardKeyframes.Count > 0;

    #endregion
}

/// <summary>
/// ViewModel for a single track (one per light).
/// </summary>
public partial class TrackViewModel : ObservableObject
{
    [ObservableProperty]
    private string _lightId = "";

    [ObservableProperty]
    private string _displayName = "";

    [ObservableProperty]
    private ObservableCollection<KeyframeViewModel> _keyframes = new();
}

/// <summary>
/// ViewModel for a single keyframe on the timeline.
/// </summary>
public partial class KeyframeViewModel : ObservableObject
{
    [ObservableProperty]
    private double _timeSeconds;

    [ObservableProperty]
    private HueColor _color = HueColors.WarmWhite;

    [ObservableProperty]
    private double _brightness = 1.0;

    [ObservableProperty]
    private TransitionStyle _transition = TransitionStyle.EaseInOut;
}

/// <summary>
/// Available event presets for event tracks.
/// </summary>
public enum EventPreset
{
    LightningFlash,
    Sparkle,
    CandleFlicker
}

/// <summary>
/// ViewModel for an event track (random triggers like lightning, sparkles).
/// </summary>
public partial class EventTrackViewModel : ObservableObject
{
    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString();

    [ObservableProperty]
    private string _displayName = "Lightning Flash";

    [ObservableProperty]
    private EventPreset _preset = EventPreset.LightningFlash;

    [ObservableProperty]
    private double _frequency = 0.5; // 0.0 (rare) to 1.0 (frequent)

    /// <summary>
    /// Gets the icon glyph for the preset.
    /// </summary>
    public string IconGlyph => Preset switch
    {
        EventPreset.LightningFlash => "\uE945", // Lightning bolt
        EventPreset.Sparkle => "\uE734", // Star
        EventPreset.CandleFlicker => "\uE7E8", // Brightness
        _ => "\uE768"
    };

    /// <summary>
    /// Gets the min/max interval based on frequency.
    /// </summary>
    public (double Min, double Max) GetInterval() => Frequency switch
    {
        < 0.33 => (8.0, 15.0),   // Rare
        < 0.66 => (3.0, 6.0),    // Medium
        _ => (0.5, 2.0)          // Frequent
    };

    /// <summary>
    /// Converts this event track to an AnimationDefinition for saving.
    /// </summary>
    public AnimationDefinition ToAnimationDefinition()
    {
        var (minInterval, maxInterval) = GetInterval();
        var triggers = GetPresetTriggers();

        return new AnimationDefinition
        {
            Id = Id,
            Name = DisplayName,
            Type = AnimationType.Event,
            LightAssignment = LightAssignment.Random,
            RandomPercentage = 1.0, // One light at a time
            EventPreset = Preset.ToString(),
            EventPattern = new EventPattern
            {
                Triggers = triggers,
                MinIntervalSeconds = minInterval,
                MaxIntervalSeconds = maxInterval,
                Probability = 1.0,
                AllowSimultaneous = false
            },
            RepeatMode = RepeatMode.Loop
        };
    }

    /// <summary>
    /// Gets the trigger states for the current preset.
    /// </summary>
    private List<EventTrigger> GetPresetTriggers()
    {
        return Preset switch
        {
            EventPreset.LightningFlash => new List<EventTrigger>
            {
                new EventTrigger
                {
                    Weight = 1.0,
                    States = new List<EventTriggerState>
                    {
                        // Flash on - bright white
                        new EventTriggerState
                        {
                            DurationSeconds = 0.1,
                            Brightness = 1.0,
                            Color = HueColors.CoolWhite,
                            TransitionStyle = TransitionStyle.Instant
                        },
                        // Fade out
                        new EventTriggerState
                        {
                            DurationSeconds = 0.2,
                            Brightness = 0.0,
                            TransitionStyle = TransitionStyle.EaseOut
                        }
                    }
                }
            },
            EventPreset.Sparkle => new List<EventTrigger>
            {
                new EventTrigger
                {
                    Weight = 1.0,
                    States = new List<EventTriggerState>
                    {
                        // Quick bright pulse
                        new EventTriggerState
                        {
                            DurationSeconds = 0.05,
                            Brightness = 1.0,
                            TransitionStyle = TransitionStyle.Instant
                        },
                        // Fade back
                        new EventTriggerState
                        {
                            DurationSeconds = 0.15,
                            Brightness = 0.5,
                            TransitionStyle = TransitionStyle.EaseOut
                        }
                    }
                }
            },
            EventPreset.CandleFlicker => new List<EventTrigger>
            {
                new EventTrigger
                {
                    Weight = 1.0,
                    States = new List<EventTriggerState>
                    {
                        // Dip down
                        new EventTriggerState
                        {
                            DurationSeconds = 0.15,
                            Brightness = 0.6,
                            Color = HueColors.WarmOrange,
                            TransitionStyle = TransitionStyle.EaseIn
                        },
                        // Return
                        new EventTriggerState
                        {
                            DurationSeconds = 0.15,
                            Brightness = 0.8,
                            TransitionStyle = TransitionStyle.EaseOut
                        }
                    }
                }
            },
            _ => new List<EventTrigger>()
        };
    }

    partial void OnPresetChanged(EventPreset value)
    {
        DisplayName = value switch
        {
            EventPreset.LightningFlash => "Lightning Flash",
            EventPreset.Sparkle => "Sparkle",
            EventPreset.CandleFlicker => "Candle Flicker",
            _ => "Event"
        };
    }
}
