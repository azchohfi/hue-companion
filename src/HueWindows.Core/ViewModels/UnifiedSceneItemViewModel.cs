using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HueWindows.Core.Models;

namespace HueWindows.Core.ViewModels;

/// <summary>
/// Wrapper ViewModel that unifies native Hue scenes and animated scenes
/// into a single bindable type for the room detail page.
/// </summary>
public partial class UnifiedSceneItemViewModel : ObservableObject
{
    private readonly SceneItemViewModel? _nativeScene;
    private readonly AnimatedSceneModel? _animatedScene;

    /// <summary>
    /// Creates a unified wrapper for a native Hue scene.
    /// </summary>
    public UnifiedSceneItemViewModel(SceneItemViewModel nativeScene)
    {
        _nativeScene = nativeScene ?? throw new ArgumentNullException(nameof(nativeScene));
        _nativeScene.SceneActivated += (s, id) => SceneActivated?.Invoke(this, id);
        _nativeScene.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(SceneItemViewModel.IsActive))
                OnPropertyChanged(nameof(IsActive));
        };
    }

    /// <summary>
    /// Creates a unified wrapper for an animated scene.
    /// </summary>
    public UnifiedSceneItemViewModel(AnimatedSceneModel animatedScene)
    {
        _animatedScene = animatedScene ?? throw new ArgumentNullException(nameof(animatedScene));
    }

    public bool IsNative => _nativeScene != null;
    public bool IsAnimated => _animatedScene != null;

    public string Name => _nativeScene?.Name ?? _animatedScene?.Name ?? string.Empty;

    /// <summary>
    /// Icon glyph: scene icon for native, play icon for animated.
    /// </summary>
    public string IconGlyph => IsNative ? "\uE91B" : "\uE768";

    /// <summary>
    /// Whether this scene is currently active (native scenes only).
    /// </summary>
    public bool IsActive
    {
        get => _nativeScene?.IsActive ?? false;
        set
        {
            if (_nativeScene != null)
                _nativeScene.IsActive = value;
        }
    }

    /// <summary>
    /// Gets the native scene ID (for delete operations).
    /// </summary>
    public Guid? NativeSceneId => _nativeScene?.SceneId;

    /// <summary>
    /// Gets the animated scene model (for start/remove operations).
    /// </summary>
    public AnimatedSceneModel? AnimatedScene => _animatedScene;

    /// <summary>
    /// Gets the underlying native scene view model.
    /// </summary>
    public SceneItemViewModel? NativeScene => _nativeScene;

    // Palette colors - unified from both scene types
    public bool HasPaletteColors => ColorCount > 0;
    public int ColorCount => IsNative ? _nativeScene!.ColorCount : (_animatedScene?.PaletteColors.Count ?? 0);

    public string Color1Hex => IsNative
        ? _nativeScene!.Color1Hex
        : GetAnimatedColorHex(0);

    public string Color2Hex => IsNative
        ? _nativeScene!.Color2Hex
        : GetAnimatedColorHex(1);

    public string Color3Hex => IsNative
        ? _nativeScene!.Color3Hex
        : GetAnimatedColorHex(2);

    public string Color4Hex => IsNative
        ? _nativeScene!.Color4Hex
        : GetAnimatedColorHex(3);

    /// <summary>
    /// Event raised when a native scene is activated (for pulse animation).
    /// </summary>
    public event EventHandler<Guid>? SceneActivated;

    /// <summary>
    /// Event raised when an animated scene should be started.
    /// </summary>
    public event EventHandler<AnimatedSceneModel>? AnimatedSceneRequested;

    [RelayCommand]
    private void Activate()
    {
        if (_nativeScene != null)
        {
            _nativeScene.ActivateCommand.Execute(null);
        }
        else if (_animatedScene != null)
        {
            AnimatedSceneRequested?.Invoke(this, _animatedScene);
        }
    }

    private string GetAnimatedColorHex(int index)
    {
        if (_animatedScene == null || index >= _animatedScene.PaletteColors.Count)
            return string.Empty;

        var (r, g, b) = _animatedScene.PaletteColors[index].ToRgb(1.0);
        return $"#{r:X2}{g:X2}{b:X2}";
    }
}
