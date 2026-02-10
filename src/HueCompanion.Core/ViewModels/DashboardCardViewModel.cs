using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HueCompanion.Core.Models;
using HueCompanion.Core.Services.Interfaces;
using HueCompanion.Core.Utilities;

namespace HueCompanion.Core.ViewModels;

/// <summary>
/// Lightweight data carrier for a scene chip displayed on a dashboard card.
/// </summary>
public record SceneChipItem(Guid SceneId, string Name, string Color1Hex, bool IsFavorite);

/// <summary>
/// Unified ViewModel for items on the custom dashboard.
/// Can represent a room, zone, or individual light.
/// Implements same interface as RoomCardViewModel so RoomCard control can bind to it.
/// </summary>
public partial class DashboardCardViewModel : ObservableObject, IRoomCardViewModel
{
    private readonly IMultiBridgeService _multiBridgeService;
    private readonly IPinnedItemsService _pinnedItemsService;
    private readonly ISettingsService _settingsService;
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

    // Scene quick-access fields (only used for room/zone items)
    private bool _scenesLoaded;
    private List<SceneModel> _allScenes = new();

    [ObservableProperty]
    private ObservableCollection<SceneChipItem> _sceneChips = new();

    [ObservableProperty]
    private bool _hasScenes;

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
        IPinnedItemsService pinnedItemsService,
        ISettingsService settingsService)
    {
        _room = room;
        _multiBridgeService = multiBridgeService;
        _pinnedItemsService = pinnedItemsService;
        _settingsService = settingsService;
        _bridgeId = room.BridgeId;

        ItemId = room.Id;
        ItemType = room.GroupType == LightGroupType.Zone ? PinnedItemType.Zone : PinnedItemType.Room;

        RoomName = room.DisplayName;
        _isOn = room.IsOn; // Use backing field to avoid triggering OnIsOnChanged → SetRoomOnAsync
        Brightness = room.Brightness;
        DominantColor = room.DominantColor;
        LightCount = room.Lights.Count;
        RoomIcon = RoomIconHelper.GetIconForRoom(room.Id, room.Archetype, settingsService.Settings.CustomRoomIcons);
    }

    /// <summary>
    /// Creates a card for an individual light.
    /// </summary>
    public DashboardCardViewModel(
        LightModel light,
        IMultiBridgeService multiBridgeService,
        IPinnedItemsService pinnedItemsService,
        ISettingsService settingsService,
        string? bridgeId = null)
    {
        _light = light;
        _multiBridgeService = multiBridgeService;
        _pinnedItemsService = pinnedItemsService;
        _settingsService = settingsService;
        _bridgeId = bridgeId;

        ItemId = light.Id;
        ItemType = PinnedItemType.Light;

        RoomName = light.Name;
        _isOn = light.IsOn; // Use backing field to avoid triggering OnIsOnChanged → SetLightOnAsync
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
    /// Lazily loads scenes for this room/zone on first call. No-op for individual lights.
    /// </summary>
    public async Task LoadScenesAsync()
    {
        if (_scenesLoaded || _room == null) return;
        _scenesLoaded = true;

        var bridgeService = GetBridgeService();
        if (bridgeService == null) return;

        var result = _room.GroupType == LightGroupType.Zone
            ? await bridgeService.GetScenesForZoneAsync(ItemId)
            : await bridgeService.GetScenesForRoomAsync(ItemId);

        if (result.IsFailure) return;
        _allScenes = result.Value!.ToList();

        RebuildSceneChips();
        HasScenes = _allScenes.Count > 0;
    }

    /// <summary>
    /// Activates a scene and records the activation for quick-access tracking.
    /// </summary>
    public async Task ActivateSceneAsync(Guid sceneId)
    {
        var bridgeService = GetBridgeService();
        if (bridgeService == null) return;

        await bridgeService.ActivateSceneAsync(sceneId);
        RecordSceneActivation(_settingsService, ItemId, sceneId);
        await _settingsService.SaveAsync();
    }

    /// <summary>
    /// Gets all scenes as SceneChipItem list for the flyout.
    /// </summary>
    public IReadOnlyList<SceneChipItem> GetAllSceneItems()
    {
        var groupKey = ItemId.ToString();
        var favs = _settingsService.Settings.SceneFavorites.TryGetValue(groupKey, out var f) ? f : new();
        return _allScenes.Select(s =>
        {
            var hex = s.PaletteColors.Count > 0
                ? ToHex(s.PaletteColors[0].ToRgb(1.0))
                : "#888888";
            return new SceneChipItem(s.Id, s.Name, hex, favs.Contains(s.Id.ToString()));
        }).ToList();
    }

    private void RebuildSceneChips()
    {
        SceneChips.Clear();
        var quickIds = GetQuickAccessSceneIds(_settingsService, ItemId, 3);
        foreach (var id in quickIds)
        {
            var scene = _allScenes.FirstOrDefault(s => s.Id == id);
            if (scene == null) continue;
            var hex = scene.PaletteColors.Count > 0
                ? ToHex(scene.PaletteColors[0].ToRgb(1.0))
                : "#888888";
            var groupKey = ItemId.ToString();
            var isFav = _settingsService.Settings.SceneFavorites
                .TryGetValue(groupKey, out var favs) && favs.Contains(id.ToString());
            SceneChips.Add(new SceneChipItem(scene.Id, scene.Name, hex, isFav));
        }
    }

    /// <summary>
    /// Returns quick-access scene IDs: favorites first, then most recent, deduplicated.
    /// </summary>
    public static List<Guid> GetQuickAccessSceneIds(ISettingsService settings, Guid groupId, int max = 3)
    {
        var groupKey = groupId.ToString();
        var result = new List<Guid>();
        var seen = new HashSet<string>();

        // Favorites first
        if (settings.Settings.SceneFavorites.TryGetValue(groupKey, out var favs))
        {
            foreach (var id in favs)
            {
                if (seen.Add(id) && Guid.TryParse(id, out var guid))
                {
                    result.Add(guid);
                    if (result.Count >= max) return result;
                }
            }
        }

        // Then recents by most recent first
        if (settings.Settings.SceneRecents.TryGetValue(groupKey, out var recents))
        {
            foreach (var activation in recents.OrderByDescending(a => a.ActivatedAt))
            {
                if (seen.Add(activation.SceneId) && Guid.TryParse(activation.SceneId, out var guid))
                {
                    result.Add(guid);
                    if (result.Count >= max) return result;
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Records a scene activation for recency tracking. Call from any context that activates a scene.
    /// </summary>
    public static void RecordSceneActivation(ISettingsService settings, Guid groupId, Guid sceneId)
    {
        var groupKey = groupId.ToString();
        var sceneKey = sceneId.ToString();

        if (!settings.Settings.SceneRecents.TryGetValue(groupKey, out var recents))
        {
            recents = new();
            settings.Settings.SceneRecents[groupKey] = recents;
        }

        var existing = recents.FirstOrDefault(r => r.SceneId == sceneKey);
        if (existing != null)
        {
            existing.ActivatedAt = DateTime.UtcNow;
            existing.ActivationCount++;
        }
        else
        {
            recents.Add(new SceneActivation
            {
                SceneId = sceneKey,
                ActivatedAt = DateTime.UtcNow,
                ActivationCount = 1
            });
        }

        // Cap at 20 recents per room
        if (recents.Count > 20)
        {
            var oldest = recents.OrderBy(r => r.ActivatedAt).First();
            recents.Remove(oldest);
        }
    }

    private static string ToHex((byte R, byte G, byte B) color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

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
