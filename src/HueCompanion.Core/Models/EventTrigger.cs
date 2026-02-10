namespace HueCompanion.Core.Models;

/// <summary>
/// A single event trigger definition (e.g., a lightning flash or sparkle).
/// </summary>
public class EventTrigger
{
    /// <summary>
    /// List of state changes to apply in sequence.
    /// </summary>
    public List<EventTriggerState> States { get; set; } = new();

    /// <summary>
    /// Relative probability weight for this trigger (higher = more likely).
    /// </summary>
    public double Weight { get; set; } = 1.0;
}

/// <summary>
/// A single state change within an event trigger.
/// </summary>
public class EventTriggerState
{
    /// <summary>
    /// Duration in seconds for this state.
    /// </summary>
    public double DurationSeconds { get; set; }

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
    /// Whether the light should be on during this state.
    /// </summary>
    public bool? IsOn { get; set; }

    /// <summary>
    /// Transition style for entering this state.
    /// </summary>
    public TransitionStyle TransitionStyle { get; set; } = TransitionStyle.Instant;
}
