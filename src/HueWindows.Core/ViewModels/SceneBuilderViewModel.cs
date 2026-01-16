using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HueWindows.Core.Models;
using HueWindows.Core.Services.Interfaces;
using System.Collections.ObjectModel;

namespace HueWindows.Core.ViewModels;

/// <summary>
/// ViewModel for the Scene Builder page.
/// </summary>
public partial class SceneBuilderViewModel : ObservableObject
{
    private readonly IHueBridgeService _bridgeService;
    private readonly IAnimationService _animationService;

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
    private KeyframeViewModel? _selectedKeyframe;

    [ObservableProperty]
    private double _playheadPosition;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private bool _isLooping = true;

    [ObservableProperty]
    private double _zoomLevel = 50; // Pixels per second

    public SceneBuilderViewModel(
        IHueBridgeService bridgeService,
        IAnimationService animationService)
    {
        _bridgeService = bridgeService ?? throw new ArgumentNullException(nameof(bridgeService));
        _animationService = animationService ?? throw new ArgumentNullException(nameof(animationService));
    }

    public async Task InitializeAsync()
    {
        await LoadRoomsAsync();
    }

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
        if (value != null)
        {
            CreateTracksForRoom(value);
        }
        else
        {
            Tracks.Clear();
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
    }

    [RelayCommand]
    private void SelectKeyframe(KeyframeViewModel keyframe)
    {
        SelectedKeyframe = keyframe;
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
                break;
            }
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
