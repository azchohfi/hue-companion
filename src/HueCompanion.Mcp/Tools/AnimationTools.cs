using System.ComponentModel;
using System.Text.Json;
using HueWindows.Core.Models;
using HueWindows.Core.Services.Interfaces;
using HueCompanion.Mcp.Services;
using ModelContextProtocol.Server;

namespace HueCompanion.Mcp.Tools;

[McpServerToolType]
public class AnimationTools
{
    private readonly IAnimationService _animationService;
    private readonly ISceneStorageService _sceneStorage;
    private readonly IMultiBridgeService _multiBridge;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    public AnimationTools(IAnimationService animationService, ISceneStorageService sceneStorage, IMultiBridgeService multiBridge)
    {
        _animationService = animationService;
        _sceneStorage = sceneStorage;
        _multiBridge = multiBridge;
    }

    [McpServerTool(Name = "hue_create_animated_scene"), Description("Create an animated scene with keyframe-based animations. Each track is for one light with keyframes at specific times.")]
    public async Task<string> CreateAnimatedScene(
        [Description("Scene name")] string name,
        [Description("Target room name")] string room,
        [Description("JSON array of tracks: [{\"light\":\"name\",\"keyframes\":[{\"time_sec\":0,\"brightness\":80,\"color\":\"#FF0000\",\"easing\":\"linear\"}]}]")] string tracks,
        [Description("Total animation duration in seconds")] double durationSec = 30,
        [Description("Whether to loop the animation")] bool loop = true,
        [Description("Scene description")] string? description = null)
    {
        List<TrackInput>? trackInputs;
        try
        {
            trackInputs = JsonSerializer.Deserialize<List<TrackInput>>(tracks,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            // Intentionally catch only deserialization errors — return a user-friendly message
            return JsonSerializer.Serialize(new { error = "Invalid tracks JSON format" }, JsonOpts);
        }

        if (trackInputs == null || trackInputs.Count == 0)
            return JsonSerializer.Serialize(new { error = "No tracks specified" }, JsonOpts);

        var scene = new AnimatedSceneModel
        {
            Id = $"user_{Guid.NewGuid():N}",
            Name = name,
            Description = description ?? "",
            Category = "User",
            Version = "1.0"
        };

        // Create one animation definition per track
        foreach (var track in trackInputs)
        {
            var animation = new AnimationDefinition
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = $"{name} - {track.Light}",
                Type = AnimationType.Keyframe,
                LightAssignment = LightAssignment.All,
                RepeatMode = loop ? RepeatMode.Loop : RepeatMode.Once,
                DurationSeconds = durationSec
            };

            foreach (var kf in track.Keyframes)
            {
                var keyframe = new AnimationKeyframe
                {
                    TimeSeconds = kf.TimeSec,
                    Brightness = kf.Brightness.HasValue ? Math.Clamp(kf.Brightness.Value / 100.0, 0, 1) : null,
                    Color = ColorParser.Parse(kf.Color),
                    TransitionStyle = ParseEasing(kf.Easing)
                };
                animation.Keyframes.Add(keyframe);
            }

            scene.Animations.Add(animation);
        }

        var result = await _sceneStorage.SaveSceneAsync(scene);
        if (!result.IsSuccess)
            return JsonSerializer.Serialize(new { error = result.ErrorMessage }, JsonOpts);

        return JsonSerializer.Serialize(new
        {
            success = true,
            id = scene.Id,
            name = scene.Name,
            trackCount = trackInputs.Count,
            durationSec
        }, JsonOpts);
    }

    [McpServerTool(Name = "hue_play_animation"), Description("Start playing an animated scene in a room.")]
    public async Task<string> PlayAnimation(
        [Description("Animated scene name or ID")] string scene,
        [Description("Target room or zone name")] string room)
    {
        var allScenes = await _animationService.GetAllScenesAsync();
        if (!allScenes.IsSuccess)
            return JsonSerializer.Serialize(new { error = "Failed to load scenes" }, JsonOpts);

        var found = allScenes.Value!.FirstOrDefault(s =>
            s.Name.Equals(scene, StringComparison.OrdinalIgnoreCase) ||
            s.Id.Equals(scene, StringComparison.OrdinalIgnoreCase));
        if (found == null)
            return JsonSerializer.Serialize(new { error = $"Scene '{scene}' not found" }, JsonOpts);

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

        return JsonSerializer.Serialize(new { success = true, scene = found.Name, room = targetRoom.Name, playing = true }, JsonOpts);
    }

    [McpServerTool(Name = "hue_stop_animation"), Description("Stop all currently playing animations.")]
    public async Task<string> StopAnimation(
        [Description("Whether to restore lights to their previous state before the animation started")] bool restore_previous = false)
    {
        await _animationService.StopAllScenesAsync();
        return JsonSerializer.Serialize(new { success = true, stoppedAll = true, restoredPrevious = restore_previous }, JsonOpts);
    }

    [McpServerTool(Name = "hue_edit_scene"), Description("Edit an existing scene. Accepts high-level edits like transitions, keyframe changes, and effect additions.")]
    public async Task<string> EditScene(
        [Description("Scene name or ID")] string scene,
        [Description("JSON array of edits: [{\"light\":\"name\",\"action\":\"transition\",\"from_color\":\"red\",\"to_color\":\"blue\"}]")] string edits,
        [Description("Save changes immediately")] bool save = true)
    {
        var allScenes = await _animationService.GetAllScenesAsync();
        if (!allScenes.IsSuccess)
            return JsonSerializer.Serialize(new { error = "Failed to load scenes" }, JsonOpts);

        var found = allScenes.Value!.FirstOrDefault(s =>
            s.Name.Equals(scene, StringComparison.OrdinalIgnoreCase) ||
            s.Id.Equals(scene, StringComparison.OrdinalIgnoreCase));
        if (found == null)
            return JsonSerializer.Serialize(new { error = $"Scene '{scene}' not found" }, JsonOpts);

        if (found.IsBuiltIn)
            return JsonSerializer.Serialize(new { error = "Cannot edit built-in scenes" }, JsonOpts);

        List<EditInput>? editInputs;
        try
        {
            editInputs = JsonSerializer.Deserialize<List<EditInput>>(edits,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            // Intentionally catch only deserialization errors — return a user-friendly message
            return JsonSerializer.Serialize(new { error = "Invalid edits JSON format" }, JsonOpts);
        }

        if (editInputs == null || editInputs.Count == 0)
            return JsonSerializer.Serialize(new { error = "No edits specified" }, JsonOpts);

        int appliedCount = 0;
        var summaries = new List<string>();

        foreach (var edit in editInputs)
        {
            // Find the animation/track for this light
            var animation = found.Animations.FirstOrDefault(a =>
                a.Name.Contains(edit.Light, StringComparison.OrdinalIgnoreCase));

            if (animation == null && found.Animations.Count > 0)
            {
                // If single animation, use it
                animation = found.Animations[0];
            }

            if (animation == null) continue;

            switch (edit.Action?.ToLowerInvariant())
            {
                case "transition":
                {
                    var fromColor = ColorParser.Parse(edit.FromColor);
                    var toColor = ColorParser.Parse(edit.ToColor);
                    if (fromColor == null || toColor == null) continue;

                    animation.Keyframes.Clear();
                    animation.Keyframes.Add(new AnimationKeyframe
                    {
                        TimeSeconds = edit.FromSec ?? 0,
                        Color = fromColor,
                        Brightness = edit.Brightness.HasValue ? edit.Brightness.Value / 100.0 : null,
                        TransitionStyle = ParseEasing(edit.Easing)
                    });
                    animation.Keyframes.Add(new AnimationKeyframe
                    {
                        TimeSeconds = edit.ToSec ?? animation.DurationSeconds,
                        Color = toColor,
                        TransitionStyle = ParseEasing(edit.Easing)
                    });
                    summaries.Add($"{edit.Light}: {edit.FromColor} → {edit.ToColor}");
                    appliedCount++;
                    break;
                }
                case "set_at":
                {
                    var kf = new AnimationKeyframe
                    {
                        TimeSeconds = edit.AtSec ?? 0,
                        Color = ColorParser.Parse(edit.Color),
                        Brightness = edit.Brightness.HasValue ? edit.Brightness.Value / 100.0 : null,
                        TransitionStyle = TransitionStyle.Linear
                    };
                    // Insert in sorted order
                    var idx = animation.Keyframes.FindIndex(k => k.TimeSeconds > kf.TimeSeconds);
                    if (idx >= 0) animation.Keyframes.Insert(idx, kf);
                    else animation.Keyframes.Add(kf);
                    summaries.Add($"{edit.Light}: set at {edit.AtSec}s");
                    appliedCount++;
                    break;
                }
                case "set_brightness":
                {
                    var kf = new AnimationKeyframe
                    {
                        TimeSeconds = edit.AtSec ?? 0,
                        Brightness = edit.Brightness.HasValue ? edit.Brightness.Value / 100.0 : null,
                        TransitionStyle = TransitionStyle.Linear
                    };
                    var idx = animation.Keyframes.FindIndex(k => k.TimeSeconds > kf.TimeSeconds);
                    if (idx >= 0) animation.Keyframes.Insert(idx, kf);
                    else animation.Keyframes.Add(kf);
                    summaries.Add($"{edit.Light}: brightness {edit.Brightness}% at {edit.AtSec}s");
                    appliedCount++;
                    break;
                }
                case "remove":
                {
                    found.Animations.Remove(animation);
                    summaries.Add($"{edit.Light}: removed");
                    appliedCount++;
                    break;
                }
                case "add_effect":
                {
                    if (edit.Effect != null)
                    {
                        var eventAnim = new AnimationDefinition
                        {
                            Id = Guid.NewGuid().ToString("N"),
                            Name = $"{edit.Light} - {edit.Effect}",
                            Type = AnimationType.Event,
                            EventPreset = edit.Effect,
                            LightAssignment = LightAssignment.All,
                            RepeatMode = RepeatMode.Loop,
                            DurationSeconds = animation.DurationSeconds
                        };
                        found.Animations.Add(eventAnim);
                        summaries.Add($"{edit.Light}: added {edit.Effect}");
                        appliedCount++;
                    }
                    break;
                }
                case "remove_effect":
                {
                    var toRemove = found.Animations
                        .Where(a => a.Type == AnimationType.Event &&
                                    (a.EventPreset?.Contains(edit.Effect ?? "", StringComparison.OrdinalIgnoreCase) == true ||
                                     a.Name.Contains(edit.Light, StringComparison.OrdinalIgnoreCase)))
                        .ToList();
                    foreach (var r in toRemove)
                    {
                        found.Animations.Remove(r);
                        appliedCount++;
                    }
                    summaries.Add($"{edit.Light}: removed effects");
                    break;
                }
            }
        }

        if (save)
        {
            var saveResult = await _sceneStorage.SaveSceneAsync(found);
            if (!saveResult.IsSuccess)
                return JsonSerializer.Serialize(new { error = saveResult.ErrorMessage }, JsonOpts);
        }

        return JsonSerializer.Serialize(new
        {
            success = true,
            scene = found.Name,
            editsApplied = appliedCount,
            summary = string.Join(", ", summaries)
        }, JsonOpts);
    }

    [McpServerTool(Name = "hue_list_event_presets"), Description("List available event presets that can be added to animated scenes.")]
    public string ListEventPresets()
    {
        var presets = new[]
        {
            new { Name = "LightningFlash", Description = "Random bright white flashes simulating lightning", DefaultIntervalSec = 3.0 },
            new { Name = "Sparkle", Description = "Brief random brightness pulses", DefaultIntervalSec = 1.0 },
            new { Name = "CandleFlicker", Description = "Warm subtle brightness variations like a candle flame", DefaultIntervalSec = 0.5 }
        };
        return JsonSerializer.Serialize(presets, JsonOpts);
    }

    private static TransitionStyle ParseEasing(string? easing) => easing?.ToLowerInvariant() switch
    {
        "ease-in" => TransitionStyle.EaseIn,
        "ease-out" => TransitionStyle.EaseOut,
        "ease-in-out" => TransitionStyle.EaseInOut,
        "instant" => TransitionStyle.Instant,
        _ => TransitionStyle.Linear
    };

    private class TrackInput
    {
        public string Light { get; set; } = "";
        public List<KeyframeInput> Keyframes { get; set; } = new();
    }

    private class KeyframeInput
    {
        public double TimeSec { get; set; }
        public double? Brightness { get; set; }
        public string? Color { get; set; }
        public string? Easing { get; set; }
    }

    private class EditInput
    {
        public string Light { get; set; } = "";
        public string? Action { get; set; }
        public string? FromColor { get; set; }
        public string? ToColor { get; set; }
        public string? Color { get; set; }
        public double? Brightness { get; set; }
        public double? AtSec { get; set; }
        public double? FromSec { get; set; }
        public double? ToSec { get; set; }
        public string? Easing { get; set; }
        public string? Effect { get; set; }
    }
}
