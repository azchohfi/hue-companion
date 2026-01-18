using HueWindows.Core.Services.Interfaces;

namespace HueWindows.Core.Services;

/// <summary>
/// Manages undo/redo history for timeline editing operations.
/// </summary>
public class TimelineCommandHistory
{
    private readonly Stack<ITimelineCommand> _undoStack = new();
    private readonly Stack<ITimelineCommand> _redoStack = new();
    private const int MaxHistorySize = 100;

    /// <summary>
    /// Event raised when the undo/redo state changes.
    /// </summary>
    public event EventHandler? StateChanged;

    /// <summary>
    /// Whether there are commands available to undo.
    /// </summary>
    public bool CanUndo => _undoStack.Count > 0;

    /// <summary>
    /// Whether there are commands available to redo.
    /// </summary>
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>
    /// Execute a command and add it to the undo history.
    /// </summary>
    public void Execute(ITimelineCommand command)
    {
        command.Execute();
        _undoStack.Push(command);
        
        // Clear redo stack when a new command is executed
        _redoStack.Clear();

        // Limit history size
        if (_undoStack.Count > MaxHistorySize)
        {
            // Remove oldest command
            var temp = new Stack<ITimelineCommand>(_undoStack.Reverse().Skip(1));
            _undoStack.Clear();
            foreach (var cmd in temp.Reverse())
            {
                _undoStack.Push(cmd);
            }
        }

        OnStateChanged();
    }

    /// <summary>
    /// Undo the last command.
    /// </summary>
    public void Undo()
    {
        if (!CanUndo) return;

        var command = _undoStack.Pop();
        command.Undo();
        _redoStack.Push(command);
        
        OnStateChanged();
    }

    /// <summary>
    /// Redo the last undone command.
    /// </summary>
    public void Redo()
    {
        if (!CanRedo) return;

        var command = _redoStack.Pop();
        command.Execute();
        _undoStack.Push(command);
        
        OnStateChanged();
    }

    /// <summary>
    /// Clear all history.
    /// </summary>
    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        OnStateChanged();
    }

    /// <summary>
    /// Get a description of the command that would be undone.
    /// </summary>
    public string? GetUndoDescription() => CanUndo ? _undoStack.Peek().Description : null;

    /// <summary>
    /// Get a description of the command that would be redone.
    /// </summary>
    public string? GetRedoDescription() => CanRedo ? _redoStack.Peek().Description : null;

    private void OnStateChanged()
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
