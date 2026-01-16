using HueWindows.Core.Models;

namespace HueWindows.Core.Services.Interfaces;

/// <summary>
/// Service for managing and executing animated scenes.
/// Supports concurrent animations in multiple rooms.
/// </summary>
public interface IAnimationService
{
    /// <summary>
    /// Event raised when an animation state changes in any room.
    /// </summary>
    event EventHandler<RoomAnimationChangedEventArgs>? RoomAnimationChanged;

    /// <summary>
    /// Gets all available animated scenes (built-in and user-created).
    /// </summary>
    Task<Result<IReadOnlyList<AnimatedSceneModel>>> GetAllScenesAsync();

    /// <summary>
    /// Gets animated scenes filtered by category.
    /// </summary>
    Task<Result<IReadOnlyList<AnimatedSceneModel>>> GetScenesByCategoryAsync(string category);

    /// <summary>
    /// Gets a specific animated scene by ID.
    /// </summary>
    Task<Result<AnimatedSceneModel>> GetSceneAsync(string sceneId);

    /// <summary>
    /// Starts playing an animated scene in a specific room/zone.
    /// If an animation is already running in that room, it will be stopped first.
    /// </summary>
    /// <param name="sceneId">The ID of the scene to play.</param>
    /// <param name="roomId">The target room or zone ID.</param>
    Task<Result> StartSceneAsync(string sceneId, Guid roomId);

    /// <summary>
    /// Stops the animation running in a specific room/zone.
    /// </summary>
    /// <param name="roomId">The room or zone ID to stop.</param>
    Task StopSceneInRoomAsync(Guid roomId);

    /// <summary>
    /// Stops all running animations across all rooms.
    /// </summary>
    Task StopAllScenesAsync();

    /// <summary>
    /// Checks if an animation is running in a specific room/zone.
    /// </summary>
    /// <param name="roomId">The room or zone ID to check.</param>
    bool IsAnimationRunning(Guid roomId);

    /// <summary>
    /// Gets the scene currently running in a specific room/zone.
    /// </summary>
    /// <param name="roomId">The room or zone ID to check.</param>
    /// <returns>The running scene, or null if no animation is running.</returns>
    AnimatedSceneModel? GetRunningScene(Guid roomId);

    /// <summary>
    /// Gets information about all currently running animations.
    /// </summary>
    IReadOnlyList<RunningAnimationInfo> GetAllRunningAnimations();

    /// <summary>
    /// Gets whether any animation is currently playing.
    /// </summary>
    bool IsAnyAnimationRunning { get; }
}

/// <summary>
/// Event arguments for room animation state changes.
/// </summary>
public class RoomAnimationChangedEventArgs : EventArgs
{
    /// <summary>
    /// The room or zone ID where the animation state changed.
    /// </summary>
    public Guid RoomId { get; init; }

    /// <summary>
    /// The scene that started or stopped. Null if animation stopped.
    /// </summary>
    public AnimatedSceneModel? Scene { get; init; }

    /// <summary>
    /// Whether an animation is now running in this room.
    /// </summary>
    public bool IsRunning { get; init; }
}

/// <summary>
/// Information about a running animation.
/// </summary>
public class RunningAnimationInfo
{
    /// <summary>
    /// The room or zone ID where the animation is running.
    /// </summary>
    public Guid RoomId { get; init; }

    /// <summary>
    /// The scene being played.
    /// </summary>
    public AnimatedSceneModel Scene { get; init; } = null!;

    /// <summary>
    /// When the animation started.
    /// </summary>
    public DateTime StartedAt { get; init; }
}
