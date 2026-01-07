using HueApi;
using HueApi.Models;
using HueApi.Models.Requests;
using HueApi.Models.Responses;
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
    public event EventHandler? Connected;

    /// <inheritdoc/>
    public event EventHandler? Disconnected;

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

            Connected?.Invoke(this, EventArgs.Empty);

            // Start listening for real-time updates
            await StartEventStreamAsync();

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
        Disconnected?.Invoke(this, EventArgs.Empty);
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
        // Determine the current color - prefer xy color, fall back to color temperature
        HueColor? currentColor = null;
        if (lightData.Color?.Xy != null)
        {
            currentColor = new HueColor(lightData.Color.Xy.X, lightData.Color.Xy.Y);
        }
        else if (lightData.ColorTemperature?.Mirek != null)
        {
            // Derive color from color temperature for white/ambiance bulbs
            currentColor = HueColor.FromMirek((int)lightData.ColorTemperature.Mirek);
        }

        return new LightModel
        {
            Id = lightData.Id,
            Name = lightData.Metadata?.Name ?? "Unknown Light",
            IsOn = lightData.On?.IsOn ?? false,
            Brightness = (lightData.Dimming?.Brightness ?? 0) / 100.0,
            SupportsColor = lightData.Color != null,
            SupportsColorTemperature = lightData.ColorTemperature != null,
            CurrentColor = currentColor,
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
    public async Task<IReadOnlyList<RoomModel>> GetZonesAsync()
    {
        if (_hueApi == null) return Array.Empty<RoomModel>();

        var zones = await _hueApi.GetZonesAsync();
        if (zones?.Data == null) return Array.Empty<RoomModel>();

        var allLights = await _hueApi.GetLightsAsync();
        var result = new List<RoomModel>();

        foreach (var zone in zones.Data)
        {
            var zoneModel = new RoomModel
            {
                Id = zone.Id,
                Name = zone.Metadata?.Name ?? "Unknown Zone",
                GroupType = LightGroupType.Zone,
                Archetype = MapRoomArchetype(zone.Metadata?.Archetype),
            };

            // Get light IDs that belong to this zone
            var zoneLightIds = new HashSet<Guid>();
            if (zone.Children != null)
            {
                foreach (var child in zone.Children)
                {
                    if (child.Rtype == "light")
                    {
                        zoneLightIds.Add(child.Rid);
                    }
                }
            }

            // Get lights that are in this zone
            if (allLights?.Data != null && zoneLightIds.Count > 0)
            {
                foreach (var light in allLights.Data)
                {
                    if (zoneLightIds.Contains(light.Id))
                    {
                        zoneModel.Lights.Add(MapLightData(light));
                    }
                }
            }

            // Calculate zone state from lights
            if (zoneModel.Lights.Count > 0)
            {
                zoneModel.IsOn = zoneModel.Lights.Any(l => l.IsOn);
                zoneModel.Brightness = zoneModel.Lights.Where(l => l.IsOn).DefaultIfEmpty()
                    .Average(l => l?.Brightness ?? 0);

                // Get dominant color from first colored light that's on
                var coloredLight = zoneModel.Lights.FirstOrDefault(l => l.IsOn && l.CurrentColor != null);
                zoneModel.DominantColor = coloredLight?.CurrentColor;
            }

            result.Add(zoneModel);
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<RoomModel?> GetZoneAsync(Guid zoneId)
    {
        if (_hueApi == null) return null;

        var zones = await GetZonesAsync();
        return zones.FirstOrDefault(z => z.Id == zoneId);
    }

    /// <inheritdoc/>
    public async Task SetZoneOnAsync(Guid zoneId, bool isOn)
    {
        if (_hueApi == null) return;

        var zone = await _hueApi.GetZoneAsync(zoneId);
        if (zone?.Data == null || zone.Data.Count == 0) return;

        // Get the grouped_light service for this zone
        var groupedLightId = zone.Data[0].Services?
            .FirstOrDefault(s => s.Rtype == "grouped_light")?.Rid;

        if (groupedLightId.HasValue)
        {
            var command = new UpdateGroupedLight { On = new HueApi.Models.On { IsOn = isOn } };
            await _hueApi.UpdateGroupedLightAsync(groupedLightId.Value, command);
        }
    }

    /// <inheritdoc/>
    public async Task SetZoneBrightnessAsync(Guid zoneId, double brightness)
    {
        if (_hueApi == null) return;

        var zone = await _hueApi.GetZoneAsync(zoneId);
        if (zone?.Data == null || zone.Data.Count == 0) return;

        var groupedLightId = zone.Data[0].Services?
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
    public async Task<IReadOnlyList<SceneModel>> GetScenesForZoneAsync(Guid zoneId)
    {
        if (_hueApi == null) return Array.Empty<SceneModel>();

        var scenes = await _hueApi.GetScenesAsync();
        var result = new List<SceneModel>();

        foreach (var scene in scenes.Data)
        {
            // Filter scenes that belong to this zone
            if (scene.Group?.Rid == zoneId)
            {
                result.Add(new SceneModel
                {
                    Id = scene.Id,
                    Name = scene.Metadata?.Name ?? "Unknown Scene",
                    RoomId = zoneId,
                });
            }
        }

        return result;
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
        if (_hueApi == null) return Task.CompletedTask;

        // Stop any existing stream
        StopEventStream();

        _eventStreamCts = new CancellationTokenSource();

        // Subscribe to the event stream
        _hueApi.OnEventStreamMessage += OnEventStreamMessage;
        _hueApi.StartEventStream();

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void StopEventStream()
    {
        if (_hueApi != null)
        {
            _hueApi.OnEventStreamMessage -= OnEventStreamMessage;
            _hueApi.StopEventStream();
        }

        _eventStreamCts?.Cancel();
        _eventStreamCts?.Dispose();
        _eventStreamCts = null;
    }

    private void OnEventStreamMessage(string bridgeIp, List<EventStreamResponse> events)
    {
        foreach (var eventResponse in events)
        {
            foreach (var data in eventResponse.Data)
            {
                // Check if this is a light update event
                if (data.Type == "light")
                {
                    var args = ParseLightStateFromEventData(data);
                    if (args != null)
                    {
                        LightStateChanged?.Invoke(this, args);
                    }
                }
            }
        }
    }

    private LightStateChangedEventArgs? ParseLightStateFromEventData(HueResource data)
    {
        bool? isOn = null;
        double? brightness = null;
        HueColor? color = null;

        // Parse ExtensionData for light state properties
        if (data.ExtensionData != null)
        {
            // Parse "on" property
            if (data.ExtensionData.TryGetValue("on", out var onElement))
            {
                if (onElement.TryGetProperty("on", out var onValue))
                {
                    isOn = onValue.GetBoolean();
                }
            }

            // Parse "dimming" property
            if (data.ExtensionData.TryGetValue("dimming", out var dimmingElement))
            {
                if (dimmingElement.TryGetProperty("brightness", out var brightnessValue))
                {
                    brightness = brightnessValue.GetDouble() / 100.0;
                }
            }

            // Parse "color" property (xy coordinates)
            if (data.ExtensionData.TryGetValue("color", out var colorElement))
            {
                if (colorElement.TryGetProperty("xy", out var xyElement))
                {
                    if (xyElement.TryGetProperty("x", out var xValue) &&
                        xyElement.TryGetProperty("y", out var yValue))
                    {
                        color = new HueColor(xValue.GetDouble(), yValue.GetDouble());
                    }
                }
            }

            // Parse "color_temperature" property (mirek)
            if (color == null && data.ExtensionData.TryGetValue("color_temperature", out var ctElement))
            {
                if (ctElement.TryGetProperty("mirek", out var mirekValue) && mirekValue.ValueKind == System.Text.Json.JsonValueKind.Number)
                {
                    color = HueColor.FromMirek(mirekValue.GetInt32());
                }
            }
        }

        // Only create event args if we have some state to report
        if (isOn == null && brightness == null && color == null)
        {
            return null;
        }

        return new LightStateChangedEventArgs
        {
            LightId = data.Id,
            IsOn = isOn,
            Brightness = brightness,
            Color = color
        };
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
            "home" => RoomArchetype.Home,
            "downstairs" => RoomArchetype.Downstairs,
            "upstairs" => RoomArchetype.Upstairs,
            "top_floor" => RoomArchetype.TopFloor,
            "attic" => RoomArchetype.Attic,
            "guest_room" => RoomArchetype.GuestRoom,
            "staircase" => RoomArchetype.Staircase,
            "lounge" => RoomArchetype.Lounge,
            "man_cave" => RoomArchetype.ManCave,
            "computer" => RoomArchetype.Computer,
            "studio" => RoomArchetype.Studio,
            "music" => RoomArchetype.Music,
            "tv" => RoomArchetype.TV,
            "reading" => RoomArchetype.Reading,
            "closet" => RoomArchetype.Closet,
            "storage" => RoomArchetype.Storage,
            "laundry_room" => RoomArchetype.LaundryRoom,
            "balcony" => RoomArchetype.Balcony,
            "porch" => RoomArchetype.Porch,
            "barbecue" => RoomArchetype.Barbecue,
            "pool" => RoomArchetype.Pool,
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
