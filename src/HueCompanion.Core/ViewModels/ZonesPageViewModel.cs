using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HueCompanion.Core.Models;
using HueCompanion.Core.Services.Interfaces;
using HueCompanion.Core.Utilities;
using static HueCompanion.Core.Services.Interfaces.UIDispatcher;

namespace HueCompanion.Core.ViewModels;

/// <summary>
/// ViewModel for the redesigned Zones page with section layout.
/// </summary>
public partial class ZonesPageViewModel : ObservableObject, IDisposable
{
    private readonly IMultiBridgeService _multiBridgeService;

    [ObservableProperty]
    private ObservableCollection<ZoneSectionViewModel> _zoneSections = new();

    [ObservableProperty]
    private ZoneSectionViewModel? _unassignedSection;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isEditMode;

    [ObservableProperty]
    private bool _hasZones;

    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>
    /// Event raised when a light is tapped for navigation.
    /// </summary>
    public event EventHandler<(Guid LightId, string? BridgeId)>? LightTapped;

    /// <summary>
    /// Event raised when a zone header is tapped for navigation.
    /// </summary>
    public event EventHandler<(Guid ZoneId, LightGroupType Type, bool IsOn, List<(byte R, byte G, byte B)> Colors)>? ZoneTapped;

    public ZonesPageViewModel(IMultiBridgeService multiBridgeService)
    {
        _multiBridgeService = multiBridgeService;
        _multiBridgeService.RoomsOrZonesChanged += OnRoomsOrZonesChanged;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            // Fetch all rooms to build lightId → roomName map
            var roomsResult = await _multiBridgeService.GetAllRoomsAsync();
            var lightToRoom = new Dictionary<Guid, string>();
            if (roomsResult.IsSuccess && roomsResult.Value != null)
            {
                foreach (var room in roomsResult.Value)
                {
                    foreach (var light in room.Lights)
                    {
                        lightToRoom[light.Id] = room.Name;
                    }
                }
            }

            // Fetch all zones
            var zonesResult = await _multiBridgeService.GetAllZonesAsync();
            if (zonesResult.IsFailure)
            {
                ErrorMessage = zonesResult.Error;
                HasZones = false;
                return;
            }

            var zones = zonesResult.Value ?? [];
            var zoneLightIds = new HashSet<Guid>();

            var sections = new ObservableCollection<ZoneSectionViewModel>();
            foreach (var zone in zones)
            {
                var section = new ZoneSectionViewModel(zone, lightToRoom);
                section.ZoneTapped += (s, args) => ZoneTapped?.Invoke(this, args);
                sections.Add(section);

                foreach (var light in zone.Lights)
                    zoneLightIds.Add(light.Id);
            }

            // Fetch all lights to find unassigned ones
            var allLightsResult = await _multiBridgeService.GetAllLightsAsync();
            ZoneSectionViewModel? unassigned = null;
            if (allLightsResult.IsSuccess && allLightsResult.Value != null)
            {
                var unassignedLights = allLightsResult.Value
                    .Where(l => !zoneLightIds.Contains(l.Id))
                    .ToList();

                if (unassignedLights.Count > 0)
                {
                    // Populate room names
                    foreach (var light in unassignedLights)
                    {
                        if (lightToRoom.TryGetValue(light.Id, out var roomName))
                            light.RoomName = roomName;
                    }

                    unassigned = new ZoneSectionViewModel(unassignedLights, lightToRoom);
                }
            }

            RunOnUIThread(() =>
            {
                ZoneSections = sections;
                UnassignedSection = unassigned;
                HasZones = sections.Count > 0 || unassigned != null;
            });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load zones: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task AddLightToZoneAsync(string bridgeId, Guid zoneId, Guid lightId)
    {
        var result = await _multiBridgeService.AddLightToZoneAsync(bridgeId, zoneId, lightId);
        if (result.IsFailure)
            ErrorMessage = result.Error;
    }

    public async Task RemoveLightFromZoneAsync(string bridgeId, Guid zoneId, Guid lightId)
    {
        var result = await _multiBridgeService.RemoveLightFromZoneAsync(bridgeId, zoneId, lightId);
        if (result.IsFailure)
            ErrorMessage = result.Error;
    }

    private async void OnRoomsOrZonesChanged(object? sender, EventArgs e)
    {
        await LoadAsync();
    }

    public void Dispose()
    {
        _multiBridgeService.RoomsOrZonesChanged -= OnRoomsOrZonesChanged;
    }
}

/// <summary>
/// ViewModel for a zone section (or the "Unassigned" pseudo-section).
/// </summary>
public partial class ZoneSectionViewModel : ObservableObject
{
    public Guid? ZoneId { get; }
    public string Name { get; }
    public string IconGlyph { get; }
    public string? BridgeId { get; }
    public bool IsUnassigned { get; }
    public ObservableCollection<ZoneLightItemViewModel> Lights { get; } = new();

    [ObservableProperty]
    private bool _isOn;

    [ObservableProperty]
    private int _lightCount;

    public event EventHandler<(Guid ZoneId, LightGroupType Type, bool IsOn, List<(byte R, byte G, byte B)> Colors)>? ZoneTapped;

    /// <summary>
    /// Creates a section from a zone model.
    /// </summary>
    public ZoneSectionViewModel(RoomModel zone, Dictionary<Guid, string> lightToRoom)
    {
        ZoneId = zone.Id;
        Name = zone.Name;
        BridgeId = zone.BridgeId;
        IconGlyph = RoomIconHelper.GetIconForArchetype(zone.Archetype);
        IsUnassigned = false;
        _isOn = zone.IsOn;
        _lightCount = zone.Lights.Count;

        foreach (var light in zone.Lights)
        {
            if (lightToRoom.TryGetValue(light.Id, out var roomName))
                light.RoomName = roomName;

            Lights.Add(new ZoneLightItemViewModel(light));
        }
    }

    /// <summary>
    /// Creates the "Unassigned Lights" pseudo-section.
    /// </summary>
    public ZoneSectionViewModel(List<LightModel> unassignedLights, Dictionary<Guid, string> lightToRoom)
    {
        ZoneId = null;
        Name = "Unassigned Lights";
        IconGlyph = "\uE8CB"; // Warning icon
        BridgeId = null;
        IsUnassigned = true;
        _isOn = false;
        _lightCount = unassignedLights.Count;

        foreach (var light in unassignedLights)
        {
            Lights.Add(new ZoneLightItemViewModel(light));
        }
    }

    [RelayCommand]
    private void TapZone()
    {
        if (ZoneId.HasValue)
        {
            var colors = Lights
                .Where(l => l.IsOn && l.ColorRgb.HasValue)
                .Select(l => l.ColorRgb!.Value)
                .ToList();
            ZoneTapped?.Invoke(this, (ZoneId.Value, LightGroupType.Zone, IsOn, colors));
        }
    }
}

/// <summary>
/// ViewModel for a single light item in a zone section.
/// </summary>
public class ZoneLightItemViewModel : ObservableObject
{
    public Guid LightId { get; }
    public Guid? DeviceId { get; }
    public string Name { get; }
    public string? RoomName { get; }
    public bool IsOn { get; }
    public HueColor? CurrentColor { get; }
    public (byte R, byte G, byte B)? ColorRgb { get; }

    public ZoneLightItemViewModel(LightModel light)
    {
        LightId = light.Id;
        DeviceId = light.DeviceId;
        Name = light.Name;
        RoomName = light.RoomName;
        IsOn = light.IsOn;
        CurrentColor = light.CurrentColor;

        if (light.CurrentColor != null && light.IsOn)
        {
            var (r, g, b) = light.CurrentColor.ToRgb(light.Brightness);
            ColorRgb = (r, g, b);
        }
    }
}
