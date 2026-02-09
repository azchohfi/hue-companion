namespace HueWindows.Core.Models;

/// <summary>
/// Records a scene activation for tracking recent/frequent scene usage.
/// </summary>
public class SceneActivation
{
    public string SceneId { get; set; } = string.Empty;
    public DateTime ActivatedAt { get; set; }
    public int ActivationCount { get; set; }
}
