using HueWindows.Core.Models;

namespace HueWindows.Core.Services.Interfaces;

/// <summary>
/// Service for managing and executing animated scenes.
/// </summary>
public interface IAnimationService
{
    /// <summary>
    /// Event raised when an animated scene starts.
    /// </summary>
    event EventHandler<AnimatedSceneEventArgs>? SceneStarted;

    /// <summary>
    /// Event raised when an animated scene stops.
    /// </summary>
    event EventHandler<AnimatedSceneEventArgs>? SceneStopped;

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
    /// Starts playing an animated scene.
    /// </summary>
    /// <param name="sceneId">The ID of the scene to play.</param>
    /// <param name="targetId">Optional override for the target room/zone ID.</param>
    /// <param name="targetLights">Optional override for specific target lights.</param>
    Task<Result> StartSceneAsync(string sceneId, Guid? targetId = null, List<Guid>? targetLights = null);

    /// <summary>
    /// Stops the currently playing animated scene.
    /// </summary>
    Task StopCurrentSceneAsync();

    /// <summary>
    /// Gets the currently playing scene, if any.
    /// </summary>
    AnimatedSceneModel? CurrentScene { get; }

    /// <summary>
    /// Gets whether a scene is currently playing.
    /// </summary>
    bool IsPlaying { get; }
}

/// <summary>
/// Event arguments for animated scene events.
/// </summary>
public class AnimatedSceneEventArgs : EventArgs
{
    /// <summary>
    /// The scene that triggered the event.
    /// </summary>
    public AnimatedSceneModel Scene { get; init; } = null!;

    /// <summary>
    /// The target ID (room/zone) for the scene, if applicable.
    /// </summary>
    public Guid? TargetId { get; init; }
}
