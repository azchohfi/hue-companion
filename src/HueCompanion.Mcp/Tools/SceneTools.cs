using System.ComponentModel;
using System.Text.Json;
using HueCompanion.Core.Models;
using HueCompanion.Core.Services.Interfaces;
using HueCompanion.Mcp.Services;
using ModelContextProtocol.Server;

namespace HueCompanion.Mcp.Tools;

[McpServerToolType]
public class SceneTools
{
    private readonly ISceneStorageService _sceneStorage;
    private readonly IAnimationService _animationService;
    private readonly IMultiBridgeService _multiBridge;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    public SceneTools(ISceneStorageService sceneStorage, IAnimationService animationService, IMultiBridgeService multiBridge)
    {
        _sceneStorage = sceneStorage;
        _animationService = animationService;
        _multiBridge = multiBridge;
    }

    [McpServerTool(Name = "hue_list_scenes"), Description("List all saved scenes (both static and animated).")]
    public async Task<string> ListScenes(
        [Description("Filter: all, static, animated")] string type = "all")
    {
        var allScenes = await _animationService.GetAllScenesAsync();
        if (!allScenes.IsSuccess)
            return JsonSerializer.Serialize(new { error = "Failed to load scenes" }, JsonOpts);

        var filtered = allScenes.Value!.AsEnumerable();

        // Apply type filter
        switch (type.ToLowerInvariant())
        {
            case "static":
                filtered = filtered.Where(s => s.Animations.All(a => a.Type == AnimationType.Keyframe && a.RepeatMode == RepeatMode.Once));
                break;
            case "animated":
                filtered = filtered.Where(s => s.Animations.Any(a => a.Type == AnimationType.Event || a.RepeatMode == RepeatMode.Loop));
                break;
            // "all" — no filter
        }

        var scenes = filtered.Select(s => new
        {
            s.Id,
            s.Name,
            s.Category,
            s.Description,
            s.IsBuiltIn,
            AnimationCount = s.Animations.Count,
            HasKeyframes = s.Animations.Any(a => a.Type == AnimationType.Keyframe),
            HasEvents = s.Animations.Any(a => a.Type == AnimationType.Event),
        });

        return JsonSerializer.Serialize(scenes, JsonOpts);
    }

    [McpServerTool(Name = "hue_activate_scene"), Description("Activate a saved scene by name in a specific room.")]
    public async Task<string> ActivateScene(
        [Description("Scene name or ID")] string scene,
        [Description("Target room or zone name")] string room,
        [Description("Transition duration in milliseconds")] int transitionMs = 1000)
    {
        // Find the scene
        var allScenes = await _animationService.GetAllScenesAsync();
        if (!allScenes.IsSuccess)
            return JsonSerializer.Serialize(new { error = "Failed to load scenes" }, JsonOpts);

        var found = allScenes.Value!.FirstOrDefault(s =>
            s.Name.Equals(scene, StringComparison.OrdinalIgnoreCase) ||
            s.Id.Equals(scene, StringComparison.OrdinalIgnoreCase));
        if (found == null)
            return JsonSerializer.Serialize(new { error = $"Scene '{scene}' not found" }, JsonOpts);

        // Find the room
        var rooms = new List<RoomModel>();
        var roomResult = await _multiBridge.GetAllRoomsAsync();
        if (roomResult.IsSuccess) rooms.AddRange(roomResult.Value!);
        var zoneResult = await _multiBridge.GetAllZonesAsync();
        if (zoneResult.IsSuccess) rooms.AddRange(zoneResult.Value!);

        var targetRoom = FuzzyMatcher.FindRoom(rooms, room);
        if (targetRoom == null)
            return JsonSerializer.Serialize(new { error = $"Room '{room}' not found" }, JsonOpts);

        var result = await _animationService.StartSceneAsync(found.Id, targetRoom.Id, targetRoom.BridgeId);
        if (!result.IsSuccess)
            return JsonSerializer.Serialize(new { error = result.ErrorMessage }, JsonOpts);

        return JsonSerializer.Serialize(new { success = true, scene = found.Name, room = targetRoom.Name }, JsonOpts);
    }

    [McpServerTool(Name = "hue_create_scene"), Description("Create and save a new scene with light states.")]
    public async Task<string> CreateScene(
        [Description("Scene name")] string name,
        [Description("Target room name")] string room,
        [Description("JSON array of light states: [{\"light\":\"name\",\"on\":true,\"brightness\":80,\"color\":\"#FF0000\"}]")] string lights,
        [Description("Scene description")] string? description = null,
        [Description("Category for grouping")] string? category = null)
    {
        // Parse light states
        List<LightStateInput>? lightInputs;
        try
        {
            lightInputs = JsonSerializer.Deserialize<List<LightStateInput>>(lights,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            // Intentionally catch only deserialization errors — return a user-friendly message
            return JsonSerializer.Serialize(new { error = "Invalid lights JSON format" }, JsonOpts);
        }

        if (lightInputs == null || lightInputs.Count == 0)
            return JsonSerializer.Serialize(new { error = "No lights specified" }, JsonOpts);

        // Build scene
        var scene = new AnimatedSceneModel
        {
            Id = $"user_{Guid.NewGuid():N}",
            Name = name,
            Description = description ?? "",
            Category = category ?? "User",
            Version = "1.0"
        };

        // Create one animation per light so each light's state is stored separately
        foreach (var input in lightInputs)
        {
            var animation = new AnimationDefinition
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = $"{name} - {input.Light}",
                Type = AnimationType.Keyframe,
                LightAssignment = LightAssignment.All,
                RepeatMode = RepeatMode.Once,
                DurationSeconds = 0
            };

            var kf = new AnimationKeyframe
            {
                TimeSeconds = 0,
                IsOn = input.On ?? true,
                Brightness = input.Brightness.HasValue ? Math.Clamp(input.Brightness.Value / 100.0, 0, 1) : null,
                Color = ColorParser.Parse(input.Color),
                TransitionStyle = TransitionStyle.Linear
            };
            animation.Keyframes.Add(kf);
            scene.Animations.Add(animation);
        }

        var result = await _sceneStorage.SaveSceneAsync(scene);
        if (!result.IsSuccess)
            return JsonSerializer.Serialize(new { error = result.ErrorMessage }, JsonOpts);

        return JsonSerializer.Serialize(new { success = true, id = scene.Id, name = scene.Name }, JsonOpts);
    }

    [McpServerTool(Name = "hue_delete_scene"), Description("Delete a saved scene.")]
    public async Task<string> DeleteScene(
        [Description("Scene name or ID")] string scene)
    {
        // Find the scene to get its ID
        var allScenes = await _animationService.GetAllScenesAsync();
        if (!allScenes.IsSuccess)
            return JsonSerializer.Serialize(new { error = "Failed to load scenes" }, JsonOpts);

        var found = allScenes.Value!.FirstOrDefault(s =>
            s.Name.Equals(scene, StringComparison.OrdinalIgnoreCase) ||
            s.Id.Equals(scene, StringComparison.OrdinalIgnoreCase));
        if (found == null)
            return JsonSerializer.Serialize(new { error = $"Scene '{scene}' not found" }, JsonOpts);

        if (found.IsBuiltIn)
            return JsonSerializer.Serialize(new { error = "Cannot delete built-in scenes" }, JsonOpts);

        var result = await _sceneStorage.DeleteSceneAsync(found.Id);
        if (!result.IsSuccess)
            return JsonSerializer.Serialize(new { error = result.ErrorMessage }, JsonOpts);

        return JsonSerializer.Serialize(new { success = true, deleted = found.Name }, JsonOpts);
    }

    private class LightStateInput
    {
        public string Light { get; set; } = "";
        public bool? On { get; set; }
        public double? Brightness { get; set; }
        public string? Color { get; set; }
    }
}
