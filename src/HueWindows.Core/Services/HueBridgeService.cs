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
    private string? _lastIpAddress;
    private string? _lastAppKey;

    /// <inheritdoc/>
    public bool IsConnected => _hueApi != null;

    /// <inheritdoc/>
    public event EventHandler<LightStateChangedEventArgs>? LightStateChanged;

    /// <inheritdoc/>
    public event EventHandler? Connected;

    /// <inheritdoc/>
    public event EventHandler? Disconnected;

    /// <inheritdoc/>
    public async Task<Result> ConnectAsync(string ipAddress, string appKey)
    {
        try
        {
            // Store credentials for potential reconnection
            _lastIpAddress = ipAddress;
            _lastAppKey = appKey;

            _hueApi = new LocalHueApi(ipAddress, appKey);

            // Validate connection by fetching bridge info
            var bridge = await _hueApi.GetBridgeAsync();
            if (bridge?.Data == null || bridge.Data.Count == 0)
            {
                _hueApi = null;
                return Result.Failure("Could not retrieve bridge information. Please check the IP address and app key.");
            }

            Connected?.Invoke(this, EventArgs.Empty);

            // Start listening for real-time updates
            await StartEventStreamAsync();

            return Result.Success();
        }
        catch (HttpRequestException ex)
        {
            _hueApi = null;
            return Result.Failure($"Network error: Could not reach the bridge at {ipAddress}. {ex.Message}");
        }
        catch (Exception ex)
        {
            _hueApi = null;
            return Result.Failure($"Connection failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Ensures the API connection is valid, reconnecting if necessary.
    /// </summary>
    private async Task<bool> EnsureConnectedAsync()
    {
        if (_hueApi != null)
        {
            try
            {
                // Quick validation - if this throws, we need to reconnect
                await _hueApi.GetBridgeAsync();
                return true;
            }
            catch (ObjectDisposedException)
            {
                // API was disposed, need to reconnect
                _hueApi = null;
            }
            catch (HttpRequestException)
            {
                // Network error, might need to reconnect
                _hueApi = null;
            }
        }

        // Try to reconnect if we have credentials
        if (_lastIpAddress != null && _lastAppKey != null)
        {
            return await ConnectAsync(_lastIpAddress, _lastAppKey);
        }

        return false;
    }

    /// <inheritdoc/>
    public void Disconnect()
    {
        StopEventStream();
        _hueApi = null;
        Disconnected?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<RoomModel>>> GetRoomsAsync()
    {
        if (!await EnsureConnectedAsync())
            return Result<IReadOnlyList<RoomModel>>.Failure("Not connected to bridge. Please check your connection.");

        try
        {
            var rooms = await _hueApi!.Room.GetAllAsync();
            if (rooms?.Data == null)
                return Result<IReadOnlyList<RoomModel>>.Failure("Failed to retrieve rooms from the bridge.");

            var allLights = await _hueApi.Light.GetAllAsync();
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

            return Result<IReadOnlyList<RoomModel>>.Success(result);
        }
        catch (HttpRequestException ex)
        {
            return Result<IReadOnlyList<RoomModel>>.Failure($"Network error while fetching rooms: {ex.Message}");
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<RoomModel>>.Failure($"Failed to load rooms: {ex.Message}");
        }
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
    public async Task<Result<RoomModel>> GetRoomAsync(Guid roomId)
    {
        var roomsResult = await GetRoomsAsync();
        if (roomsResult.IsFailure)
            return Result<RoomModel>.Failure(roomsResult.Error!);

        var room = roomsResult.Value!.FirstOrDefault(r => r.Id == roomId);
        if (room == null)
            return Result<RoomModel>.Failure($"Room with ID {roomId} not found.");

        return Result<RoomModel>.Success(room);
    }

    /// <inheritdoc/>
    public async Task SetRoomOnAsync(Guid roomId, bool isOn)
    {
        if (_hueApi == null) return;

        var room = await _hueApi.Room.GetByIdAsync(roomId);
        if (room?.Data == null || room.Data.Count == 0) return;

        // Get the grouped_light service for this room
        var groupedLightId = room.Data[0].Services?
            .FirstOrDefault(s => s.Rtype == "grouped_light")?.Rid;

        if (groupedLightId.HasValue)
        {
            var command = new UpdateGroupedLight { On = new HueApi.Models.On { IsOn = isOn } };
            await _hueApi.GroupedLight.UpdateAsync(groupedLightId.Value, command);
        }
    }

    /// <inheritdoc/>
    public async Task SetRoomBrightnessAsync(Guid roomId, double brightness)
    {
        if (_hueApi == null) return;

        var room = await _hueApi.Room.GetByIdAsync(roomId);
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
            await _hueApi.GroupedLight.UpdateAsync(groupedLightId.Value, command);
        }
    }

    /// <inheritdoc/>
    public async Task SetRoomColorAsync(Guid roomId, HueColor color)
    {
        if (_hueApi == null) return;

        var room = await _hueApi.Room.GetByIdAsync(roomId);
        if (room?.Data == null || room.Data.Count == 0) return;

        var groupedLightId = room.Data[0].Services?
            .FirstOrDefault(s => s.Rtype == "grouped_light")?.Rid;

        if (groupedLightId.HasValue)
        {
            var command = new UpdateGroupedLight
            {
                Color = new HueApi.Models.Color
                {
                    Xy = new HueApi.Models.XyPosition { X = color.X, Y = color.Y }
                }
            };
            await _hueApi.GroupedLight.UpdateAsync(groupedLightId.Value, command);
        }
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<RoomModel>>> GetZonesAsync()
    {
        if (!await EnsureConnectedAsync())
            return Result<IReadOnlyList<RoomModel>>.Failure("Not connected to bridge. Please check your connection.");

        try
        {
            var zones = await _hueApi!.Zone.GetAllAsync();
            if (zones?.Data == null)
                return Result<IReadOnlyList<RoomModel>>.Failure("Failed to retrieve zones from the bridge.");

            var allLights = await _hueApi.Light.GetAllAsync();
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

            return Result<IReadOnlyList<RoomModel>>.Success(result);
        }
        catch (HttpRequestException ex)
        {
            return Result<IReadOnlyList<RoomModel>>.Failure($"Network error while fetching zones: {ex.Message}");
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<RoomModel>>.Failure($"Failed to load zones: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<RoomModel>> GetZoneAsync(Guid zoneId)
    {
        var zonesResult = await GetZonesAsync();
        if (zonesResult.IsFailure)
            return Result<RoomModel>.Failure(zonesResult.Error!);

        var zone = zonesResult.Value!.FirstOrDefault(z => z.Id == zoneId);
        if (zone == null)
            return Result<RoomModel>.Failure($"Zone with ID {zoneId} not found.");

        return Result<RoomModel>.Success(zone);
    }

    /// <inheritdoc/>
    public async Task SetZoneOnAsync(Guid zoneId, bool isOn)
    {
        if (_hueApi == null) return;

        var zone = await _hueApi.Zone.GetByIdAsync(zoneId);
        if (zone?.Data == null || zone.Data.Count == 0) return;

        // Get the grouped_light service for this zone
        var groupedLightId = zone.Data[0].Services?
            .FirstOrDefault(s => s.Rtype == "grouped_light")?.Rid;

        if (groupedLightId.HasValue)
        {
            var command = new UpdateGroupedLight { On = new HueApi.Models.On { IsOn = isOn } };
            await _hueApi.GroupedLight.UpdateAsync(groupedLightId.Value, command);
        }
    }

    /// <inheritdoc/>
    public async Task SetZoneBrightnessAsync(Guid zoneId, double brightness)
    {
        if (_hueApi == null) return;

        var zone = await _hueApi.Zone.GetByIdAsync(zoneId);
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
            await _hueApi.GroupedLight.UpdateAsync(groupedLightId.Value, command);
        }
    }

    /// <inheritdoc/>
    public async Task SetZoneColorAsync(Guid zoneId, HueColor color)
    {
        if (_hueApi == null) return;

        var zone = await _hueApi.Zone.GetByIdAsync(zoneId);
        if (zone?.Data == null || zone.Data.Count == 0) return;

        var groupedLightId = zone.Data[0].Services?
            .FirstOrDefault(s => s.Rtype == "grouped_light")?.Rid;

        if (groupedLightId.HasValue)
        {
            var command = new UpdateGroupedLight
            {
                Color = new HueApi.Models.Color
                {
                    Xy = new HueApi.Models.XyPosition { X = color.X, Y = color.Y }
                }
            };
            await _hueApi.GroupedLight.UpdateAsync(groupedLightId.Value, command);
        }
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<SceneModel>>> GetScenesForZoneAsync(Guid zoneId)
    {
        if (_hueApi == null)
            return Result<IReadOnlyList<SceneModel>>.Failure("Not connected to bridge.");

        try
        {
            var scenes = await _hueApi.Scene.GetAllAsync();
            var result = new List<SceneModel>();

            foreach (var scene in scenes.Data)
            {
                // Filter scenes that belong to this zone
                if (scene.Group?.Rid == zoneId)
                {
                    var sceneModel = new SceneModel
                    {
                        Id = scene.Id,
                        Name = scene.Metadata?.Name ?? "Unknown Scene",
                        RoomId = zoneId,
                        PaletteColors = ExtractPaletteColors(scene)
                    };
                    sceneModel.PreviewColor = sceneModel.PaletteColors.FirstOrDefault();
                    result.Add(sceneModel);
                }
            }

            return Result<IReadOnlyList<SceneModel>>.Success(result);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<SceneModel>>.Failure($"Failed to load scenes: {ex.Message}");
        }
    }

    /// <summary>
    /// Extracts palette colors from a scene's palette property.
    /// </summary>
    private static List<Models.HueColor> ExtractPaletteColors(Scene scene)
    {
        var colors = new List<Models.HueColor>();

        // Try to get colors from the palette
        if (scene.Palette?.Color != null)
        {
            foreach (var colorPalette in scene.Palette.Color.Take(4)) // Limit to 4 colors for UI
            {
                if (colorPalette.Color?.Xy != null)
                {
                    colors.Add(new Models.HueColor(colorPalette.Color.Xy.X, colorPalette.Color.Xy.Y));
                }
            }
        }

        // If no palette colors, try to extract from actions
        if (colors.Count == 0 && scene.Actions != null)
        {
            foreach (var action in scene.Actions.Take(4))
            {
                if (action.Action?.Color?.Xy != null)
                {
                    colors.Add(new Models.HueColor(action.Action.Color.Xy.X, action.Action.Color.Xy.Y));
                }
            }
        }

        return colors;
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<LightModel>>> GetLightsInRoomAsync(Guid roomId)
    {
        var roomResult = await GetRoomAsync(roomId);
        if (roomResult.IsFailure)
            return Result<IReadOnlyList<LightModel>>.Failure(roomResult.Error!);

        return Result<IReadOnlyList<LightModel>>.Success(roomResult.Value!.Lights);
    }

    /// <inheritdoc/>
    public async Task<Result<LightModel>> GetLightAsync(Guid lightId)
    {
        if (_hueApi == null)
            return Result<LightModel>.Failure("Not connected to bridge.");

        try
        {
            var light = await _hueApi.Light.GetByIdAsync(lightId);
            if (light?.Data == null || light.Data.Count == 0)
                return Result<LightModel>.Failure($"Light with ID {lightId} not found.");

            return Result<LightModel>.Success(MapLightData(light.Data[0]));
        }
        catch (Exception ex)
        {
            return Result<LightModel>.Failure($"Failed to load light: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task SetLightOnAsync(Guid lightId, bool isOn)
    {
        if (_hueApi == null) return;

        var command = new UpdateLight { On = new HueApi.Models.On { IsOn = isOn } };
        await _hueApi.Light.UpdateAsync(lightId, command);
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
        await _hueApi.Light.UpdateAsync(lightId, command);
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
        await _hueApi.Light.UpdateAsync(lightId, command);
    }

    /// <inheritdoc/>
    public async Task SetLightColorAndBrightnessAsync(Guid lightId, HueColor color, double brightness)
    {
        if (_hueApi == null) return;

        var command = new UpdateLight
        {
            On = new HueApi.Models.On { IsOn = brightness > 0 },
            Dimming = new HueApi.Models.Dimming { Brightness = brightness * 100 },
            Color = new HueApi.Models.Color
            {
                Xy = new HueApi.Models.XyPosition { X = color.X, Y = color.Y }
            }
        };
        await _hueApi.Light.UpdateAsync(lightId, command);
    }

    /// <inheritdoc/>
    public async Task SetLightTemperatureAsync(Guid lightId, int mirek)
    {
        if (_hueApi == null) return;

        var command = new UpdateLight
        {
            ColorTemperature = new HueApi.Models.ColorTemperature { Mirek = mirek }
        };
        await _hueApi.Light.UpdateAsync(lightId, command);
    }

    /// <inheritdoc/>
    public async Task ApplyEffectAsync(Guid lightId, string effect, double? speed = null, double? brightness = null)
    {
        if (_hueApi == null) return;

        // Map string effect names to HueApi Effect enum
        var effectEnum = effect.ToLowerInvariant() switch
        {
            "fire" => Effect.fire,
            "candle" => Effect.candle,
            "sparkle" => Effect.sparkle,
            "glisten" => Effect.glisten,
            "opal" => Effect.opal,
            "prism" => Effect.prism,
            "underwater" => Effect.underwater,
            "cosmos" => Effect.cosmos,
            "sunbeam" => Effect.sunbeam,
            "enchant" => Effect.enchant,
            "none" => Effect.no_effect,
            _ => Effect.no_effect
        };

        var command = new UpdateLight
        {
            Effects = new HueApi.Models.Effects { Effect = effectEnum }
        };

        // Add brightness if specified
        if (brightness.HasValue)
        {
            command.Dimming = new HueApi.Models.Dimming
            {
                Brightness = brightness.Value * 100 // API expects 0-100
            };
        }

        // Note: Speed may require EffectsV2 API - check HueApi support for future enhancement

        await _hueApi.Light.UpdateAsync(lightId, command);
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<SceneModel>>> GetScenesForRoomAsync(Guid roomId)
    {
        if (_hueApi == null)
            return Result<IReadOnlyList<SceneModel>>.Failure("Not connected to bridge.");

        try
        {
            var scenes = await _hueApi.Scene.GetAllAsync();
            var result = new List<SceneModel>();

            foreach (var scene in scenes.Data)
            {
                // Filter scenes that belong to this room
                if (scene.Group?.Rid == roomId)
                {
                    var sceneModel = new SceneModel
                    {
                        Id = scene.Id,
                        Name = scene.Metadata?.Name ?? "Unknown Scene",
                        RoomId = roomId,
                        PaletteColors = ExtractPaletteColors(scene)
                    };
                    sceneModel.PreviewColor = sceneModel.PaletteColors.FirstOrDefault();
                    result.Add(sceneModel);
                }
            }

            return Result<IReadOnlyList<SceneModel>>.Success(result);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<SceneModel>>.Failure($"Failed to load scenes: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task ActivateSceneAsync(Guid sceneId)
    {
        if (_hueApi == null) return;

        // Recall the scene to activate it
        var command = new UpdateScene
        {
            Recall = new Recall { Action = SceneRecallAction.active }
        };
        await _hueApi.Scene.UpdateAsync(sceneId, command);
    }

    /// <inheritdoc/>
    public async Task<Result<Guid>> CreateSceneFromCurrentStateAsync(Guid groupId, string sceneName, bool isZone = false)
    {
        if (_hueApi == null)
            return Result<Guid>.Failure("Not connected to bridge.");

        try
        {
            // Get current light states from the room/zone
            var groupResult = isZone
                ? await GetZoneAsync(groupId)
                : await GetRoomAsync(groupId);

            if (!groupResult.IsSuccess || groupResult.Value == null)
                return Result<Guid>.Failure(groupResult.Error ?? "Failed to get room/zone.");

            var lights = groupResult.Value.Lights;
            if (lights == null || lights.Count == 0)
                return Result<Guid>.Failure("No lights found in room/zone.");

            // Build scene actions from current light states
            var actions = new List<SceneAction>();
            foreach (var light in lights)
            {
                var lightAction = new LightAction
                {
                    On = new On { IsOn = light.IsOn },
                    Dimming = new Dimming { Brightness = light.Brightness * 100 }
                };

                if (light.CurrentColor != null)
                {
                    lightAction.Color = new HueApi.Models.Color
                    {
                        Xy = new XyPosition { X = light.CurrentColor.X, Y = light.CurrentColor.Y }
                    };
                }

                var action = new SceneAction
                {
                    Target = new ResourceIdentifier { Rid = light.Id, Rtype = "light" },
                    Action = lightAction
                };
                actions.Add(action);
            }

            // Create the scene
            var metadata = new Metadata { Name = sceneName };
            var group = new ResourceIdentifier { Rid = groupId, Rtype = isZone ? "zone" : "room" };
            var createScene = new CreateScene(metadata, group)
            {
                Actions = actions
            };

            var result = await _hueApi.Scene.CreateAsync(createScene);
            var createdId = result?.Data?.FirstOrDefault()?.Rid;

            if (createdId.HasValue)
            {
                return Result<Guid>.Success(createdId.Value);
            }

            return Result<Guid>.Failure("Failed to create scene - no ID returned.");
        }
        catch (Exception ex)
        {
            return Result<Guid>.Failure($"Failed to create scene: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result> DeleteSceneAsync(Guid sceneId)
    {
        if (_hueApi == null)
            return Result.Failure("Not connected to bridge.");

        try
        {
            await _hueApi.Scene.DeleteAsync(sceneId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to delete scene: {ex.Message}");
        }
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
