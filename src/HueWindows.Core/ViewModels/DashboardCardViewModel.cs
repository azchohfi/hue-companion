using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HueWindows.Core.Models;
using HueWindows.Core.Services.Interfaces;

namespace HueWindows.Core.ViewModels;

/// <summary>
/// Unified ViewModel for items on the custom dashboard.
/// Can represent a room, zone, or individual light.
/// Implements same interface as RoomCardViewModel so RoomCard control can bind to it.
/// </summary>
public partial class DashboardCardViewModel : ObservableObject, IRoomCardViewModel
{
    private readonly IHueBridgeService _bridgeService;
    private readonly IPinnedItemsService _pinnedItemsService;

    // The underlying data (only one will be set)
    private readonly RoomModel? _room;
    private readonly LightModel? _light;

    /// <summary>
    /// The ID of this item (room, zone, or light).
    /// </summary>
    public Guid ItemId { get; }

    /// <summary>
    /// The type of this pinned item.
    /// </summary>
    public PinnedItemType ItemType { get; }

    [ObservableProperty]
    private string _roomName = string.Empty;

    [ObservableProperty]
    private bool _isOn;

    [ObservableProperty]
    private double _brightness;

    [ObservableProperty]
    private HueColor? _dominantColor;

    [ObservableProperty]
    private string _roomIcon = "\uE781";

    [ObservableProperty]
    private int _lightCount;

    [ObservableProperty]
    private bool _isAdjusting;

    /// <summary>
    /// Event raised when the card is tapped (for navigation).
    /// </summary>
    public event EventHandler<(Guid Id, PinnedItemType Type)>? CardTapped;

    public int BrightnessPercent => (int)(Brightness * 100);
    public string BrightnessDisplayText => $"{BrightnessPercent}%";

    // Explicit ICommand implementations for interface
    ICommand IRoomCardViewModel.TapRoomCommand => TapRoomCommand;
    ICommand IRoomCardViewModel.SetBrightnessCommand => SetBrightnessCommand;

    /// <summary>
    /// Gets light colors for gradient display.
    /// </summary>
    public List<(byte R, byte G, byte B)> LightColors
    {
        get
        {
            if (!IsOn) return new List<(byte R, byte G, byte B)>();

            if (_room != null)
            {
                return _room.Lights
                    .Where(l => l.IsOn && l.CurrentColor != null)
                    .Select(l => l.CurrentColor!.ToRgb(1.0))
                    .Distinct()
                    .ToList();
            }
            else if (_light?.CurrentColor != null)
            {
                return new List<(byte R, byte G, byte B)> { _light.CurrentColor.ToRgb(1.0) };
            }

            return new List<(byte R, byte G, byte B)>();
        }
    }

    public (byte R, byte G, byte B)? BackgroundColorRgb
    {
        get
        {
            var colors = LightColors;
            return colors.Count > 0 ? colors[0] : null;
        }
    }

    public bool UseBlackText
    {
        get
        {
            var colors = LightColors;
            if (colors.Count == 0) return false;

            double totalLuminance = 0;
            foreach (var (r, g, b) in colors)
            {
                totalLuminance += (0.299 * r + 0.587 * g + 0.114 * b) / 255.0;
            }
            return (totalLuminance / colors.Count) > 0.5;
        }
    }

    /// <summary>
    /// Creates a card for a room or zone.
    /// </summary>
    public DashboardCardViewModel(
        RoomModel room,
        IHueBridgeService bridgeService,
        IPinnedItemsService pinnedItemsService)
    {
        _room = room;
        _bridgeService = bridgeService;
        _pinnedItemsService = pinnedItemsService;

        ItemId = room.Id;
        ItemType = room.GroupType == LightGroupType.Zone ? PinnedItemType.Zone : PinnedItemType.Room;

        RoomName = room.Name;
        IsOn = room.IsOn;
        Brightness = room.Brightness;
        DominantColor = room.DominantColor;
        LightCount = room.Lights.Count;
        RoomIcon = GetIconForArchetype(room.Archetype);
    }

    /// <summary>
    /// Creates a card for an individual light.
    /// </summary>
    public DashboardCardViewModel(
        LightModel light,
        IHueBridgeService bridgeService,
        IPinnedItemsService pinnedItemsService)
    {
        _light = light;
        _bridgeService = bridgeService;
        _pinnedItemsService = pinnedItemsService;

        ItemId = light.Id;
        ItemType = PinnedItemType.Light;

        RoomName = light.Name;
        IsOn = light.IsOn;
        Brightness = light.Brightness;
        DominantColor = light.CurrentColor;
        LightCount = 1;
        RoomIcon = "\uE781"; // Light bulb icon
    }

    partial void OnIsOnChanged(bool value)
    {
        if (_room != null)
        {
            _ = _bridgeService.SetRoomOnAsync(ItemId, value);
        }
        else if (_light != null)
        {
            _ = _bridgeService.SetLightOnAsync(ItemId, value);
        }

        OnPropertyChanged(nameof(BackgroundColorRgb));
        OnPropertyChanged(nameof(UseBlackText));
        OnPropertyChanged(nameof(LightColors));
    }

    [RelayCommand]
    private async Task SetBrightnessAsync(double brightness)
    {
        Brightness = Math.Clamp(brightness, 0.0, 1.0);
        OnPropertyChanged(nameof(BrightnessPercent));
        OnPropertyChanged(nameof(BrightnessDisplayText));

        if (_room != null)
        {
            await _bridgeService.SetRoomBrightnessAsync(ItemId, Brightness);
        }
        else if (_light != null)
        {
            await _bridgeService.SetLightBrightnessAsync(ItemId, Brightness);
        }

        // Update on state based on brightness
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
    }

    [RelayCommand]
    private void TapRoom()
    {
        CardTapped?.Invoke(this, (ItemId, ItemType));
    }

    [RelayCommand]
    private async Task UnpinAsync()
    {
        await _pinnedItemsService.UnpinAsync(ItemId, ItemType);
    }

    /// <summary>
    /// Called when a light state change event is received.
    /// </summary>
    public void OnLightStateChanged(LightStateChangedEventArgs e)
    {
        if (_room != null)
        {
            // Check if any light in this room changed
            var light = _room.Lights.FirstOrDefault(l => l.Id == e.LightId);
            if (light == null) return;

            // Update light state
            if (e.IsOn.HasValue) light.IsOn = e.IsOn.Value;
            if (e.Brightness.HasValue) light.Brightness = e.Brightness.Value;
            if (e.Color != null) light.CurrentColor = e.Color;

            // Recalculate room state
            IsOn = _room.Lights.Any(l => l.IsOn);
            Brightness = _room.Lights.Where(l => l.IsOn).DefaultIfEmpty()
                .Average(l => l?.Brightness ?? 0);

            var coloredLight = _room.Lights.FirstOrDefault(l => l.IsOn && l.CurrentColor != null);
            DominantColor = coloredLight?.CurrentColor;
        }
        else if (_light != null && _light.Id == e.LightId)
        {
            // Update this light's state
            if (e.IsOn.HasValue) IsOn = e.IsOn.Value;
            if (e.Brightness.HasValue) Brightness = e.Brightness.Value;
            if (e.Color != null) DominantColor = e.Color;
        }

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
            RoomArchetype.LivingRoom => "\uE7F4",
            RoomArchetype.Lounge => "\uE7F4",
            RoomArchetype.Kitchen => "\uED56",
            RoomArchetype.Dining => "\uE799",
            RoomArchetype.Bedroom => "\uEC32",
            RoomArchetype.KidsBedroom => "\uEC32",
            RoomArchetype.GuestRoom => "\uEC32",
            RoomArchetype.Bathroom => "\uE9FC",
            RoomArchetype.Toilet => "\uE9FC",
            RoomArchetype.Nursery => "\uE734",
            RoomArchetype.Recreation => "\uE7FC",
            RoomArchetype.ManCave => "\uE7FC",
            RoomArchetype.Office => "\uE821",
            RoomArchetype.Computer => "\uE7F8",
            RoomArchetype.Studio => "\uE722",
            RoomArchetype.Gym => "\uE805",
            RoomArchetype.Hallway => "\uE8B0",
            RoomArchetype.Staircase => "\uE74A",
            RoomArchetype.FrontDoor => "\uE7AD",
            RoomArchetype.Garage => "\uE804",
            RoomArchetype.Carport => "\uE804",
            RoomArchetype.Driveway => "\uE804",
            RoomArchetype.Terrace => "\uE8B3",
            RoomArchetype.Garden => "\uE8E2",
            RoomArchetype.Balcony => "\uE8B3",
            RoomArchetype.Porch => "\uE8B3",
            RoomArchetype.Pool => "\uE8A2",
            RoomArchetype.Barbecue => "\uE8F9",
            RoomArchetype.Home => "\uE80F",
            RoomArchetype.Downstairs => "\uE74B",
            RoomArchetype.Upstairs => "\uE74A",
            RoomArchetype.TopFloor => "\uE74A",
            RoomArchetype.Attic => "\uE74A",
            RoomArchetype.Music => "\uE8D6",
            RoomArchetype.TV => "\uE7F4",
            RoomArchetype.Reading => "\uE736",
            RoomArchetype.Closet => "\uE8AF",
            RoomArchetype.Storage => "\uE8AF",
            RoomArchetype.LaundryRoom => "\uE8AF",
            _ => "\uE781"
        };
    }
}
