using System.Text.Json;
using System.Text.Json.Serialization;
using HueCompanion.Core.Models;
using HueCompanion.Core.Services.Interfaces;

namespace HueCompanion.Core.Services;

/// <summary>
/// Service for loading and saving animated scene definitions from/to JSON files.
/// </summary>
public class SceneStorageService : ISceneStorageService
{
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly string? _basePath;

    public SceneStorageService(string? basePath = null)
    {
        _basePath = basePath;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    /// <inheritdoc/>
    public string BuiltInScenesPath
    {
        get
        {
            // For packaged apps, scenes are in the installation directory
            var appPath = AppContext.BaseDirectory;
            return Path.Combine(appPath, "Assets", "Scenes");
        }
    }

    /// <inheritdoc/>
    public string UserScenesPath
    {
        get
        {
            if (_basePath != null)
            {
                Directory.CreateDirectory(_basePath);
                return _basePath;
            }

            // User scenes go in the local app data folder
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var userScenesPath = Path.Combine(localAppData, "HueCompanion", "Scenes");

            // Ensure directory exists
            Directory.CreateDirectory(userScenesPath);

            return userScenesPath;
        }
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<AnimatedSceneModel>>> LoadBuiltInScenesAsync()
    {
        try
        {
            if (!Directory.Exists(BuiltInScenesPath))
            {
                return Result<IReadOnlyList<AnimatedSceneModel>>.Success(Array.Empty<AnimatedSceneModel>());
            }

            var scenes = new List<AnimatedSceneModel>();
            var files = Directory.GetFiles(BuiltInScenesPath, "scene_*.json");

            foreach (var file in files)
            {
                var sceneResult = await LoadSceneAsync(file);
                if (sceneResult.IsSuccess && sceneResult.Value != null)
                {
                    sceneResult.Value.IsBuiltIn = true;
                    scenes.Add(sceneResult.Value);
                }
            }

            return Result<IReadOnlyList<AnimatedSceneModel>>.Success(scenes);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<AnimatedSceneModel>>.Failure($"Failed to load built-in scenes: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<AnimatedSceneModel>>> LoadUserScenesAsync()
    {
        try
        {
            var scenes = new List<AnimatedSceneModel>();
            var files = Directory.GetFiles(UserScenesPath, "*.json");

            foreach (var file in files)
            {
                var sceneResult = await LoadSceneAsync(file);
                if (sceneResult.IsSuccess && sceneResult.Value != null)
                {
                    sceneResult.Value.IsBuiltIn = false;
                    scenes.Add(sceneResult.Value);
                }
            }

            return Result<IReadOnlyList<AnimatedSceneModel>>.Success(scenes);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<AnimatedSceneModel>>.Failure($"Failed to load user scenes: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<AnimatedSceneModel>> LoadSceneAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return Result<AnimatedSceneModel>.Failure($"Scene file not found: {filePath}");
            }

            var json = await File.ReadAllTextAsync(filePath);
            var scene = JsonSerializer.Deserialize<AnimatedSceneModel>(json, _jsonOptions);

            if (scene == null)
            {
                return Result<AnimatedSceneModel>.Failure($"Failed to deserialize scene from {filePath}");
            }

            // Validate the scene
            var validationResult = ValidateScene(scene);
            if (validationResult.IsFailure)
            {
                return Result<AnimatedSceneModel>.Failure($"Scene validation failed: {validationResult.Error}");
            }

            return Result<AnimatedSceneModel>.Success(scene);
        }
        catch (JsonException ex)
        {
            return Result<AnimatedSceneModel>.Failure($"Invalid JSON in scene file: {ex.Message}");
        }
        catch (Exception ex)
        {
            return Result<AnimatedSceneModel>.Failure($"Failed to load scene: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result> SaveSceneAsync(AnimatedSceneModel scene)
    {
        try
        {
            // Validate before saving
            var validationResult = ValidateScene(scene);
            if (validationResult.IsFailure)
            {
                return validationResult;
            }

            // Don't allow overwriting built-in scenes
            if (scene.IsBuiltIn)
            {
                return Result.Failure("Cannot overwrite built-in scenes");
            }

            var fileName = $"{scene.Id}.json";
            var filePath = Path.Combine(UserScenesPath, fileName);

            var json = JsonSerializer.Serialize(scene, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json);

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to save scene: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result> DeleteSceneAsync(string sceneId)
    {
        try
        {
            var fileName = $"{sceneId}.json";
            var filePath = Path.Combine(UserScenesPath, fileName);

            if (!File.Exists(filePath))
            {
                return Result.Failure($"Scene '{sceneId}' not found");
            }

            // Load the scene to check if it's built-in
            var sceneResult = await LoadSceneAsync(filePath);
            if (sceneResult.IsSuccess && sceneResult.Value?.IsBuiltIn == true)
            {
                return Result.Failure("Cannot delete built-in scenes");
            }

            File.Delete(filePath);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to delete scene: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public Result ValidateScene(AnimatedSceneModel scene)
    {
        // Basic validation rules
        if (string.IsNullOrWhiteSpace(scene.Id))
        {
            return Result.Failure("Scene ID is required");
        }

        if (string.IsNullOrWhiteSpace(scene.Name))
        {
            return Result.Failure("Scene name is required");
        }

        if (scene.Animations == null || scene.Animations.Count == 0)
        {
            return Result.Failure("Scene must have at least one animation");
        }

        // Validate each animation
        for (int i = 0; i < scene.Animations.Count; i++)
        {
            var animation = scene.Animations[i];
            var animResult = ValidateAnimation(animation, i);
            if (animResult.IsFailure)
            {
                return animResult;
            }
        }

        // Validate targeting
        if (scene.DefaultTargeting == LightTargeting.Room || scene.DefaultTargeting == LightTargeting.Zone)
        {
            // TargetId is optional - can be set at runtime
        }
        else if (scene.DefaultTargeting == LightTargeting.Lights)
        {
            // TargetLights is optional - can be set at runtime
        }

        return Result.Success();
    }

    private Result ValidateAnimation(AnimationDefinition animation, int index)
    {
        if (string.IsNullOrWhiteSpace(animation.Id))
        {
            return Result.Failure($"Animation {index}: ID is required");
        }

        if (animation.Type == AnimationType.Keyframe)
        {
            if (animation.Keyframes == null || animation.Keyframes.Count < 2)
            {
                return Result.Failure($"Animation {index} ({animation.Id}): Keyframe animations must have at least 2 keyframes");
            }

            if (animation.DurationSeconds <= 0)
            {
                return Result.Failure($"Animation {index} ({animation.Id}): Duration must be greater than 0");
            }

            // Validate keyframes are in order
            for (int i = 1; i < animation.Keyframes.Count; i++)
            {
                if (animation.Keyframes[i].TimeSeconds <= animation.Keyframes[i - 1].TimeSeconds)
                {
                    return Result.Failure($"Animation {index} ({animation.Id}): Keyframes must be in ascending time order");
                }
            }
        }
        else if (animation.Type == AnimationType.Event)
        {
            if (animation.EventPattern == null)
            {
                return Result.Failure($"Animation {index} ({animation.Id}): Event animations must have an EventPattern");
            }

            if (animation.EventPattern.Triggers == null || animation.EventPattern.Triggers.Count == 0)
            {
                return Result.Failure($"Animation {index} ({animation.Id}): EventPattern must have at least one trigger");
            }

            if (animation.EventPattern.MinIntervalSeconds < 0 || animation.EventPattern.MaxIntervalSeconds < animation.EventPattern.MinIntervalSeconds)
            {
                return Result.Failure($"Animation {index} ({animation.Id}): Invalid interval configuration");
            }
        }

        return Result.Success();
    }
}
