using HueApi;
using HueApi.Models;
using HueApi.Models.Requests;
using HueWindows.Core.Models;
using HueWindows.Core.Services.Interfaces;

namespace HueWindows.Core.Services;

/// <summary>
/// Service for communicating with a connected Hue bridge using the HueApi library.
/// </summary>
public class HueBridgeService : IHueBridgeService
{
    private LocalHueApi? _hueApi;
    private CancellationTokenSource? _eventStreamCts;

    /// <inheritdoc/>
    public bool IsConnected => _hueApi != null;

    /// <inheritdoc/>
    public event EventHandler<LightStateChangedEventArgs>? LightStateChanged;

    /// <inheritdoc/>
    public async Task<bool> ConnectAsync(string ipAddress, string appKey)
    {
        try
        {
            _hueApi = new LocalHueApi(ipAddress, appKey);

            // Validate connection by fetching bridge info
            var bridge = await _hueApi.GetBridgeAsync();
            if (bridge?.Data == null || bridge.Data.Count == 0)
            {
                _hueApi = null;
                return false;
            }

            return true;
        }
        catch
        {
            _hueApi = null;
            return false;
        }
    }

    /// <inheritdoc/>
    public void Disconnect()
    {
        StopEventStream();
        _hueApi = null;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<RoomModel>> GetRoomsAsync()
    {
        if (_hueApi == null) return Array.Empty<RoomModel>();

        var rooms = await _hueApi.GetRoomsAsync();
        if (rooms?.Data == null) return Array.Empty<RoomModel>();

        var allLights = await _hueApi.GetLightsAsync();
        var result = new List<RoomModel>();

        foreach (var room in rooms.Data)
        {
            var roomModel = new RoomModel
            {
                Id = room.Id,
                Name = room.Metadata?.Name ?? "Unknown Room",
                Archetype = MapRoomArchetype(room.Metadata?.Archetype),
            };

            // Get device IDs that belong to this room
            var roomDeviceIds = new HashSet<Guid>();
            if (room.Children != null)
            {
                foreach (var child in room.Children)
                {
                    if (child.Rtype == "device")
                    {
                        roomDeviceIds.Add(child.Rid);
                    }
                }
            }

            // Get lights whose owner device is in this room
            if (allLights?.Data != null && roomDeviceIds.Count > 0)
            {
                foreach (var light in allLights.Data)
                {
                    // Light's owner is a device - check if that device is in this room
                    if (light.Owner?.Rid != null && roomDeviceIds.Contains(light.Owner.Rid))
                    {
                        roomModel.Lights.Add(MapLightData(light));
                    }
                }
            }

            // Calculate room state from lights
            if (roomModel.Lights.Count > 0)
            {
                roomModel.IsOn = roomModel.Lights.Any(l => l.IsOn);
                roomModel.Brightness = roomModel.Lights.Where(l => l.IsOn).DefaultIfEmpty()
                    .Average(l => l?.Brightness ?? 0);

                // Get dominant color from first colored light that's on
                var coloredLight = roomModel.Lights.FirstOrDefault(l => l.IsOn && l.CurrentColor != null);
                roomModel.DominantColor = coloredLight?.CurrentColor;
            }

            result.Add(roomModel);
        }

        return result;
    }

    private static LightModel MapLightData(HueApi.Models.Light lightData)
    {
        return new LightModel
        {
            Id = lightData.Id,
            Name = lightData.Metadata?.Name ?? "Unknown Light",
            IsOn = lightData.On?.IsOn ?? false,
            Brightness = (lightData.Dimming?.Brightness ?? 0) / 100.0,
            SupportsColor = lightData.Color != null,
            SupportsColorTemperature = lightData.ColorTemperature != null,
            CurrentColor = lightData.Color?.Xy != null
                ? new HueColor(lightData.Color.Xy.X, lightData.Color.Xy.Y)
                : null,
            ColorTemperature = (int?)(lightData.ColorTemperature?.Mirek),
            Archetype = MapLightArchetype(lightData.Metadata?.Archetype)
        };
    }

    /// <inheritdoc/>
    public async Task<RoomModel?> GetRoomAsync(Guid roomId)
    {
        if (_hueApi == null) return null;

        var rooms = await GetRoomsAsync();
        return rooms.FirstOrDefault(r => r.Id == roomId);
    }

    /// <inheritdoc/>
    public async Task SetRoomOnAsync(Guid roomId, bool isOn)
    {
        if (_hueApi == null) return;

        var room = await _hueApi.GetRoomAsync(roomId);
        if (room?.Data == null || room.Data.Count == 0) return;

        // Get the grouped_light service for this room
        var groupedLightId = room.Data[0].Services?
            .FirstOrDefault(s => s.Rtype == "grouped_light")?.Rid;

        if (groupedLightId.HasValue)
        {
            var command = new UpdateGroupedLight { On = new HueApi.Models.On { IsOn = isOn } };
            await _hueApi.UpdateGroupedLightAsync(groupedLightId.Value, command);
        }
    }

    /// <inheritdoc/>
    public async Task SetRoomBrightnessAsync(Guid roomId, double brightness)
    {
        if (_hueApi == null) return;

        var room = await _hueApi.GetRoomAsync(roomId);
        if (room?.Data == null || room.Data.Count == 0) return;

        var groupedLightId = room.Data[0].Services?
            .FirstOrDefault(s => s.Rtype == "grouped_light")?.Rid;

        if (groupedLightId.HasValue)
        {
            var command = new UpdateGroupedLight
            {
                On = new HueApi.Models.On { IsOn = brightness > 0 },
                Dimming = new HueApi.Models.Dimming { Brightness = brightness * 100 }
            };
            await _hueApi.UpdateGroupedLightAsync(groupedLightId.Value, command);
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<LightModel>> GetLightsInRoomAsync(Guid roomId)
    {
        var room = await GetRoomAsync(roomId);
        return room?.Lights ?? new List<LightModel>();
    }

    /// <inheritdoc/>
    public async Task<LightModel?> GetLightAsync(Guid lightId)
    {
        if (_hueApi == null) return null;

        try
        {
            var light = await _hueApi.GetLightAsync(lightId);
            if (light?.Data == null || light.Data.Count == 0) return null;

            return MapLightData(light.Data[0]);
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task SetLightOnAsync(Guid lightId, bool isOn)
    {
        if (_hueApi == null) return;

        var command = new UpdateLight { On = new HueApi.Models.On { IsOn = isOn } };
        await _hueApi.UpdateLightAsync(lightId, command);
    }

    /// <inheritdoc/>
    public async Task SetLightBrightnessAsync(Guid lightId, double brightness)
    {
        if (_hueApi == null) return;

        var command = new UpdateLight
        {
            On = new HueApi.Models.On { IsOn = brightness > 0 },
            Dimming = new HueApi.Models.Dimming { Brightness = brightness * 100 }
        };
        await _hueApi.UpdateLightAsync(lightId, command);
    }

    /// <inheritdoc/>
    public async Task SetLightColorAsync(Guid lightId, HueColor color)
    {
        if (_hueApi == null) return;

        var command = new UpdateLight
        {
            Color = new HueApi.Models.Color
            {
                Xy = new HueApi.Models.XyPosition { X = color.X, Y = color.Y }
            }
        };
        await _hueApi.UpdateLightAsync(lightId, command);
    }

    /// <inheritdoc/>
    public async Task SetLightTemperatureAsync(Guid lightId, int mirek)
    {
        if (_hueApi == null) return;

        var command = new UpdateLight
        {
            ColorTemperature = new HueApi.Models.ColorTemperature { Mirek = mirek }
        };
        await _hueApi.UpdateLightAsync(lightId, command);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SceneModel>> GetScenesForRoomAsync(Guid roomId)
    {
        if (_hueApi == null) return Array.Empty<SceneModel>();

        var scenes = await _hueApi.GetScenesAsync();
        var result = new List<SceneModel>();

        foreach (var scene in scenes.Data)
        {
            // Filter scenes that belong to this room
            if (scene.Group?.Rid == roomId)
            {
                result.Add(new SceneModel
                {
                    Id = scene.Id,
                    Name = scene.Metadata?.Name ?? "Unknown Scene",
                    RoomId = roomId,
                    // TODO: Extract preview color from scene palette if available
                });
            }
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task ActivateSceneAsync(Guid sceneId)
    {
        if (_hueApi == null) return;

        // Recall the scene to activate it
        await _hueApi.RecallSceneAsync(sceneId);
    }

    /// <inheritdoc/>
    public Task StartEventStreamAsync()
    {
        // TODO: Implement event stream when HueApi version supports it
        // For now, we'll rely on polling or manual refresh
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void StopEventStream()
    {
        _eventStreamCts?.Cancel();
        _eventStreamCts?.Dispose();
        _eventStreamCts = null;
    }

    private static RoomArchetype MapRoomArchetype(string? archetype)
    {
        return archetype?.ToLowerInvariant() switch
        {
            "living_room" => RoomArchetype.LivingRoom,
            "kitchen" => RoomArchetype.Kitchen,
            "dining" => RoomArchetype.Dining,
            "bedroom" => RoomArchetype.Bedroom,
            "kids_bedroom" => RoomArchetype.KidsBedroom,
            "bathroom" => RoomArchetype.Bathroom,
            "nursery" => RoomArchetype.Nursery,
            "recreation" => RoomArchetype.Recreation,
            "office" => RoomArchetype.Office,
            "gym" => RoomArchetype.Gym,
            "hallway" => RoomArchetype.Hallway,
            "toilet" => RoomArchetype.Toilet,
            "front_door" => RoomArchetype.FrontDoor,
            "garage" => RoomArchetype.Garage,
            "terrace" => RoomArchetype.Terrace,
            "garden" => RoomArchetype.Garden,
            "driveway" => RoomArchetype.Driveway,
            "carport" => RoomArchetype.Carport,
            "other" => RoomArchetype.Other,
            _ => RoomArchetype.Other
        };
    }

    private static LightArchetype MapLightArchetype(string? archetype)
    {
        return archetype?.ToLowerInvariant() switch
        {
            "classic_bulb" => LightArchetype.ClassicBulb,
            "sultan_bulb" => LightArchetype.SultanBulb,
            "flood_bulb" => LightArchetype.FloodBulb,
            "spot_bulb" => LightArchetype.SpotBulb,
            "candle_bulb" => LightArchetype.CandleBulb,
            "hue_lightstrip" => LightArchetype.HueLightstrip,
            "hue_iris" => LightArchetype.HueIris,
            "hue_bloom" => LightArchetype.HueBloom,
            "hue_go" => LightArchetype.HueGo,
            "hue_play" => LightArchetype.HuePlay,
            _ => LightArchetype.UnknownArchetype
        };
    }
}
