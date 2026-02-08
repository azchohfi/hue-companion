using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HueWindows.Core.Models;
using HueWindows.Core.Services.Interfaces;

namespace HueWindows.Core.ViewModels;

/// <summary>
/// ViewModel for the main Scenes page showing both static and animated scenes.
/// </summary>
public partial class ScenesViewModel : ObservableObject, IDisposable
{
    private readonly IMultiBridgeService _multiBridgeService;
    private readonly IAnimationService _animationService;
    private readonly ISceneStorageService _sceneStorageService;
    private readonly IRoomSceneAssignmentService _assignmentService;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private List<RoomModel> _rooms = new();

    [ObservableProperty]
    private RoomModel? _selectedRoom;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRoomContext))]
    private Guid? _contextRoomId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRoomContext))]
    private string? _contextRoomName;

    public bool HasRoomContext => ContextRoomId != null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUserScenes))]
    private List<AnimatedSceneModel> _userScenes = new();

    [ObservableProperty]
    private List<AnimatedSceneModel> _presetScenes = new();

    public bool HasUserScenes => UserScenes.Count > 0;

    /// <summary>
    /// Gets all available native Hue effects.
    /// </summary>
    public IReadOnlyList<NativeEffectInfo> NativeEffects => NativeEffectInfo.All;

    [ObservableProperty]
    private bool _isAnimationPlaying;

    [ObservableProperty]
    private string? _currentAnimationName;

    public ScenesViewModel(
        IMultiBridgeService multiBridgeService,
        IAnimationService animationService,
        ISceneStorageService sceneStorageService,
        IRoomSceneAssignmentService assignmentService)
    {
        _multiBridgeService = multiBridgeService ?? throw new ArgumentNullException(nameof(multiBridgeService));
        _animationService = animationService ?? throw new ArgumentNullException(nameof(animationService));
        _sceneStorageService = sceneStorageService ?? throw new ArgumentNullException(nameof(sceneStorageService));
        _assignmentService = assignmentService ?? throw new ArgumentNullException(nameof(assignmentService));

        _animationService.RoomAnimationChanged += OnRoomAnimationChanged;
    }

    /// <summary>
    /// Sets the room context when navigating from a room detail page.
    /// Pre-selects the room and enables the "Add to Room" flow.
    /// </summary>
    public void SetRoomContext(Guid roomId, string roomName)
    {
        ContextRoomId = roomId;
        ContextRoomName = roomName;
    }

    public async Task InitializeAsync()
    {
        await LoadDataAsync();

        // Pre-select the context room if set
        if (ContextRoomId != null && Rooms.Count > 0)
        {
            SelectedRoom = Rooms.FirstOrDefault(r => r.Id == ContextRoomId.Value) ?? Rooms.FirstOrDefault();
        }
    }

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            // Load rooms from all bridges
            var roomsResult = await _multiBridgeService.GetAllRoomsAsync();
            if (roomsResult.IsSuccess && roomsResult.Value != null)
            {
                Rooms = roomsResult.Value.ToList();
                SelectedRoom = Rooms.FirstOrDefault();
            }

            // Load animated scenes and separate by type
            var scenesResult = await _animationService.GetAllScenesAsync();
            if (scenesResult.IsSuccess && scenesResult.Value != null)
            {
                UserScenes = scenesResult.Value.Where(s => !s.IsBuiltIn).ToList();
                PresetScenes = scenesResult.Value.Where(s => s.IsBuiltIn).ToList();
            }

            // Update animation state
            UpdateAnimationState();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load data: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task StartAnimatedScene(AnimatedSceneModel scene)
    {
        if (SelectedRoom == null)
        {
            ErrorMessage = "Please select a room first";
            return;
        }

        ErrorMessage = string.Empty;

        var result = await _animationService.StartSceneAsync(scene.Id, SelectedRoom.Id);
        if (result.IsFailure)
        {
            ErrorMessage = result.Error ?? "Failed to start animated scene";
        }
    }

    [RelayCommand]
    private async Task StopAnimatedScene()
    {
        if (SelectedRoom != null)
        {
            await _animationService.StopSceneInRoomAsync(SelectedRoom.Id);
        }
    }

    /// <summary>
    /// Adds an animated scene to the selected (or context) room.
    /// </summary>
    [RelayCommand]
    private async Task AddToRoomAsync(AnimatedSceneModel scene)
    {
        var roomId = ContextRoomId ?? SelectedRoom?.Id;
        if (roomId == null)
        {
            ErrorMessage = "Please select a room first";
            return;
        }

        await _assignmentService.AssignSceneToRoomAsync(roomId.Value, scene.Id);

        if (HasRoomContext)
        {
            ErrorMessage = string.Empty;
        }
    }

    /// <summary>
    /// Applies a native Hue effect to all lights in the selected room.
    /// </summary>
    /// <param name="effect">The effect identifier (e.g., "fire", "candle").</param>
    /// <param name="speed">Effect speed (0.0-1.0).</param>
    /// <param name="brightness">Brightness level (0.0-1.0).</param>
    public async Task ApplyEffectToRoomAsync(string effect, double speed, double brightness)
    {
        if (SelectedRoom == null) return;

        var bridgeService = SelectedRoom.BridgeId != null
            ? _multiBridgeService.GetBridgeService(SelectedRoom.BridgeId)
            : _multiBridgeService.GetDefaultBridgeService();

        if (bridgeService == null)
        {
            ErrorMessage = "No bridge connected.";
            return;
        }

        try
        {
            var lights = await bridgeService.GetLightsInRoomAsync(SelectedRoom.Id);
            if (lights.IsSuccess && lights.Value != null)
            {
                foreach (var light in lights.Value)
                {
                    await bridgeService.ApplyEffectAsync(light.Id, effect, speed, brightness);
                }
            }
            else
            {
                ErrorMessage = "Failed to get lights for room.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to apply effect: {ex.Message}";
        }
    }

    private void OnRoomAnimationChanged(object? sender, RoomAnimationChangedEventArgs e)
    {
        UpdateAnimationState();
    }

    private void UpdateAnimationState()
    {
        // Check if animation is running in the selected room
        if (SelectedRoom != null)
        {
            IsAnimationPlaying = _animationService.IsAnimationRunning(SelectedRoom.Id);
            CurrentAnimationName = _animationService.GetRunningScene(SelectedRoom.Id)?.Name;
        }
        else
        {
            IsAnimationPlaying = _animationService.IsAnyAnimationRunning;
            CurrentAnimationName = null;
        }
    }

    partial void OnSelectedRoomChanged(RoomModel? value)
    {
        // Update animation state for the newly selected room
        UpdateAnimationState();
    }

    [RelayCommand]
    private async Task DeleteUserSceneAsync(AnimatedSceneModel scene)
    {
        var result = await _sceneStorageService.DeleteSceneAsync(scene.Id);
        if (result.IsSuccess)
        {
            UserScenes = UserScenes.Where(s => s.Id != scene.Id).ToList();
        }
        else
        {
            ErrorMessage = result.Error ?? "Failed to delete scene";
        }
    }

    /// <summary>
    /// Saves a user-created scene.
    /// </summary>
    public async Task SaveUserSceneAsync(AnimatedSceneModel scene)
    {
        var result = await _sceneStorageService.SaveSceneAsync(scene);
        if (result.IsSuccess)
        {
            // Refresh user scenes list
            UserScenes = new List<AnimatedSceneModel>(UserScenes) { scene };
        }
        else
        {
            ErrorMessage = result.Error ?? "Failed to save scene";
        }
    }

    public void Dispose()
    {
        _animationService.RoomAnimationChanged -= OnRoomAnimationChanged;
    }
}
