namespace HueCompanion.Core.Services.Interfaces;

/// <summary>
/// Represents a reversible command for timeline editing operations.
/// </summary>
public interface ITimelineCommand
{
    /// <summary>
    /// Execute the command.
    /// </summary>
    void Execute();

    /// <summary>
    /// Undo the command.
    /// </summary>
    void Undo();

    /// <summary>
    /// Description of the command for debugging/UI.
    /// </summary>
    string Description { get; }
}
