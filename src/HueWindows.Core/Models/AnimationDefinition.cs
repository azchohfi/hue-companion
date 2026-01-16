namespace HueWindows.Core.Models;

/// <summary>
/// Defines a single animation within a scene (keyframe-based or event-based).
/// </summary>
public class AnimationDefinition
{
    /// <summary>
    /// Unique identifier for this animation.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Display name for this animation.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Type of animation (keyframe or event-based).
    /// </summary>
    public AnimationType Type { get; set; }

    /// <summary>
    /// How lights are assigned for this animation.
    /// </summary>
    public LightAssignment LightAssignment { get; set; }

    /// <summary>
    /// Light indices to target (if using Subset or Random assignment).
    /// </summary>
    public List<int> TargetLightIndices { get; set; } = new();

    /// <summary>
    /// Percentage of lights to target (if using Random assignment).
    /// </summary>
    public double? RandomPercentage { get; set; }

    /// <summary>
    /// List of keyframes for keyframe-based animations.
    /// </summary>
    public List<AnimationKeyframe> Keyframes { get; set; } = new();

    /// <summary>
    /// Event pattern for event-based animations.
    /// </summary>
    public EventPattern? EventPattern { get; set; }

    /// <summary>
    /// Total duration of the animation in seconds (for keyframe animations).
    /// </summary>
    public double DurationSeconds { get; set; }

    /// <summary>
    /// How the animation should repeat.
    /// </summary>
    public RepeatMode RepeatMode { get; set; }

    /// <summary>
    /// Priority/layer for animation execution (higher numbers play on top).
    /// </summary>
    public int Priority { get; set; }
}

/// <summary>
/// Type of animation.
/// </summary>
public enum AnimationType
{
    /// <summary>
    /// Keyframe-based smooth animation (interpolated transitions).
    /// </summary>
    Keyframe,

    /// <summary>
    /// Event-based random triggers (e.g., lightning flashes, sparkles).
    /// </summary>
    Event,

    /// <summary>
    /// Native Hue effect (fire, candle, etc.).
    /// </summary>
    NativeEffect
}

/// <summary>
/// How lights are assigned for an animation.
/// </summary>
public enum LightAssignment
{
    /// <summary>
    /// All available lights.
    /// </summary>
    All,

    /// <summary>
    /// Specific subset of lights by index.
    /// </summary>
    Subset,

    /// <summary>
    /// Random selection of lights.
    /// </summary>
    Random,

    /// <summary>
    /// Alternating lights (every other light).
    /// </summary>
    Alternating
}

/// <summary>
/// How an animation repeats.
/// </summary>
public enum RepeatMode
{
    /// <summary>
    /// Play once and stop.
    /// </summary>
    Once,

    /// <summary>
    /// Loop continuously.
    /// </summary>
    Loop,

    /// <summary>
    /// Loop forward then backward (ping-pong).
    /// </summary>
    PingPong
}

/// <summary>
/// Transition style between keyframes.
/// </summary>
public enum TransitionStyle
{
    /// <summary>
    /// Linear interpolation.
    /// </summary>
    Linear,

    /// <summary>
    /// Ease in (slow start).
    /// </summary>
    EaseIn,

    /// <summary>
    /// Ease out (slow end).
    /// </summary>
    EaseOut,

    /// <summary>
    /// Ease in and out (slow start and end).
    /// </summary>
    EaseInOut,

    /// <summary>
    /// Instant jump (no interpolation).
    /// </summary>
    Instant
}
