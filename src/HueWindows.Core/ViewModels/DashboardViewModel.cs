using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using HueWindows.Core.Models;
using HueWindows.Core.Services.Interfaces;
using HueWindows.Core.Utilities;
using System.ComponentModel;
using static HueWindows.Core.Services.Interfaces.UIDispatcher;

namespace HueWindows.Core.ViewModels;

/// <summary>
/// ViewModel for the dashboard/home page showing room cards.
/// </summary>
public partial class DashboardViewModel : ObservableObject, IDisposable
{
    private readonly IMultiBridgeService _multiBridgeService;
    private readonly IMessenger _messenger;
    private readonly ISettingsService _settingsService;

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
        IMessenger messenger,
        ISettingsService settingsService)
    {
        _multiBridgeService = multiBridgeService;
        _messenger = messenger;
        _settingsService = settingsService;

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

        var disconnectedBridges = new HashSet<string>();

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
                    var cardVm = new RoomCardViewModel(room, bridgeService, _settingsService.Settings.CustomRoomIcons);
                    cardVm.RoomTapped += OnRoomTapped;
                    RoomCards.Add(cardVm);
                }
                else if (room.BridgeId != null)
                {
                    // Track disconnected bridge
                    disconnectedBridges.Add(room.BridgeName ?? room.BridgeId);
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
                    var cardVm = new RoomCardViewModel(zone, bridgeService, _settingsService.Settings.CustomRoomIcons);
                    cardVm.RoomTapped += OnRoomTapped;
                    ZoneCards.Add(cardVm);
                }
                else if (zone.BridgeId != null)
                {
                    // Track disconnected bridge
                    disconnectedBridges.Add(zone.BridgeName ?? zone.BridgeId);
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

        // Show warning if some bridges are disconnected
        if (disconnectedBridges.Count > 0 && ErrorMessage == null)
        {
            var bridgeList = string.Join(", ", disconnectedBridges);
            ErrorMessage = $"Some rooms not shown - bridge(s) disconnected: {bridgeList}";
        }

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

    public void Dispose()
    {
        _multiBridgeService.LightStateChanged -= OnMultiBridgeLightStateChanged;
    }
}

/// <summary>
/// ViewModel for an individual room card on the dashboard.
/// </summary>
public partial class RoomCardViewModel : ObservableObject, IRoomCardViewModel
{
    private readonly IHueBridgeService _bridgeService;
    private readonly RoomModel _room;
    private IReadOnlyList<(byte R, byte G, byte B)>? _lightColorsCache;

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
    /// Cached and invalidated when light state changes.
    /// </summary>
    public IReadOnlyList<(byte R, byte G, byte B)> LightColors
    {
        get
        {
            if (!IsOn) return Array.Empty<(byte R, byte G, byte B)>();

            if (_lightColorsCache == null)
            {
                _lightColorsCache = _room.Lights
                    .Where(l => l.IsOn && l.CurrentColor != null)
                    .Select(l => l.CurrentColor!.ToRgb(1.0))
                    .Distinct()
                    .ToList();
            }

            return _lightColorsCache;
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

    public RoomCardViewModel(RoomModel room, IHueBridgeService bridgeService, Dictionary<string, string>? customIcons = null)
    {
        _room = room;
        _bridgeService = bridgeService;

        _roomName = room.DisplayName;
        _isOn = room.IsOn;
        _brightness = room.Brightness;
        _dominantColor = room.DominantColor;
        _lightCount = room.Lights.Count;
        _roomIcon = RoomIconHelper.GetIconForRoom(room.Id, room.Archetype, customIcons);
    }

    partial void OnIsOnChanged(bool value)
    {
        // Send command to bridge when toggle changes
        _bridgeService.SetRoomOnAsync(RoomId, value).FireAndForget();

        // Invalidate cached colors and update computed properties
        InvalidateLightColorsCache();
    }

    partial void OnBrightnessChanged(double value)
    {
        // Invalidate cached colors and update computed properties when brightness changes
        InvalidateLightColorsCache();
    }

    partial void OnDominantColorChanged(HueColor? value)
    {
        // Invalidate cached colors and update computed properties when color changes
        InvalidateLightColorsCache();
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
        InvalidateLightColorsCache();
    }

    [RelayCommand]
    private void TapRoom()
    {
        RoomTapped?.Invoke(this, (RoomId, _room.GroupType, IsOn, LightColors.ToList()));
    }

    /// <summary>
    /// Invalidates the cached light colors and raises PropertyChanged for all color-dependent properties.
    /// </summary>
    private void InvalidateLightColorsCache()
    {
        _lightColorsCache = null;
        OnPropertyChanged(nameof(LightColors));
        OnPropertyChanged(nameof(BackgroundColorRgb));
        OnPropertyChanged(nameof(UseBlackText));
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
        InvalidateLightColorsCache();
    }

}
