using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HueWindows.Core.Models;
using HueWindows.Core.Services.Interfaces;

namespace HueWindows.Core.ViewModels;

/// <summary>
/// ViewModel for the Scene Library page showing animated scenes by category.
/// </summary>
public partial class SceneLibraryViewModel : ObservableObject, IDisposable
{
    private readonly IAnimationService _animationService;
    private readonly IMultiBridgeService _multiBridgeService;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private List<AnimatedSceneModel> _allScenes = new();

    [ObservableProperty]
    private List<AnimatedSceneModel> _filteredScenes = new();

    [ObservableProperty]
    private string _selectedCategory = "All";

    [ObservableProperty]
    private List<string> _categories = new();

    [ObservableProperty]
    private List<RoomModel> _rooms = new();

    [ObservableProperty]
    private RoomModel? _selectedRoom;

    [ObservableProperty]
    private bool _isAnimationPlaying;

    [ObservableProperty]
    private string? _currentAnimationName;

    public SceneLibraryViewModel(
        IAnimationService animationService,
        IMultiBridgeService multiBridgeService)
    {
        _animationService = animationService ?? throw new ArgumentNullException(nameof(animationService));
        _multiBridgeService = multiBridgeService ?? throw new ArgumentNullException(nameof(multiBridgeService));

        _animationService.RoomAnimationChanged += OnRoomAnimationChanged;
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
            // Load rooms from all bridges
            var roomsResult = await _multiBridgeService.GetAllRoomsAsync();
            if (roomsResult.IsSuccess && roomsResult.Value != null)
            {
                Rooms = roomsResult.Value.ToList();
                SelectedRoom = Rooms.FirstOrDefault();
            }

            // Load animated scenes
            var scenesResult = await _animationService.GetAllScenesAsync();
            if (scenesResult.IsSuccess && scenesResult.Value != null)
            {
                AllScenes = scenesResult.Value.ToList();

                // Extract categories
                Categories = new List<string> { "All" };
                Categories.AddRange(AllScenes
                    .Select(s => s.Category)
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .Distinct()
                    .OrderBy(c => c));

                ApplyFilter();
            }

            // Update animation state
            UpdateAnimationState();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load scenes: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void SelectCategory(string category)
    {
        SelectedCategory = category;
        ApplyFilter();
    }

    [RelayCommand]
    private async Task StartScene(AnimatedSceneModel scene)
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
            ErrorMessage = result.Error ?? "Failed to start scene";
        }
    }

    [RelayCommand]
    private async Task StopScene()
    {
        if (SelectedRoom != null)
        {
            await _animationService.StopSceneInRoomAsync(SelectedRoom.Id);
        }
    }

    private void ApplyFilter()
    {
        if (SelectedCategory == "All")
        {
            FilteredScenes = AllScenes.ToList();
        }
        else
        {
            FilteredScenes = AllScenes
                .Where(s => s.Category.Equals(SelectedCategory, StringComparison.OrdinalIgnoreCase))
                .ToList();
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

    partial void OnSelectedCategoryChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnSelectedRoomChanged(RoomModel? value)
    {
        UpdateAnimationState();
    }

    public void Dispose()
    {
        _animationService.RoomAnimationChanged -= OnRoomAnimationChanged;
    }
}
