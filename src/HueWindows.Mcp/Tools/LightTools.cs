using System.ComponentModel;
using System.Text.Json;
using HueWindows.Core.Models;
using HueWindows.Core.Services.Interfaces;
using HueWindows.Mcp.Services;
using ModelContextProtocol.Server;

namespace HueWindows.Mcp.Tools;

[McpServerToolType]
public class LightTools
{
    private readonly IMultiBridgeService _multiBridge;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    public LightTools(IMultiBridgeService multiBridge)
    {
        _multiBridge = multiBridge;
    }

    private async Task<List<RoomModel>> GetAllGroupsAsync()
    {
        var rooms = new List<RoomModel>();
        var roomResult = await _multiBridge.GetAllRoomsAsync();
        if (roomResult.IsSuccess) rooms.AddRange(roomResult.Value!);
        var zoneResult = await _multiBridge.GetAllZonesAsync();
        if (zoneResult.IsSuccess) rooms.AddRange(zoneResult.Value!);
        return rooms;
    }

    [McpServerTool(Name = "hue_list_rooms"), Description("List all rooms and zones with their lights and current states.")]
    public async Task<string> ListRooms(
        [Description("Include individual light states")] bool includeLightDetails = true)
    {
        var groups = await GetAllGroupsAsync();

        var result = groups.Select(r => new
        {
            r.Name,
            Type = r.GroupType.ToString(),
            r.IsOn,
            Brightness = Math.Round(r.Brightness * 100, 1),
            LightCount = r.Lights.Count,
            Lights = includeLightDetails ? r.Lights.Select(l => new
            {
                l.Name,
                l.IsOn,
                Brightness = Math.Round(l.Brightness * 100, 1),
                Color = l.CurrentColor != null ? ColorParser.ToHex(l.CurrentColor, l.Brightness) : null,
                l.ColorTemperature,
                l.IsReachable
            }).ToArray() : null
        });

        return JsonSerializer.Serialize(result, JsonOpts);
    }

    [McpServerTool(Name = "hue_get_light"), Description("Get the current state of a specific light.")]
    public async Task<string> GetLight(
        [Description("Light name or ID")] string light)
    {
        var groups = await GetAllGroupsAsync();
        var found = FuzzyMatcher.FindLight(groups, light);
        if (found == null)
            return JsonSerializer.Serialize(new { error = $"Light '{light}' not found" }, JsonOpts);

        var (l, room) = found.Value;
        return JsonSerializer.Serialize(new
        {
            l.Name,
            Room = room.Name,
            l.IsOn,
            Brightness = Math.Round(l.Brightness * 100, 1),
            Color = l.CurrentColor != null ? ColorParser.ToHex(l.CurrentColor, l.Brightness) : null,
            l.ColorTemperature,
            l.SupportsColor,
            l.SupportsColorTemperature,
            l.IsReachable
        }, JsonOpts);
    }

    [McpServerTool(Name = "hue_set_light"), Description("Set the state of a Hue light by name or ID. Supports on/off, brightness, color, and color temperature.")]
    public async Task<string> SetLight(
        [Description("Light name or Hue resource ID")] string light,
        [Description("Turn light on (true) or off (false)")] bool? on = null,
        [Description("Brightness percentage (0-100)")] double? brightness = null,
        [Description("Color as hex (#FF0000), RGB (rgb(255,0,0)), or named color (red, warm white, etc.)")] string? color = null,
        [Description("Color temperature in mirek (153=cold, 500=warm)")] int? colorTemperatureMirek = null,
        [Description("Transition duration in milliseconds")] int transitionMs = 400)
    {
        var groups = await GetAllGroupsAsync();
        var found = FuzzyMatcher.FindLight(groups, light);
        if (found == null)
            return JsonSerializer.Serialize(new { error = $"Light '{light}' not found" }, JsonOpts);

        var (l, room) = found.Value;
        var bridgeId = room.BridgeId;
        var bridge = bridgeId != null ? _multiBridge.GetBridgeService(bridgeId) : _multiBridge.GetDefaultBridgeService();
        if (bridge == null)
            return JsonSerializer.Serialize(new { error = "No connected bridge" }, JsonOpts);

        int? transition = transitionMs > 0 ? transitionMs : null;

        if (on.HasValue)
            await bridge.SetLightOnAsync(l.Id, on.Value, transition);

        if (brightness.HasValue)
            await bridge.SetLightBrightnessAsync(l.Id, Math.Clamp(brightness.Value / 100.0, 0, 1), transition);

        if (color != null)
        {
            var parsed = ColorParser.Parse(color);
            if (parsed != null)
            {
                if (brightness.HasValue)
                    await bridge.SetLightColorAndBrightnessAsync(l.Id, parsed, Math.Clamp(brightness.Value / 100.0, 0, 1), transition);
                else
                    await bridge.SetLightColorAsync(l.Id, parsed, transition);
            }
        }

        if (colorTemperatureMirek.HasValue)
            await bridge.SetLightTemperatureAsync(l.Id, Math.Clamp(colorTemperatureMirek.Value, 153, 500), transition);

        return JsonSerializer.Serialize(new { success = true, light = l.Name, room = room.Name }, JsonOpts);
    }

    [McpServerTool(Name = "hue_set_room"), Description("Set the state of all lights in a Hue room or zone.")]
    public async Task<string> SetRoom(
        [Description("Room or zone name")] string room,
        [Description("Turn on/off")] bool? on = null,
        [Description("Brightness percentage (0-100)")] double? brightness = null,
        [Description("Color as hex, RGB, or named color")] string? color = null,
        [Description("Color temperature in mirek (153=cold, 500=warm)")] int? colorTemperatureMirek = null,
        [Description("Transition duration in milliseconds")] int transitionMs = 400)
    {
        var groups = await GetAllGroupsAsync();
        var found = FuzzyMatcher.FindRoom(groups, room);
        if (found == null)
            return JsonSerializer.Serialize(new { error = $"Room '{room}' not found" }, JsonOpts);

        var bridgeId = found.BridgeId;
        var bridge = bridgeId != null ? _multiBridge.GetBridgeService(bridgeId) : _multiBridge.GetDefaultBridgeService();
        if (bridge == null)
            return JsonSerializer.Serialize(new { error = "No connected bridge" }, JsonOpts);

        int? transition = transitionMs > 0 ? transitionMs : null;

        if (on.HasValue)
            await bridge.SetRoomOnAsync(found.Id, on.Value, transition);

        if (brightness.HasValue)
            await bridge.SetRoomBrightnessAsync(found.Id, Math.Clamp(brightness.Value / 100.0, 0, 1), transition);

        if (color != null)
        {
            var parsed = ColorParser.Parse(color);
            if (parsed != null)
                await bridge.SetRoomColorAsync(found.Id, parsed, transition);
        }

        if (colorTemperatureMirek.HasValue)
        {
            // Set temperature on each light individually (no room-level temp API)
            foreach (var l in found.Lights.Where(l => l.SupportsColorTemperature))
                await bridge.SetLightTemperatureAsync(l.Id, Math.Clamp(colorTemperatureMirek.Value, 153, 500), transition);
        }

        return JsonSerializer.Serialize(new
        {
            success = true,
            room = found.Name,
            lightsUpdated = found.Lights.Count
        }, JsonOpts);
    }

    [McpServerTool(Name = "hue_turn_off_all"), Description("Turn off all lights on all connected bridges.")]
    public async Task<string> TurnOffAll()
    {
        var groups = await GetAllGroupsAsync();
        int count = 0;
        foreach (var group in groups)
        {
            var bridge = group.BridgeId != null ? _multiBridge.GetBridgeService(group.BridgeId) : _multiBridge.GetDefaultBridgeService();
            if (bridge != null)
            {
                if (group.GroupType == LightGroupType.Zone)
                    await bridge.SetZoneOnAsync(group.Id, false);
                else
                    await bridge.SetRoomOnAsync(group.Id, false);
                count += group.Lights.Count;
            }
        }

        return JsonSerializer.Serialize(new { success = true, lightsAffected = count }, JsonOpts);
    }
}
