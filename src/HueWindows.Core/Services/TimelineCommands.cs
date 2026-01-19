using HueWindows.Core.Models;
using HueWindows.Core.Services.Interfaces;
using HueWindows.Core.ViewModels;
using System.Collections.ObjectModel;

namespace HueWindows.Core.Services;

/// <summary>
/// Command for adding a keyframe to a track.
/// </summary>
public class AddKeyframeCommand : ITimelineCommand
{
    private readonly TrackViewModel _track;
    private readonly KeyframeViewModel _keyframe;
    private int _insertIndex;

    public string Description => $"Add keyframe at {_keyframe.TimeSeconds:F2}s";

    public AddKeyframeCommand(TrackViewModel track, KeyframeViewModel keyframe)
    {
        _track = track;
        _keyframe = keyframe;
    }

    public void Execute()
    {
        // Find the correct insertion position (maintain sorted order)
        _insertIndex = 0;
        for (int i = 0; i < _track.Keyframes.Count; i++)
        {
            if (_track.Keyframes[i].TimeSeconds > _keyframe.TimeSeconds)
            {
                _insertIndex = i;
                break;
            }
            _insertIndex = i + 1;
        }

        _track.Keyframes.Insert(_insertIndex, _keyframe);
    }

    public void Undo()
    {
        _track.Keyframes.Remove(_keyframe);
    }
}

/// <summary>
/// Command for deleting keyframes from a track.
/// </summary>
public class DeleteKeyframesCommand : ITimelineCommand
{
    private readonly List<(TrackViewModel Track, KeyframeViewModel Keyframe, int Index)> _deletedKeyframes = new();

    public string Description => _deletedKeyframes.Count == 1 
        ? "Delete keyframe" 
        : $"Delete {_deletedKeyframes.Count} keyframes";

    public DeleteKeyframesCommand(TrackViewModel track, IEnumerable<KeyframeViewModel> keyframes)
    {
        foreach (var keyframe in keyframes)
        {
            var index = track.Keyframes.IndexOf(keyframe);
            if (index >= 0)
            {
                _deletedKeyframes.Add((track, keyframe, index));
            }
        }
    }

    public DeleteKeyframesCommand(IEnumerable<(TrackViewModel Track, KeyframeViewModel Keyframe)> keyframes)
    {
        foreach (var (track, keyframe) in keyframes)
        {
            var index = track.Keyframes.IndexOf(keyframe);
            if (index >= 0)
            {
                _deletedKeyframes.Add((track, keyframe, index));
            }
        }
    }

    public void Execute()
    {
        // Delete in descending index order so indices remain valid
        foreach (var (track, keyframe, _) in _deletedKeyframes.OrderByDescending(x => x.Index))
        {
            track.Keyframes.Remove(keyframe);
        }
    }

    public void Undo()
    {
        // Restore in ascending index order (reverse of delete order)
        foreach (var (track, keyframe, index) in _deletedKeyframes.OrderBy(x => x.Index))
        {
            track.Keyframes.Insert(index, keyframe);
        }
    }
}

/// <summary>
/// Command for moving a keyframe's time position.
/// </summary>
public class MoveKeyframeCommand : ITimelineCommand
{
    private readonly KeyframeViewModel _keyframe;
    private readonly double _oldTime;
    private readonly double _newTime;

    public string Description => $"Move keyframe to {_newTime:F2}s";

    public MoveKeyframeCommand(KeyframeViewModel keyframe, double oldTime, double newTime)
    {
        _keyframe = keyframe;
        _oldTime = oldTime;
        _newTime = newTime;
    }

    public void Execute()
    {
        _keyframe.TimeSeconds = _newTime;
    }

    public void Undo()
    {
        _keyframe.TimeSeconds = _oldTime;
    }
}

/// <summary>
/// Command for modifying keyframe properties (color, brightness, transition).
/// </summary>
public class ModifyKeyframeCommand : ITimelineCommand
{
    private readonly KeyframeViewModel _keyframe;
    private readonly HueColor _oldColor;
    private readonly HueColor? _newColor;
    private readonly double? _oldBrightness;
    private readonly double? _newBrightness;
    private readonly Models.TransitionStyle? _oldTransition;
    private readonly Models.TransitionStyle? _newTransition;

    public string Description => "Modify keyframe";

    public ModifyKeyframeCommand(
        KeyframeViewModel keyframe,
        HueColor? newColor = null,
        double? newBrightness = null,
        Models.TransitionStyle? newTransition = null)
    {
        _keyframe = keyframe;
        _oldColor = _keyframe.Color;
        
        if (newColor != null)
        {
            _newColor = newColor;
        }
        
        if (newBrightness.HasValue)
        {
            _oldBrightness = _keyframe.Brightness;
            _newBrightness = newBrightness;
        }
        
        if (newTransition.HasValue)
        {
            _oldTransition = _keyframe.Transition;
            _newTransition = newTransition;
        }
    }

    public void Execute()
    {
        if (_newColor != null) _keyframe.Color = _newColor;
        if (_newBrightness.HasValue) _keyframe.Brightness = _newBrightness.Value;
        if (_newTransition.HasValue) _keyframe.Transition = _newTransition.Value;
    }

    public void Undo()
    {
        _keyframe.Color = _oldColor;
        if (_oldBrightness.HasValue) _keyframe.Brightness = _oldBrightness.Value;
        if (_oldTransition.HasValue) _keyframe.Transition = _oldTransition.Value;
    }
}

/// <summary>
/// Command for batch operations (combines multiple commands).
/// </summary>
public class BatchCommand : ITimelineCommand
{
    private readonly List<ITimelineCommand> _commands = new();
    private readonly string _description;

    public string Description => _description;

    public BatchCommand(string description, IEnumerable<ITimelineCommand> commands)
    {
        _description = description;
        _commands.AddRange(commands);
    }

    public void Execute()
    {
        foreach (var command in _commands)
        {
            command.Execute();
        }
    }

    public void Undo()
    {
        // Undo in reverse order
        foreach (var command in _commands.AsEnumerable().Reverse())
        {
            command.Undo();
        }
    }
}
