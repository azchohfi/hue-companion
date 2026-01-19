using System.Collections.Concurrent;
using HueWindows.Core.Models;
using HueWindows.Core.Services.Interfaces;

namespace HueWindows.Core.Services;

/// <summary>
/// Service for managing and executing animated scenes.
/// Supports concurrent animations in multiple rooms.
/// </summary>
public class AnimationService : IAnimationService
{
    private readonly ISceneStorageService _storageService;
    private readonly IMultiBridgeService _multiBridgeService;
    private readonly ConcurrentDictionary<Guid, RunningAnimation> _runningAnimations = new();

    public AnimationService(
        ISceneStorageService storageService,
        IMultiBridgeService multiBridgeService)
    {
        _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
        _multiBridgeService = multiBridgeService ?? throw new ArgumentNullException(nameof(multiBridgeService));
    }

    /// <inheritdoc/>
    public event EventHandler<RoomAnimationChangedEventArgs>? RoomAnimationChanged;

    /// <inheritdoc/>
    public bool IsAnyAnimationRunning => !_runningAnimations.IsEmpty;

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
    public async Task<Result> StartSceneAsync(string sceneId, Guid roomId, string? bridgeId = null)
    {
        try
        {
            // Stop any animation currently running in this room
            await StopSceneInRoomAsync(roomId);

            // Get bridge service
            var bridgeService = bridgeId != null
                ? _multiBridgeService.GetBridgeService(bridgeId)
                : _multiBridgeService.GetDefaultBridgeService();

            if (bridgeService == null)
            {
                return Result.Failure("No bridge connected. Please configure a bridge in Settings.");
            }

            // Load the scene
            var sceneResult = await GetSceneAsync(sceneId);
            if (sceneResult.IsFailure)
            {
                return Result.Failure(sceneResult.Error!);
            }

            var scene = sceneResult.Value!;

            // Get lights for the target room/zone
            var lights = await GetLightsForRoomAsync(roomId, bridgeService);

            if (lights.Count == 0)
            {
                return Result.Failure("No lights found in the specified room/zone");
            }

            // Create and start the animation engine
            var engine = new AnimationEngine(bridgeService, scene, lights);
            engine.Start();

            var runningAnimation = new RunningAnimation
            {
                Engine = engine,
                Scene = scene,
                RoomId = roomId,
                StartedAt = DateTime.UtcNow
            };

            _runningAnimations[roomId] = runningAnimation;

            // Raise event
            RoomAnimationChanged?.Invoke(this, new RoomAnimationChangedEventArgs
            {
                RoomId = roomId,
                Scene = scene,
                IsRunning = true
            });

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to start scene: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task StopSceneInRoomAsync(Guid roomId)
    {
        if (_runningAnimations.TryRemove(roomId, out var animation))
        {
            await animation.Engine.StopAsync();
            animation.Engine.Dispose();

            // Raise event
            RoomAnimationChanged?.Invoke(this, new RoomAnimationChangedEventArgs
            {
                RoomId = roomId,
                Scene = animation.Scene,
                IsRunning = false
            });
        }
    }

    /// <inheritdoc/>
    public async Task StopAllScenesAsync()
    {
        var roomIds = _runningAnimations.Keys.ToList();

        foreach (var roomId in roomIds)
        {
            await StopSceneInRoomAsync(roomId);
        }
    }

    /// <inheritdoc/>
    public bool IsAnimationRunning(Guid roomId)
    {
        return _runningAnimations.ContainsKey(roomId);
    }

    /// <inheritdoc/>
    public AnimatedSceneModel? GetRunningScene(Guid roomId)
    {
        if (_runningAnimations.TryGetValue(roomId, out var animation))
        {
            return animation.Scene;
        }
        return null;
    }

    /// <inheritdoc/>
    public IReadOnlyList<RunningAnimationInfo> GetAllRunningAnimations()
    {
        return _runningAnimations.Values
            .Select(a => new RunningAnimationInfo
            {
                RoomId = a.RoomId,
                Scene = a.Scene,
                StartedAt = a.StartedAt
            })
            .ToList();
    }

    private async Task<List<Guid>> GetLightsForRoomAsync(Guid roomId, IHueBridgeService bridgeService)
    {
        var lights = new List<Guid>();

        try
        {
            // Try as room first
            var roomResult = await bridgeService.GetRoomAsync(roomId);
            if (roomResult.IsSuccess && roomResult.Value != null)
            {
                lights.AddRange(roomResult.Value.Lights.Select(l => l.Id));
                return lights;
            }

            // Try as zone
            var zoneResult = await bridgeService.GetZoneAsync(roomId);
            if (zoneResult.IsSuccess && zoneResult.Value != null)
            {
                lights.AddRange(zoneResult.Value.Lights.Select(l => l.Id));
            }
        }
        catch
        {
            // Return empty list if we can't get lights
        }

        return lights;
    }

    /// <summary>
    /// Internal class to track a running animation.
    /// </summary>
    private class RunningAnimation
    {
        public AnimationEngine Engine { get; init; } = null!;
        public AnimatedSceneModel Scene { get; init; } = null!;
        public Guid RoomId { get; init; }
        public DateTime StartedAt { get; init; }
    }
}
