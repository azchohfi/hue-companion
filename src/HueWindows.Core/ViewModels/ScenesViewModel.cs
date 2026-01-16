using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HueWindows.Core.Models;
using HueWindows.Core.Services.Interfaces;

namespace HueWindows.Core.ViewModels;

/// <summary>
/// ViewModel for the main Scenes page showing both static and animated scenes.
/// </summary>
public partial class ScenesViewModel : ObservableObject
{
    private readonly IHueBridgeService _bridgeService;
    private readonly IAnimationService _animationService;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private List<RoomModel> _rooms = new();

    [ObservableProperty]
    private RoomModel? _selectedRoom;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUserScenes))]
    private List<AnimatedSceneModel> _userScenes = new();

    [ObservableProperty]
    private List<AnimatedSceneModel> _presetScenes = new();

    public bool HasUserScenes => UserScenes.Count > 0;

    [ObservableProperty]
    private bool _isAnimationPlaying;

    [ObservableProperty]
    private string? _currentAnimationName;

    public ScenesViewModel(
        IHueBridgeService bridgeService,
        IAnimationService animationService)
    {
        _bridgeService = bridgeService ?? throw new ArgumentNullException(nameof(bridgeService));
        _animationService = animationService ?? throw new ArgumentNullException(nameof(animationService));

        _animationService.SceneStarted += OnSceneStarted;
        _animationService.SceneStopped += OnSceneStopped;
    }

    public async Task InitializeAsync()
    {
        await LoadDataAsync();
    }

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            // Load rooms
            var roomsResult = await _bridgeService.GetRoomsAsync();
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
        await _animationService.StopCurrentSceneAsync();
    }

    private void OnSceneStarted(object? sender, AnimatedSceneEventArgs e)
    {
        UpdateAnimationState();
    }

    private void OnSceneStopped(object? sender, AnimatedSceneEventArgs e)
    {
        UpdateAnimationState();
    }

    private void UpdateAnimationState()
    {
        IsAnimationPlaying = _animationService.IsPlaying;
        CurrentAnimationName = _animationService.CurrentScene?.Name;
    }

    partial void OnSelectedRoomChanged(RoomModel? value)
    {
        // Room selection changed - could update UI accordingly
    }
}
