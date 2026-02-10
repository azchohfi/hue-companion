namespace HueCompanion.Core.Models;

/// <summary>
/// A single keyframe in a keyframe-based animation.
/// </summary>
public class AnimationKeyframe
{
    /// <summary>
    /// Time offset in seconds from the start of the animation.
    /// </summary>
    public double TimeSeconds { get; set; }

    /// <summary>
    /// Target brightness (0.0 to 1.0), or null to leave unchanged.
    /// </summary>
    public double? Brightness { get; set; }

    /// <summary>
    /// Target color, or null to leave unchanged.
    /// </summary>
    public HueColor? Color { get; set; }

    /// <summary>
    /// Target color temperature in mirek (153-500), or null to leave unchanged.
    /// </summary>
    public int? ColorTemperature { get; set; }

    /// <summary>
    /// Whether the light should be on at this keyframe.
    /// </summary>
    public bool? IsOn { get; set; }

    /// <summary>
    /// Transition style to use when moving to this keyframe.
    /// </summary>
    public TransitionStyle TransitionStyle { get; set; } = TransitionStyle.Linear;

    /// <summary>
    /// Transition duration in seconds (overrides automatic calculation).
    /// </summary>
    public double? TransitionDurationSeconds { get; set; }
}
