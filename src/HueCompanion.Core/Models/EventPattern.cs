namespace HueCompanion.Core.Models;

/// <summary>
/// Defines an event-based animation pattern with random triggers.
/// </summary>
public class EventPattern
{
    /// <summary>
    /// List of possible event triggers to randomly select from.
    /// </summary>
    public List<EventTrigger> Triggers { get; set; } = new();

    /// <summary>
    /// Minimum time in seconds between events.
    /// </summary>
    public double MinIntervalSeconds { get; set; }

    /// <summary>
    /// Maximum time in seconds between events.
    /// </summary>
    public double MaxIntervalSeconds { get; set; }

    /// <summary>
    /// Probability (0.0 to 1.0) that an event will occur at each check.
    /// </summary>
    public double? Probability { get; set; }

    /// <summary>
    /// Whether multiple lights can trigger simultaneously.
    /// </summary>
    public bool AllowSimultaneous { get; set; }

    /// <summary>
    /// Maximum number of simultaneous triggers (if AllowSimultaneous is true).
    /// </summary>
    public int? MaxSimultaneous { get; set; }
}
