using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using HueWindows.Core.Models;
using HueWindows.Core.Services.Interfaces;
using System.ComponentModel;
using static HueWindows.Core.Services.Interfaces.UIDispatcher;

namespace HueWindows.Core.ViewModels;

/// <summary>
/// ViewModel for the dashboard/home page showing room cards.
/// </summary>
public partial class DashboardViewModel : ObservableObject
{
    private readonly IMultiBridgeService _multiBridgeService;
    private readonly IMessenger _messenger;

    [ObservableProperty]
    private ObservableCollection<RoomCardViewModel> _roomCards = new();

    [ObservableProperty]
    private ObservableCollection<RoomCardViewModel> _zoneCards = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _showEmptyState;

    [ObservableProperty]
    private bool _hasRooms;

    [ObservableProperty]
    private bool _hasZones;

    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>
    /// Event raised when a room is selected for detail view.
    /// Includes initial state for smooth visual transitions.
    /// </summary>
    public event EventHandler<(Guid Id, LightGroupType Type, bool IsOn, List<(byte R, byte G, byte B)> Colors)>? RoomSelected;

    public DashboardViewModel(
        IMultiBridgeService multiBridgeService,
        IMessenger messenger)
    {
        _multiBridgeService = multiBridgeService;
        _messenger = messenger;

        // Subscribe to light state changes
        _multiBridgeService.LightStateChanged += OnMultiBridgeLightStateChanged;
    }

    [RelayCommand]
    public async Task LoadRoomsAsync()
    {
        if (IsLoading) return;

        IsLoading = true;
        ErrorMessage = null;
        ShowEmptyState = false;

        // Load rooms from all bridges
        var roomsResult = await _multiBridgeService.GetAllRoomsAsync();

        RoomCards.Clear();

        if (roomsResult.IsSuccess)
        {
            foreach (var room in roomsResult.Value!)
            {
                // Get the bridge service for this room
                var bridgeService = room.BridgeId != null
                    ? _multiBridgeService.GetBridgeService(room.BridgeId)
                    : null;

                if (bridgeService != null)
                {
                    var cardVm = new RoomCardViewModel(room, bridgeService);
                    cardVm.RoomTapped += OnRoomTapped;
                    RoomCards.Add(cardVm);
                }
            }
        }
        else
        {
            ErrorMessage = roomsResult.Error;
        }

        HasRooms = RoomCards.Count > 0;

        // Load zones from all bridges
        var zonesResult = await _multiBridgeService.GetAllZonesAsync();

        ZoneCards.Clear();

        if (zonesResult.IsSuccess)
        {
            foreach (var zone in zonesResult.Value!)
            {
                // Get the bridge service for this zone
                var bridgeService = zone.BridgeId != null
                    ? _multiBridgeService.GetBridgeService(zone.BridgeId)
                    : null;

                if (bridgeService != null)
                {
                    var cardVm = new RoomCardViewModel(zone, bridgeService);
                    cardVm.RoomTapped += OnRoomTapped;
                    ZoneCards.Add(cardVm);
                }
            }
        }
        else if (ErrorMessage == null)
        {
            // Only set error if we don't already have one from rooms
            ErrorMessage = zonesResult.Error;
        }

        HasZones = ZoneCards.Count > 0;

        ShowEmptyState = RoomCards.Count == 0 && ZoneCards.Count == 0;

        IsLoading = false;
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadRoomsAsync();
    }

    private void OnRoomTapped(object? sender, (Guid Id, LightGroupType Type, bool IsOn, List<(byte R, byte G, byte B)> Colors) args)
    {
        RoomSelected?.Invoke(this, args);
    }

    private void OnMultiBridgeLightStateChanged(object? sender, MultiBridgeLightStateChangedEventArgs e)
    {
        // Marshal to UI thread since event comes from background thread
        RunOnUIThread(() =>
        {
            // Update room cards when light states change
            foreach (var roomCard in RoomCards)
            {
                roomCard.OnLightStateChanged(e);
            }

            // Update zone cards when light states change
            foreach (var zoneCard in ZoneCards)
            {
                zoneCard.OnLightStateChanged(e);
            }
        });
    }
}

/// <summary>
/// ViewModel for an individual room card on the dashboard.
/// </summary>
public partial class RoomCardViewModel : ObservableObject, IRoomCardViewModel
{
    private readonly IHueBridgeService _bridgeService;
    private readonly RoomModel _room;

    public Guid RoomId => _room.Id;

    /// <summary>
    /// The item ID for pinning (same as RoomId).
    /// </summary>
    public Guid ItemId => _room.Id;

    /// <summary>
    /// The type of this item for pinning.
    /// </summary>
    public PinnedItemType ItemType => _room.GroupType == LightGroupType.Zone
        ? PinnedItemType.Zone
        : PinnedItemType.Room;

    [ObservableProperty]
    private string _roomName;

    [ObservableProperty]
    private bool _isOn;

    [ObservableProperty]
    private double _brightness;

    [ObservableProperty]
    private HueColor? _dominantColor;

    [ObservableProperty]
    private string _roomIcon;

    [ObservableProperty]
    private int _lightCount;

    [ObservableProperty]
    private bool _isAdjusting;

    /// <summary>
    /// Event raised when the room card is tapped (for navigation).
    /// Includes initial state for smooth visual transitions.
    /// </summary>
    public event EventHandler<(Guid Id, LightGroupType Type, bool IsOn, List<(byte R, byte G, byte B)> Colors)>? RoomTapped;

    public int BrightnessPercent => (int)(Brightness * 100);
    public string BrightnessDisplayText => $"{BrightnessPercent}%";

    // Explicit ICommand implementations for interface
    ICommand IRoomCardViewModel.TapRoomCommand => TapRoomCommand;
    ICommand IRoomCardViewModel.SetBrightnessCommand => SetBrightnessCommand;
    ICommand? IRoomCardViewModel.SetColorCommand => SetColorCommand;

    /// <summary>
    /// Whether any light in this room supports color.
    /// </summary>
    public bool SupportsColor => _room.Lights.Any(l => l.SupportsColor);

    /// <summary>
    /// Gets all unique light colors in the room as RGB values for gradient display.
    /// </summary>
    public List<(byte R, byte G, byte B)> LightColors
    {
        get
        {
            if (!IsOn) return new List<(byte R, byte G, byte B)>();

            var colors = _room.Lights
                .Where(l => l.IsOn && l.CurrentColor != null)
                .Select(l => l.CurrentColor!.ToRgb(1.0))
                .Distinct()
                .ToList();

            return colors;
        }
    }

    /// <summary>
    /// Gets the background color RGB values when the room is on.
    /// Returns null if no color is available or room is off.
    /// </summary>
    public (byte R, byte G, byte B)? BackgroundColorRgb
    {
        get
        {
            var colors = LightColors;
            if (colors.Count == 0) return null;
            return colors[0];
        }
    }

    /// <summary>
    /// Returns true if black text should be used on the current background.
    /// Uses the average luminance of all light colors.
    /// </summary>
    public bool UseBlackText
    {
        get
        {
            var colors = LightColors;
            if (colors.Count == 0) return false;

            // Calculate average luminance
            double totalLuminance = 0;
            foreach (var (r, g, b) in colors)
            {
                // Simple luminance calculation
                totalLuminance += (0.299 * r + 0.587 * g + 0.114 * b) / 255.0;
            }
            double avgLuminance = totalLuminance / colors.Count;

            return avgLuminance > 0.5;
        }
    }

    public RoomCardViewModel(RoomModel room, IHueBridgeService bridgeService)
    {
        _room = room;
        _bridgeService = bridgeService;

        _roomName = room.Name;
        _isOn = room.IsOn;
        _brightness = room.Brightness;
        _dominantColor = room.DominantColor;
        _lightCount = room.Lights.Count;
        _roomIcon = GetIconForArchetype(room.Archetype);
    }

    partial void OnIsOnChanged(bool value)
    {
        // Send command to bridge when toggle changes
        _ = _bridgeService.SetRoomOnAsync(RoomId, value);

        // Update computed color properties
        OnPropertyChanged(nameof(BackgroundColorRgb));
        OnPropertyChanged(nameof(UseBlackText));
    }

    partial void OnBrightnessChanged(double value)
    {
        // Update computed color properties when brightness changes
        OnPropertyChanged(nameof(BackgroundColorRgb));
        OnPropertyChanged(nameof(UseBlackText));
    }

    partial void OnDominantColorChanged(HueColor? value)
    {
        // Update computed color properties when color changes
        OnPropertyChanged(nameof(BackgroundColorRgb));
        OnPropertyChanged(nameof(UseBlackText));
    }

    [RelayCommand]
    private async Task SetBrightnessAsync(double brightness)
    {
        Brightness = Math.Clamp(brightness, 0.0, 1.0);
        OnPropertyChanged(nameof(BrightnessPercent));
        OnPropertyChanged(nameof(BrightnessDisplayText));

        await _bridgeService.SetRoomBrightnessAsync(RoomId, Brightness);

        // Update on state based on brightness
        // Set field directly to update UI without triggering OnIsOnChanged (which would send redundant API call)
#pragma warning disable MVVMTK0034
        if (Brightness > 0 && !IsOn)
        {
            _isOn = true;
            OnPropertyChanged(nameof(IsOn));
        }
        else if (Brightness == 0 && IsOn)
        {
            _isOn = false;
            OnPropertyChanged(nameof(IsOn));
        }
#pragma warning restore MVVMTK0034
    }

    [RelayCommand]
    private async Task SetColorAsync((byte R, byte G, byte B) rgb)
    {
        if (!SupportsColor) return;

        var color = HueColor.FromRgb(rgb.R, rgb.G, rgb.B);

        if (_room.GroupType == LightGroupType.Zone)
            await _bridgeService.SetZoneColorAsync(RoomId, color);
        else
            await _bridgeService.SetRoomColorAsync(RoomId, color);

        // Update local light models
        foreach (var light in _room.Lights.Where(l => l.SupportsColor))
        {
            light.CurrentColor = color;
        }

        DominantColor = color;
        OnPropertyChanged(nameof(BackgroundColorRgb));
        OnPropertyChanged(nameof(UseBlackText));
        OnPropertyChanged(nameof(LightColors));
    }

    [RelayCommand]
    private void TapRoom()
    {
        RoomTapped?.Invoke(this, (RoomId, _room.GroupType, IsOn, LightColors));
    }

    /// <summary>
    /// Called when a light state change event is received.
    /// </summary>
    public void OnLightStateChanged(LightStateChangedEventArgs e)
    {
        // Check if any light in this room changed
        var light = _room.Lights.FirstOrDefault(l => l.Id == e.LightId);
        if (light == null) return;

        // Update light state
        if (e.IsOn.HasValue)
        {
            light.IsOn = e.IsOn.Value;
        }
        if (e.Brightness.HasValue)
        {
            light.Brightness = e.Brightness.Value;
        }
        if (e.Color != null)
        {
            light.CurrentColor = e.Color;
        }

        // Recalculate room state
        IsOn = _room.Lights.Any(l => l.IsOn);
        Brightness = _room.Lights.Where(l => l.IsOn).DefaultIfEmpty()
            .Average(l => l?.Brightness ?? 0);

        var coloredLight = _room.Lights.FirstOrDefault(l => l.IsOn && l.CurrentColor != null);
        DominantColor = coloredLight?.CurrentColor;

        OnPropertyChanged(nameof(BrightnessPercent));
        OnPropertyChanged(nameof(BrightnessDisplayText));
        OnPropertyChanged(nameof(BackgroundColorRgb));
        OnPropertyChanged(nameof(UseBlackText));
        OnPropertyChanged(nameof(LightColors));
    }

    private static string GetIconForArchetype(RoomArchetype archetype)
    {
        return archetype switch
        {
            RoomArchetype.LivingRoom => "\uE7F4",    // Couch
            RoomArchetype.Lounge => "\uE7F4",        // Couch
            RoomArchetype.Kitchen => "\uED56",       // Kitchen
            RoomArchetype.Dining => "\uE799",        // Dining/eating
            RoomArchetype.Bedroom => "\uEC32",       // Bed
            RoomArchetype.KidsBedroom => "\uEC32",   // Bed
            RoomArchetype.GuestRoom => "\uEC32",     // Bed
            RoomArchetype.Bathroom => "\uE9FC",      // Shower
            RoomArchetype.Toilet => "\uE9FC",        // Shower
            RoomArchetype.Nursery => "\uE734",       // Star (for kids)
            RoomArchetype.Recreation => "\uE7FC",    // Game controller
            RoomArchetype.ManCave => "\uE7FC",       // Game controller
            RoomArchetype.Office => "\uE821",        // Briefcase
            RoomArchetype.Computer => "\uE7F8",      // Computer/PC
            RoomArchetype.Studio => "\uE722",        // Camera
            RoomArchetype.Gym => "\uE805",           // Fitness
            RoomArchetype.Hallway => "\uE8B0",       // Walking/hall
            RoomArchetype.Staircase => "\uE74A",     // Up arrow
            RoomArchetype.FrontDoor => "\uE7AD",     // Door
            RoomArchetype.Garage => "\uE804",        // Car
            RoomArchetype.Carport => "\uE804",       // Car
            RoomArchetype.Driveway => "\uE804",      // Car
            RoomArchetype.Terrace => "\uE8B3",       // Outdoor/sun
            RoomArchetype.Garden => "\uE8E2",        // Leaf/nature
            RoomArchetype.Balcony => "\uE8B3",       // Outdoor/sun
            RoomArchetype.Porch => "\uE8B3",         // Outdoor/sun
            RoomArchetype.Pool => "\uE8A2",          // Water
            RoomArchetype.Barbecue => "\uE8F9",      // Flame
            RoomArchetype.Home => "\uE80F",          // House
            RoomArchetype.Downstairs => "\uE74B",    // Down arrow
            RoomArchetype.Upstairs => "\uE74A",      // Up arrow
            RoomArchetype.TopFloor => "\uE74A",      // Up arrow
            RoomArchetype.Attic => "\uE74A",         // Up arrow
            RoomArchetype.Music => "\uE8D6",         // Music note
            RoomArchetype.TV => "\uE7F4",            // Display/monitor
            RoomArchetype.Reading => "\uE736",       // Book
            RoomArchetype.Closet => "\uE8AF",        // Cabinet
            RoomArchetype.Storage => "\uE8AF",       // Cabinet
            RoomArchetype.LaundryRoom => "\uE8AF",   // Cabinet
            _ => "\uE781"                            // Default light bulb
        };
    }
}
