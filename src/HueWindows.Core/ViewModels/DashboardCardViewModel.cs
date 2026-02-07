using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HueWindows.Core.Models;
using HueWindows.Core.Services.Interfaces;
using HueWindows.Core.Utilities;

namespace HueWindows.Core.ViewModels;

/// <summary>
/// Unified ViewModel for items on the custom dashboard.
/// Can represent a room, zone, or individual light.
/// Implements same interface as RoomCardViewModel so RoomCard control can bind to it.
/// </summary>
public partial class DashboardCardViewModel : ObservableObject, IRoomCardViewModel
{
    private readonly IMultiBridgeService _multiBridgeService;
    private readonly IPinnedItemsService _pinnedItemsService;
    private readonly string? _bridgeId;

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
    ICommand? IRoomCardViewModel.SetColorCommand => SetColorCommand;

    /// <summary>
    /// Whether this item supports color.
    /// </summary>
    public bool SupportsColor
    {
        get
        {
            if (_room != null)
                return _room.Lights.Any(l => l.SupportsColor);
            return _light?.SupportsColor ?? false;
        }
    }

    /// <summary>
    /// Gets light colors for gradient display.
    /// </summary>
    public IReadOnlyList<(byte R, byte G, byte B)> LightColors
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
        IMultiBridgeService multiBridgeService,
        IPinnedItemsService pinnedItemsService)
    {
        _room = room;
        _multiBridgeService = multiBridgeService;
        _pinnedItemsService = pinnedItemsService;
        _bridgeId = room.BridgeId;

        ItemId = room.Id;
        ItemType = room.GroupType == LightGroupType.Zone ? PinnedItemType.Zone : PinnedItemType.Room;

        RoomName = room.DisplayName;
        IsOn = room.IsOn;
        Brightness = room.Brightness;
        DominantColor = room.DominantColor;
        LightCount = room.Lights.Count;
        RoomIcon = RoomIconHelper.GetIconForArchetype(room.Archetype);
    }

    /// <summary>
    /// Creates a card for an individual light.
    /// </summary>
    public DashboardCardViewModel(
        LightModel light,
        IMultiBridgeService multiBridgeService,
        IPinnedItemsService pinnedItemsService,
        string? bridgeId = null)
    {
        _light = light;
        _multiBridgeService = multiBridgeService;
        _pinnedItemsService = pinnedItemsService;
        _bridgeId = bridgeId;

        ItemId = light.Id;
        ItemType = PinnedItemType.Light;

        RoomName = light.Name;
        IsOn = light.IsOn;
        Brightness = light.Brightness;
        DominantColor = light.CurrentColor;
        LightCount = 1;
        RoomIcon = "\uE781"; // Light bulb icon
    }

    /// <summary>
    /// Gets the bridge service for this item's bridge.
    /// </summary>
    private IHueBridgeService? GetBridgeService()
    {
        return _bridgeId != null
            ? _multiBridgeService.GetBridgeService(_bridgeId)
            : _multiBridgeService.GetDefaultBridgeService();
    }

    partial void OnIsOnChanged(bool value)
    {
        var bridgeService = GetBridgeService();
        if (bridgeService == null) return;

        if (_room != null)
        {
            bridgeService.SetRoomOnAsync(ItemId, value).FireAndForget();
        }
        else if (_light != null)
        {
            bridgeService.SetLightOnAsync(ItemId, value).FireAndForget();
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

        var bridgeService = GetBridgeService();
        if (bridgeService != null)
        {
            if (_room != null)
            {
                await bridgeService.SetRoomBrightnessAsync(ItemId, Brightness);
            }
            else if (_light != null)
            {
                await bridgeService.SetLightBrightnessAsync(ItemId, Brightness);
            }
        }

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

        var bridgeService = GetBridgeService();
        if (bridgeService != null)
        {
            if (_room != null)
            {
                if (_room.GroupType == LightGroupType.Zone)
                    await bridgeService.SetZoneColorAsync(ItemId, color);
                else
                    await bridgeService.SetRoomColorAsync(ItemId, color);

                // Update local light models
                foreach (var light in _room.Lights.Where(l => l.SupportsColor))
                {
                    light.CurrentColor = color;
                }
            }
            else if (_light != null)
            {
                await bridgeService.SetLightColorAsync(ItemId, color);
                _light.CurrentColor = color;
            }
        }

        DominantColor = color;
        OnPropertyChanged(nameof(BackgroundColorRgb));
        OnPropertyChanged(nameof(UseBlackText));
        OnPropertyChanged(nameof(LightColors));
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

}
