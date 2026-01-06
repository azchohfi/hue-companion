namespace HueWindows.Core.Models;

/// <summary>
/// Represents a Hue scene for a room.
/// </summary>
public class SceneModel
{
    /// <summary>
    /// The unique identifier of the scene.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The display name of the scene.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The room this scene belongs to.
    /// </summary>
    public Guid RoomId { get; set; }

    /// <summary>
    /// A preview color representing the scene's mood.
    /// </summary>
    public HueColor? PreviewColor { get; set; }

    /// <summary>
    /// Whether this is a dynamic scene.
    /// </summary>
    public bool IsDynamic { get; set; }
}
