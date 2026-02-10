namespace HueCompanion.Core.Services.Interfaces;

/// <summary>
/// Service for managing animated scene assignments to rooms.
/// </summary>
public interface IRoomSceneAssignmentService
{
    /// <summary>
    /// Gets the animated scene IDs assigned to a room.
    /// </summary>
    Task<IReadOnlyList<string>> GetAssignedScenesAsync(Guid roomId);

    /// <summary>
    /// Assigns an animated scene to a room.
    /// </summary>
    Task AssignSceneToRoomAsync(Guid roomId, string sceneId);

    /// <summary>
    /// Removes an animated scene assignment from a room.
    /// </summary>
    Task RemoveSceneFromRoomAsync(Guid roomId, string sceneId);

    /// <summary>
    /// Gets all room assignments.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, List<string>>> GetAllAssignmentsAsync();
}
