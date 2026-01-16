using HueWindows.Core.Models;
using HueWindows.Core.Services.Interfaces;

namespace HueWindows.Core.Services;

/// <summary>
/// Service for managing and executing animated scenes.
/// </summary>
public class AnimationService : IAnimationService
{
    private readonly ISceneStorageService _storageService;
    private readonly IHueBridgeService _bridgeService;
    private AnimationEngine? _currentEngine;
    private AnimatedSceneModel? _currentScene;
    private Guid? _currentTargetId;

    public AnimationService(
        ISceneStorageService storageService,
        IHueBridgeService bridgeService)
    {
        _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
        _bridgeService = bridgeService ?? throw new ArgumentNullException(nameof(bridgeService));
    }

    /// <inheritdoc/>
    public event EventHandler<AnimatedSceneEventArgs>? SceneStarted;

    /// <inheritdoc/>
    public event EventHandler<AnimatedSceneEventArgs>? SceneStopped;

    /// <inheritdoc/>
    public AnimatedSceneModel? CurrentScene => _currentScene;

    /// <inheritdoc/>
    public bool IsPlaying => _currentEngine != null;

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<AnimatedSceneModel>>> GetAllScenesAsync()
    {
        try
        {
            var builtInResult = await _storageService.LoadBuiltInScenesAsync();
            var userResult = await _storageService.LoadUserScenesAsync();

            var allScenes = new List<AnimatedSceneModel>();

            if (builtInResult.IsSuccess && builtInResult.Value != null)
            {
                allScenes.AddRange(builtInResult.Value);
            }

            if (userResult.IsSuccess && userResult.Value != null)
            {
                allScenes.AddRange(userResult.Value);
            }

            return Result<IReadOnlyList<AnimatedSceneModel>>.Success(allScenes);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<AnimatedSceneModel>>.Failure($"Failed to load scenes: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<AnimatedSceneModel>>> GetScenesByCategoryAsync(string category)
    {
        var allScenesResult = await GetAllScenesAsync();

        if (allScenesResult.IsFailure)
        {
            return allScenesResult;
        }

        var filtered = allScenesResult.Value!
            .Where(s => s.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return Result<IReadOnlyList<AnimatedSceneModel>>.Success(filtered);
    }

    /// <inheritdoc/>
    public async Task<Result<AnimatedSceneModel>> GetSceneAsync(string sceneId)
    {
        var allScenesResult = await GetAllScenesAsync();

        if (allScenesResult.IsFailure)
        {
            return Result<AnimatedSceneModel>.Failure(allScenesResult.Error!);
        }

        var scene = allScenesResult.Value!.FirstOrDefault(s => s.Id == sceneId);

        if (scene == null)
        {
            return Result<AnimatedSceneModel>.Failure($"Scene '{sceneId}' not found");
        }

        return Result<AnimatedSceneModel>.Success(scene);
    }

    /// <inheritdoc/>
    public async Task<Result> StartSceneAsync(string sceneId, Guid? targetId = null, List<Guid>? targetLights = null)
    {
        try
        {
            // Stop any currently running scene
            await StopCurrentSceneAsync();

            // Load the scene
            var sceneResult = await GetSceneAsync(sceneId);
            if (sceneResult.IsFailure)
            {
                return Result.Failure(sceneResult.Error!);
            }

            var scene = sceneResult.Value!;

            // Determine target lights
            List<Guid> lights;

            if (targetLights != null && targetLights.Count > 0)
            {
                lights = targetLights;
            }
            else
            {
                // Use scene's default targeting
                var effectiveTargetId = targetId ?? scene.TargetId;

                if (scene.TargetLights.Count > 0)
                {
                    lights = scene.TargetLights;
                }
                else if (effectiveTargetId.HasValue)
                {
                    lights = await GetLightsForTargetAsync(scene.DefaultTargeting, effectiveTargetId.Value);
                }
                else
                {
                    return Result.Failure("No target specified for scene. Please specify a room, zone, or lights.");
                }
            }

            if (lights.Count == 0)
            {
                return Result.Failure("No lights found for the specified target");
            }

            // Create and start the animation engine
            _currentEngine = new AnimationEngine(_bridgeService, scene, lights);
            _currentEngine.Start();

            _currentScene = scene;
            _currentTargetId = targetId ?? scene.TargetId;

            // Raise event
            SceneStarted?.Invoke(this, new AnimatedSceneEventArgs
            {
                Scene = scene,
                TargetId = _currentTargetId
            });

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to start scene: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task StopCurrentSceneAsync()
    {
        if (_currentEngine != null)
        {
            await _currentEngine.StopAsync();
            _currentEngine.Dispose();
            _currentEngine = null;

            var stoppedScene = _currentScene;
            var stoppedTargetId = _currentTargetId;

            _currentScene = null;
            _currentTargetId = null;

            if (stoppedScene != null)
            {
                SceneStopped?.Invoke(this, new AnimatedSceneEventArgs
                {
                    Scene = stoppedScene,
                    TargetId = stoppedTargetId
                });
            }
        }
    }

    private async Task<List<Guid>> GetLightsForTargetAsync(LightTargeting targeting, Guid targetId)
    {
        var lights = new List<Guid>();

        try
        {
            if (targeting == LightTargeting.Room)
            {
                var roomResult = await _bridgeService.GetRoomAsync(targetId);
                if (roomResult.IsSuccess && roomResult.Value != null)
                {
                    lights.AddRange(roomResult.Value.Lights.Select(l => l.Id));
                }
            }
            else if (targeting == LightTargeting.Zone)
            {
                var zoneResult = await _bridgeService.GetZoneAsync(targetId);
                if (zoneResult.IsSuccess && zoneResult.Value != null)
                {
                    lights.AddRange(zoneResult.Value.Lights.Select(l => l.Id));
                }
            }
        }
        catch
        {
            // Return empty list if we can't get lights
        }

        return lights;
    }
}
