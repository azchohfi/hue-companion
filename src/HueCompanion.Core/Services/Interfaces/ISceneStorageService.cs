using HueCompanion.Core.Models;

namespace HueCompanion.Core.Services.Interfaces;

/// <summary>
/// Service for loading and saving animated scene definitions from/to JSON files.
/// </summary>
public interface ISceneStorageService
{
    /// <summary>
    /// Loads all built-in animated scenes from the Assets/Scenes directory.
    /// </summary>
    Task<Result<IReadOnlyList<AnimatedSceneModel>>> LoadBuiltInScenesAsync();

    /// <summary>
    /// Loads user-created scenes from the user data directory.
    /// </summary>
    Task<Result<IReadOnlyList<AnimatedSceneModel>>> LoadUserScenesAsync();

    /// <summary>
    /// Loads a specific scene from a JSON file.
    /// </summary>
    Task<Result<AnimatedSceneModel>> LoadSceneAsync(string filePath);

    /// <summary>
    /// Saves a scene to the user data directory.
    /// </summary>
    Task<Result> SaveSceneAsync(AnimatedSceneModel scene);

    /// <summary>
    /// Deletes a user-created scene.
    /// </summary>
    Task<Result> DeleteSceneAsync(string sceneId);

    /// <summary>
    /// Validates a scene definition against the schema.
    /// </summary>
    Result ValidateScene(AnimatedSceneModel scene);

    /// <summary>
    /// Gets the path to the built-in scenes directory.
    /// </summary>
    string BuiltInScenesPath { get; }

    /// <summary>
    /// Gets the path to the user scenes directory.
    /// </summary>
    string UserScenesPath { get; }
}
