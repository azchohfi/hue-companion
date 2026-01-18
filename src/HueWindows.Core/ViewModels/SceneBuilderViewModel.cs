using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HueWindows.Core.Models;
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
    private readonly IHueBridgeService _bridgeService;
    private readonly IAnimationService _animationService;
    private readonly ISceneStorageService _storageService;

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
        IHueBridgeService bridgeService,
        IAnimationService animationService,
        ISceneStorageService storageService)
    {
        _bridgeService = bridgeService ?? throw new ArgumentNullException(nameof(bridgeService));
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
        var roomsResult = await _bridgeService.GetRoomsAsync();
        if (roomsResult.IsSuccess && roomsResult.Value != null)
        {
            Rooms = new ObservableCollection<RoomModel>(roomsResult.Value);
        }

        var zonesResult = await _bridgeService.GetZonesAsync();
        if (zonesResult.IsSuccess && zonesResult.Value != null)
        {
            foreach (var zone in zonesResult.Value)
            {
                Rooms.Add(zone);
            }
        }
    }

    partial void OnSelectedRoomChanged(RoomModel? value)
    {
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
                                    Color = k.Color ?? new HueColor(0.45, 0.41),
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
                                Color = k.Color ?? new HueColor(0.45, 0.41),
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
            var track = new TrackViewModel
            {
                LightId = light.Id.ToString(),
                DisplayName = light.Name,
                Keyframes = new ObservableCollection<KeyframeViewModel>
                {
                    // Default: two keyframes at start and end with same color
                    new KeyframeViewModel
                    {
                        TimeSeconds = 0,
                        Color = new HueColor(0.45, 0.41), // Warm white
                        Brightness = 1.0,
                        Transition = TransitionStyle.EaseInOut
                    },
                    new KeyframeViewModel
                    {
                        TimeSeconds = DurationSeconds,
                        Color = new HueColor(0.45, 0.41),
                        Brightness = 1.0,
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
        var newColor = prevKeyframe?.Color ?? new HueColor(0.45, 0.41);
        var newBrightness = prevKeyframe?.Brightness ?? 1.0;

        if (prevKeyframe != null && nextKeyframe != null)
        {
            var t = (timeSeconds - prevKeyframe.TimeSeconds) /
                    (nextKeyframe.TimeSeconds - prevKeyframe.TimeSeconds);

            // Simple linear interpolation
            var x = prevKeyframe.Color.X + (nextKeyframe.Color.X - prevKeyframe.Color.X) * t;
            var y = prevKeyframe.Color.Y + (nextKeyframe.Color.Y - prevKeyframe.Color.Y) * t;
            newColor = new HueColor(x, y);
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
                // Don't delete if it's the only keyframe or one of two keyframes
                if (track.Keyframes.Count > 2)
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
    /// </summary>
    public async Task UpdateLightsForPlayheadAsync()
    {
        if (SelectedRoom == null || Tracks.Count == 0)
            return;

        foreach (var track in Tracks)
        {
            if (!Guid.TryParse(track.LightId, out var lightId))
                continue;

            var (color, brightness) = InterpolateAtTime(track, PlayheadPosition);

            // Send to light
            await _bridgeService.SetLightColorAsync(lightId, color);
            await _bridgeService.SetLightBrightnessAsync(lightId, brightness);
        }
    }

    /// <summary>
    /// Updates a single light to match a keyframe (for editing preview).
    /// </summary>
    public async Task UpdateLightForKeyframeAsync(KeyframeViewModel keyframe)
    {
        // Find which track this keyframe belongs to
        foreach (var track in Tracks)
        {
            if (track.Keyframes.Contains(keyframe))
            {
                if (Guid.TryParse(track.LightId, out var lightId))
                {
                    await _bridgeService.SetLightColorAsync(lightId, keyframe.Color);
                    await _bridgeService.SetLightBrightnessAsync(lightId, keyframe.Brightness);
                }
                break;
            }
        }
    }

    private (HueColor color, double brightness) InterpolateAtTime(TrackViewModel track, double timeSeconds)
    {
        if (track.Keyframes.Count == 0)
            return (new HueColor(0.45, 0.41), 1.0);

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
        t = ApplyEasing(t, next.Transition);

        // Interpolate color
        var x = prev.Color.X + (next.Color.X - prev.Color.X) * t;
        var y = prev.Color.Y + (next.Color.Y - prev.Color.Y) * t;
        var brightness = prev.Brightness + (next.Brightness - prev.Brightness) * t;

        return (new HueColor(x, y), brightness);
    }

    private double ApplyEasing(double t, TransitionStyle style)
    {
        return style switch
        {
            TransitionStyle.Linear => t,
            TransitionStyle.EaseIn => t * t,
            TransitionStyle.EaseOut => 1 - (1 - t) * (1 - t),
            TransitionStyle.EaseInOut => t < 0.5 ? 2 * t * t : 1 - Math.Pow(-2 * t + 2, 2) / 2,
            TransitionStyle.Instant => t >= 1 ? 1 : 0,
            _ => t
        };
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
            .Take(4)
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

                    // Try to determine preset from trigger states or native effect name
                    if (animation.Type == AnimationType.NativeEffect)
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
                                        Color = k.Color ?? new HueColor(0.45, 0.41),
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
                                    Color = k.Color ?? new HueColor(0.45, 0.41),
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
    private HueColor _color = new(0.45, 0.41);

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
                            Color = new HueColor(0.31, 0.32), // Cool white
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
                            Color = new HueColor(0.57, 0.41), // Warm orange
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
