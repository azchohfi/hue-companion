using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Extensions.DependencyInjection;
using HueWindows.Core.ViewModels;
using HueWindows.Core.Models;
using Windows.UI;
using Windows.UI.Core;
using Windows.Storage.Pickers;
using Windows.ApplicationModel.DataTransfer;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using System.Numerics;
using HueWindows.Views.Rendering;

namespace HueWindows.Views;

/// <summary>
/// Scene Builder page with DAW-style timeline editor for creating animated scenes.
/// </summary>
public sealed partial class SceneBuilderPage : Page
{
    public SceneBuilderViewModel ViewModel { get; }
    private DispatcherTimer? _playbackTimer;
    private DateTime _lastFrameTime;
    private DateTime _lastLightUpdateTime;
    private const int LightUpdateIntervalMs = 100; // ~10 updates per second
    private bool _isDraggingPlayhead;
    private bool _isDraggingKeyframe;
    private KeyframeViewModel? _draggingKeyframe;
    private TrackViewModel? _draggingTrack;

    // Hover state tracking
    private KeyframeViewModel? _hoveredKeyframe;
    private bool _isPlayheadHovered;
    private HitType _lastHitType = HitType.None;

    // Win2D rendering
    private TimelineRenderer? _timelineRenderer;
    private bool _resourcesCreated = false;

    // Event track playback state
    private readonly Dictionary<string, double> _nextEventFireTimes = new();
    private readonly Dictionary<string, Microsoft.UI.Xaml.Shapes.Ellipse> _eventPulses = new();
    private readonly Random _random = new();

    private string? _sceneIdToLoad;

    // Flag to prevent feedback loops when updating panel from keyframe selection
    private bool _isUpdatingPanel;

    public SceneBuilderPage()
    {
        this.InitializeComponent();
        ViewModel = App.Services.GetRequiredService<SceneBuilderViewModel>();
        Loaded += Page_Loaded;
        Unloaded += SceneBuilderPage_Unloaded;
        KeyDown += SceneBuilderPage_KeyDown;

        // Initialize playback timer
        _playbackTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16) // ~60fps
        };
        _playbackTimer.Tick += PlaybackTimer_Tick;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        // Check if we're editing an existing scene
        if (e.Parameter is string sceneId && !string.IsNullOrEmpty(sceneId))
        {
            _sceneIdToLoad = sceneId;
        }
    }

    /// <summary>
    /// Handles keyboard shortcuts for timeline editing.
    /// </summary>
    private void SceneBuilderPage_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        var ctrl = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        var shift = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        switch (e.Key)
        {
            case Windows.System.VirtualKey.Space:
                // Play/Pause
                if (!e.Handled)
                {
                    PlayButton_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                }
                break;

            case Windows.System.VirtualKey.Delete:
            case Windows.System.VirtualKey.Back: // Backspace
                // Delete selected keyframes
                if (ViewModel.HasSelectedKeyframes())
                {
                    ViewModel.DeleteSelectedKeyframes();
                    RenderTimeline();
                    e.Handled = true;
                }
                break;

            case Windows.System.VirtualKey.Z when ctrl && !shift:
                // Undo
                if (ViewModel.CanUndo)
                {
                    ViewModel.Undo();
                    RenderTimeline();
                    e.Handled = true;
                }
                break;

            case Windows.System.VirtualKey.Y when ctrl:
            case Windows.System.VirtualKey.Z when ctrl && shift:
                // Redo
                if (ViewModel.CanRedo)
                {
                    ViewModel.Redo();
                    RenderTimeline();
                    e.Handled = true;
                }
                break;

            case Windows.System.VirtualKey.C when ctrl:
                // Copy selected keyframes
                if (ViewModel.HasSelectedKeyframes())
                {
                    ViewModel.CopyKeyframes();
                    e.Handled = true;
                }
                break;

            case Windows.System.VirtualKey.V when ctrl:
                // Paste keyframes
                if (ViewModel.CanPaste())
                {
                    ViewModel.PasteKeyframes();
                    RenderTimeline();
                    e.Handled = true;
                }
                break;

            case Windows.System.VirtualKey.A when ctrl:
                // Select all keyframes in current track
                if (ViewModel.Tracks.Count > 0)
                {
                    ViewModel.SelectedKeyframes.Clear();
                    foreach (var track in ViewModel.Tracks)
                    {
                        foreach (var keyframe in track.Keyframes)
                        {
                            ViewModel.SelectedKeyframes.Add(keyframe);
                        }
                    }
                    RenderTimeline();
                    e.Handled = true;
                }
                break;

            case Windows.System.VirtualKey.Escape:
                // Clear selection
                ViewModel.ClearSelection();
                RenderTimeline();
                e.Handled = true;
                break;

            case Windows.System.VirtualKey.Add when ctrl: // Ctrl + "+"
            case (Windows.System.VirtualKey)187 when ctrl: // Ctrl + "="
                // Zoom in
                if (ViewModel.ZoomLevel < 200)
                {
                    ViewModel.ZoomLevel = Math.Min(200, ViewModel.ZoomLevel + 10);
                    RenderTimeline();
                    e.Handled = true;
                }
                break;

            case Windows.System.VirtualKey.Subtract when ctrl: // Ctrl + "-"
            case (Windows.System.VirtualKey)189 when ctrl:
                // Zoom out
                if (ViewModel.ZoomLevel > 20)
                {
                    ViewModel.ZoomLevel = Math.Max(20, ViewModel.ZoomLevel - 10);
                    RenderTimeline();
                    e.Handled = true;
                }
                break;
        }
    }

    private async void PlaybackTimer_Tick(object? sender, object e)
    {
        try
        {
            if (ViewModel == null || !ViewModel.IsPlaying)
                return;

            var now = DateTime.Now;
            var deltaSeconds = (now - _lastFrameTime).TotalSeconds;
            _lastFrameTime = now;

            ViewModel.PlayheadPosition += deltaSeconds;

            // Check if we've reached the end
            if (ViewModel.PlayheadPosition >= ViewModel.DurationSeconds)
            {
                if (ViewModel.IsLooping)
                {
                    ViewModel.PlayheadPosition = 0;
                    InitializeEventPlayback(); // Reset event timers on loop
                    // Full re-render when looping back (ruler needs update)
                    RenderTimeline();
                    // Don't skip light update on loop - fall through to update lights at position 0
                }
                else
                {
                    ViewModel.PlayheadPosition = ViewModel.DurationSeconds;
                    ViewModel.IsPlaying = false;
                    _playbackTimer?.Stop();
                    UpdatePlayButtonState();
                    return; // Only return early when stopping, not when looping
                }
            }

            // Efficiently update just the playhead position (not full re-render)
            UpdatePlayheadPosition();

            // Rate-limit light updates to ~10 per second to avoid flooding the bridge
            var msSinceLastUpdate = (now - _lastLightUpdateTime).TotalMilliseconds;
            if (msSinceLastUpdate >= LightUpdateIntervalMs)
            {
                _lastLightUpdateTime = now;
                await ViewModel.UpdateLightsForPlayheadAsync();
            }

            // Check for event track triggers
            CheckEventTriggers();
        }
        catch
        {
            // Prevent exceptions from crashing the timer
        }
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.InitializeAsync();

        // If we have a scene to load, load it
        if (!string.IsNullOrEmpty(_sceneIdToLoad))
        {
            var result = await ViewModel.LoadSceneAsync(_sceneIdToLoad);
            if (result.IsFailure)
            {
                var dialog = new ContentDialog
                {
                    Title = "Load Failed",
                    Content = result.Error ?? "Failed to load the scene.",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
            }
            else
            {
                RenderTimeline();
            }
        }

        UpdateEmptyState();
        UpdateTimeDisplay();
    }

    private void UpdateEmptyState()
    {
        var hasRoom = ViewModel.SelectedRoom != null;
        EmptyStatePanel.Visibility = hasRoom ? Visibility.Collapsed : Visibility.Visible;
        TracksGrid.Visibility = hasRoom ? Visibility.Visible : Visibility.Collapsed;
    }

    #region Win2D Event Handlers

    private void TimelineCanvas_CreateResources(CanvasControl sender, CanvasCreateResourcesEventArgs args)
    {
        // Dispose old renderer on DPI change
        if (args.Reason == CanvasCreateResourcesReason.DpiChanged)
        {
            _timelineRenderer?.Dispose();
        }

        _timelineRenderer = new TimelineRenderer();
        _timelineRenderer.CreateResources(sender, GetRenderContext(sender));
        _resourcesCreated = true;
    }

    private void TimeRulerCanvas_CreateResources(CanvasControl sender, CanvasCreateResourcesEventArgs args)
    {
        // Ruler uses same renderer, resources created once
    }

    private void TimelineCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        if (!_resourcesCreated || _timelineRenderer == null)
            return;

        var context = GetRenderContext(sender);
        _timelineRenderer.Draw(args.DrawingSession, context);
    }

    private void TimeRulerCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        if (!_resourcesCreated || _timelineRenderer == null)
            return;

        // Draw ruler only
        _timelineRenderer.DrawRuler(
            args.DrawingSession,
            (float)sender.Size.Width,
            (float)sender.Size.Height,
            (float)ViewModel.ZoomLevel,
            (int)ViewModel.DurationSeconds);
    }

    private TimelineRenderContext GetRenderContext(CanvasControl sender)
    {
        const float trackHeight = 50f;
        return new TimelineRenderContext(
            Tracks: ViewModel.Tracks.ToList(),
            EventTracks: ViewModel.EventTracks.ToList(),
            SelectedKeyframes: ViewModel.SelectedKeyframes,
            ZoomLevel: (float)ViewModel.ZoomLevel,
            DurationSeconds: (float)ViewModel.DurationSeconds,
            PlayheadPosition: (float)ViewModel.PlayheadPosition,
            CanvasWidth: (float)sender.Size.Width,
            CanvasHeight: (float)sender.Size.Height,
            TrackHeight: trackHeight,
            SnapInterval: (float)ViewModel.SnapInterval,
            IsSnapEnabled: ViewModel.IsSnapEnabled,
            HoveredKeyframe: _hoveredKeyframe,
            IsPlayheadHovered: _isPlayheadHovered,
            IsPlayheadDragging: _isDraggingPlayhead
        );
    }

    private void SceneBuilderPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _timelineRenderer?.Dispose();
        _timelineRenderer = null;

        // Required to break reference cycle (from research)
        TimelineCanvas?.RemoveFromVisualTree();
        TimeRulerCanvas?.RemoveFromVisualTree();
    }

    #endregion

    private void RoomComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel == null) return;
        UpdateEmptyState();
        RenderTimeline();
    }

    private void RenderTimeline()
    {
        // Calculate and set canvas dimensions based on tracks and duration
        const double trackHeight = 50;
        const double keyframeRadius = 12; // Padding for keyframe visibility at edges
        var totalHeight = (ViewModel.Tracks.Count + ViewModel.EventTracks.Count) * trackHeight;
        // Include left margin and right padding so keyframes at edges aren't clipped
        var totalWidth = GradientTrackRenderer.LeftMargin + (ViewModel.DurationSeconds * ViewModel.ZoomLevel) + keyframeRadius;

        if (TimelineCanvas != null)
        {
            if (totalHeight > 0)
            {
                TimelineCanvas.Height = totalHeight;
            }
            if (totalWidth > 0)
            {
                TimelineCanvas.Width = totalWidth;
                TimelineCanvas.MinWidth = totalWidth; // Ensure minimum size
            }
        }

        if (TimeRulerCanvas != null && totalWidth > 0)
        {
            TimeRulerCanvas.Width = totalWidth;
            TimeRulerCanvas.MinWidth = totalWidth;
        }

        // Force layout update before invalidating to ensure canvas bounds are correct
        TimelineCanvas?.UpdateLayout();
        TimeRulerCanvas?.UpdateLayout();

        // Invalidate cache if zoom or duration changed
        _timelineRenderer?.InvalidateCache();

        // Trigger Win2D redraw
        TimelineCanvas?.Invalidate();
        TimeRulerCanvas?.Invalidate();

        UpdateTimeDisplay();
    }






    /// <summary>
    /// Efficiently updates just the playhead position without re-rendering everything.
    /// </summary>
    private void UpdatePlayheadPosition()
    {
        // Just invalidate - Win2D redraws efficiently
        TimelineCanvas?.Invalidate();

        // Update ruler playhead marker
        TimeRulerCanvas?.Invalidate();

        // Update time display
        UpdateTimeDisplay();
    }

    /// <summary>
    /// Updates the time display in the footer to show current position and duration.
    /// Format: mm:ss.ff (minutes:seconds.centiseconds)
    /// </summary>
    private void UpdateTimeDisplay()
    {
        if (CurrentTimeText != null)
        {
            CurrentTimeText.Text = FormatTime(ViewModel.PlayheadPosition);
        }
        if (TotalTimeText != null)
        {
            TotalTimeText.Text = FormatTime(ViewModel.DurationSeconds);
        }
    }

    /// <summary>
    /// Formats a time value in seconds as mm:ss.ff
    /// </summary>
    private static string FormatTime(double seconds)
    {
        var minutes = (int)(seconds / 60);
        var secs = (int)(seconds % 60);
        var centiseconds = (int)((seconds - Math.Floor(seconds)) * 100);
        return $"{minutes:D2}:{secs:D2}.{centiseconds:D2}";
    }


    private async void TimeRulerCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (ViewModel == null)
            return;

        var point = e.GetCurrentPoint(TimeRulerCanvas);
        var timeSeconds = (point.Position.X - GradientTrackRenderer.LeftMargin) / ViewModel.ZoomLevel;

        // Clamp to valid range
        if (timeSeconds < 0) timeSeconds = 0;
        if (timeSeconds > ViewModel.DurationSeconds) timeSeconds = ViewModel.DurationSeconds;

        // Apply snap when enabled, unless Ctrl is held
        var ctrlHeld = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        if (ViewModel.IsSnapEnabled && !ctrlHeld)
        {
            timeSeconds = ViewModel.SnapToGrid(timeSeconds);
        }

        ViewModel.PlayheadPosition = timeSeconds;
        RenderTimeline();

        // Update lights to match new playhead position
        await ViewModel.UpdateLightsForPlayheadAsync();

        e.Handled = true;
    }

    private void TimelineCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        // Skip hover tracking during drag operations
        if (_isDraggingKeyframe || _isDraggingPlayhead)
            return;

        // Guard against uninitialized state
        if (ViewModel?.Tracks == null || ViewModel.Tracks.Count == 0)
            return;

        var pointerPoint = e.GetCurrentPoint(TimelineCanvas);
        var point = new Vector2((float)pointerPoint.Position.X, (float)pointerPoint.Position.Y);

        // Calculate hit test parameters
        var playheadX = GradientTrackRenderer.LeftMargin + (float)(ViewModel.PlayheadPosition * ViewModel.ZoomLevel);
        var lightTracksHeight = ViewModel.Tracks.Count * 50f;
        var canvasHeight = lightTracksHeight + ViewModel.EventTracks.Count * 50f;

        var hitResult = HitTestHelper.HitTest(
            TimelineCanvas,
            point,
            playheadX,
            canvasHeight,
            ViewModel.Tracks.ToList(),
            ViewModel.EventTracks.ToList(),
            ViewModel.SelectedKeyframes,
            (float)ViewModel.ZoomLevel);

        // Track state changes
        var oldHoveredKeyframe = _hoveredKeyframe;
        var oldPlayheadHovered = _isPlayheadHovered;

        // Update hover state
        _hoveredKeyframe = hitResult.Type == HitType.Keyframe ? hitResult.Keyframe : null;
        _isPlayheadHovered = hitResult.Type == HitType.Playhead;

        // Invalidate only if state changed
        if (_hoveredKeyframe != oldHoveredKeyframe || _isPlayheadHovered != oldPlayheadHovered)
        {
            _lastHitType = hitResult.Type;
            UpdateCursor(hitResult.Type);
            TimelineCanvas.Invalidate();
        }
        else if (_lastHitType != hitResult.Type)
        {
            // Cursor change needed even if no keyframe/playhead hover change
            _lastHitType = hitResult.Type;
            UpdateCursor(hitResult.Type);
        }
    }

    private void TimelineCanvas_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (_hoveredKeyframe != null || _isPlayheadHovered)
        {
            _hoveredKeyframe = null;
            _isPlayheadHovered = false;
            _lastHitType = HitType.None;
            UpdateCursor(HitType.None);
            TimelineCanvas.Invalidate();
        }
    }

    private void UpdateCursor(HitType hitType)
    {
        var cursor = hitType switch
        {
            HitType.Keyframe => new CoreCursor(CoreCursorType.Hand, 0),
            HitType.Playhead => new CoreCursor(CoreCursorType.Hand, 0),
            _ => new CoreCursor(CoreCursorType.Arrow, 0)
        };

        CanvasControlCursorHelper.SetCursor(TimelineCanvas, cursor);
    }



    private (int r, int g, int b) HueColorToRgb(HueWindows.Core.Models.HueColor color)
    {
        // Simplified xy to RGB conversion
        // This is an approximation - real conversion requires the light's gamut
        var x = color.X;
        var y = color.Y;
        var z = 1.0 - x - y;

        var Y = 1.0; // Brightness
        var X = (Y / y) * x;
        var Z = (Y / y) * z;

        // XYZ to RGB
        var r = X * 1.656492 - Y * 0.354851 - Z * 0.255038;
        var g = -X * 0.707196 + Y * 1.655397 + Z * 0.036152;
        var b = X * 0.051713 - Y * 0.121364 + Z * 1.011530;

        // Clamp and convert to 0-255
        r = Math.Max(0, Math.Min(1, r));
        g = Math.Max(0, Math.Min(1, g));
        b = Math.Max(0, Math.Min(1, b));

        return ((int)(r * 255), (int)(g * 255), (int)(b * 255));
    }


    private void TimelineCanvas_KeyframeDrag(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDraggingKeyframe || _draggingKeyframe == null || _draggingTrack == null)
            return;

        var point = e.GetCurrentPoint(TimelineCanvas);
        var timeSeconds = (point.Position.X - GradientTrackRenderer.LeftMargin) / ViewModel.ZoomLevel;

        // Clamp to valid range
        if (timeSeconds < 0) timeSeconds = 0;
        if (timeSeconds > ViewModel.DurationSeconds) timeSeconds = ViewModel.DurationSeconds;

        // Apply snap if enabled and Ctrl not held
        var ctrlHeld = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        if (ViewModel.IsSnapEnabled && !ctrlHeld)
        {
            timeSeconds = ViewModel.SnapToGrid(timeSeconds);
        }

        // Update keyframe time
        _draggingKeyframe.TimeSeconds = timeSeconds;

        // Re-sort keyframes in track
        var sorted = _draggingTrack.Keyframes.OrderBy(k => k.TimeSeconds).ToList();
        _draggingTrack.Keyframes.Clear();
        foreach (var kf in sorted)
        {
            _draggingTrack.Keyframes.Add(kf);
        }

        // Update UI
        RenderTimeline();
        KeyframeTimeText.Text = $"Keyframe @ {timeSeconds:F1}s";
    }

    private void TimelineCanvas_KeyframeDragEnd(object sender, PointerRoutedEventArgs e)
    {
        if (_isDraggingKeyframe)
        {
            _isDraggingKeyframe = false;
            _draggingKeyframe = null;
            _draggingTrack = null;
            TimelineCanvas.ReleasePointerCapture(e.Pointer);

            // Restore hover cursor
            UpdateCursor(_lastHitType);

            // Unwire events
            TimelineCanvas.PointerMoved -= TimelineCanvas_KeyframeDrag;
            TimelineCanvas.PointerReleased -= TimelineCanvas_KeyframeDragEnd;
        }
    }


    private async void SelectKeyframe(KeyframeViewModel keyframe)
    {
        _isUpdatingPanel = true;

        // Update both SelectedKeyframe and SelectedKeyframes collection
        ViewModel.SelectedKeyframes.Clear();
        ViewModel.SelectedKeyframes.Add(keyframe);
        ViewModel.SelectedKeyframe = keyframe;
        SidePanel.Visibility = Visibility.Visible;
        KeyframeTimeText.Text = $"Keyframe @ {keyframe.TimeSeconds:F1}s";

        // Update color picker (brightness is set via internal slider)
        KeyframeColorPicker.SelectedColor = keyframe.Color;
        KeyframeColorPicker.SetBrightness(keyframe.Brightness);

        // Update transition combo
        var transitionName = keyframe.Transition.ToString();
        foreach (ComboBoxItem item in TransitionComboBox.Items)
        {
            if (item.Tag?.ToString() == transitionName)
            {
                TransitionComboBox.SelectedItem = item;
                break;
            }
        }

        _isUpdatingPanel = false;

        // Live preview the selected keyframe on the light
        await ViewModel.UpdateLightForKeyframeAsync(keyframe);
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack)
        {
            Frame.GoBack();
        }
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var result = await ViewModel.SaveSceneAsync();

        if (result.IsSuccess)
        {
            // Show success notification
            var dialog = new ContentDialog
            {
                Title = "Scene Saved",
                Content = $"'{ViewModel.SceneName}' has been saved to your scene library.",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
        else
        {
            // Show error
            var dialog = new ContentDialog
            {
                Title = "Save Failed",
                Content = result.Error ?? "An error occurred while saving the scene.",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
    }

    private void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.IsPlaying = !ViewModel.IsPlaying;

        if (ViewModel.IsPlaying)
        {
            // If at the end, restart from beginning
            if (ViewModel.PlayheadPosition >= ViewModel.DurationSeconds)
            {
                ViewModel.PlayheadPosition = 0;
                RenderTimeline();
            }
            _lastFrameTime = DateTime.Now;
            _lastLightUpdateTime = DateTime.Now;
            InitializeEventPlayback();
            _playbackTimer?.Start();
        }
        else
        {
            _playbackTimer?.Stop();
        }

        UpdatePlayButtonState();
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.IsPlaying = false;
        ViewModel.PlayheadPosition = 0;
        _playbackTimer?.Stop();
        UpdatePlayButtonState();
        RenderTimeline();
    }

    private void DurationNumberBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (ViewModel == null || double.IsNaN(args.NewValue))
            return;

        // Re-render the timeline with new duration
        RenderTimeline();
        UpdateTimeDisplay();
    }

    private void ZoomSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (ViewModel == null)
            return;

        // Re-render the timeline with new zoom level
        RenderTimeline();
    }

    private void TimelineCanvas_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (ViewModel == null)
            return;

        var point = e.GetCurrentPoint(TimelineCanvas);
        var delta = point.Properties.MouseWheelDelta;

        // Check if Ctrl is held for finer zoom control
        var ctrlHeld = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        // Zoom in/out based on wheel direction
        var zoomChange = ctrlHeld ? (delta > 0 ? 5 : -5) : (delta > 0 ? 10 : -10);
        var oldZoom = ViewModel.ZoomLevel;
        var newZoom = oldZoom + zoomChange;

        // Clamp to slider range
        if (newZoom < 20) newZoom = 20;
        if (newZoom > 200) newZoom = 200;

        if (Math.Abs(newZoom - oldZoom) < 0.1)
        {
            e.Handled = true;
            return; // No change
        }

        // Calculate the time position under the mouse cursor before zoom
        var mouseX = point.Position.X;
        var timeUnderMouse = mouseX / oldZoom;

        // Apply new zoom
        ViewModel.ZoomLevel = newZoom;

        // Calculate how much we need to scroll to keep the same time under the mouse
        // This creates a "zoom to cursor" effect
        var newMouseX = timeUnderMouse * newZoom;
        var scrollDelta = newMouseX - mouseX;

        // Update the zoom slider to reflect the change
        if (ZoomSlider != null)
        {
            ZoomSlider.Value = newZoom;
        }

        RenderTimeline();

        // If the canvas is inside a ScrollViewer, adjust the scroll position
        // to keep the timeline centered on the mouse cursor
        if (sender is Microsoft.UI.Xaml.FrameworkElement element)
        {
            var scrollViewer = FindParentScrollViewer(element);
            if (scrollViewer != null && scrollDelta != 0)
            {
                scrollViewer.ChangeView(scrollViewer.HorizontalOffset + scrollDelta, null, null, false);
            }
        }

        e.Handled = true;
    }

    private ScrollViewer? FindParentScrollViewer(Microsoft.UI.Xaml.DependencyObject element)
    {
        var parent = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(element);
        while (parent != null)
        {
            if (parent is ScrollViewer scrollViewer)
                return scrollViewer;
            parent = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(parent);
        }
        return null;
    }

    private void UpdatePlayButtonState()
    {
        if (ViewModel.IsPlaying)
        {
            PlayButtonIcon.Glyph = "\uE769"; // Pause icon
            PlayButtonText.Text = "Pause";
        }
        else
        {
            PlayButtonIcon.Glyph = "\uE768"; // Play icon
            PlayButtonText.Text = "Play";
        }
    }

    private void AddKeyframe_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.Tracks == null || ViewModel.Tracks.Count == 0)
            return;

        // Add a keyframe at the playhead position for all tracks
        var timeSeconds = ViewModel.PlayheadPosition;

        // Clamp to valid range
        if (timeSeconds < 0) timeSeconds = 0;
        if (timeSeconds > ViewModel.DurationSeconds) timeSeconds = ViewModel.DurationSeconds;

        foreach (var track in ViewModel.Tracks)
        {
            // Check if a keyframe already exists at this time
            var existingKeyframe = track.Keyframes.FirstOrDefault(k => Math.Abs(k.TimeSeconds - timeSeconds) < 0.1);
            if (existingKeyframe == null)
            {
                ViewModel.AddKeyframe(track, timeSeconds);
            }
        }

        RenderTimeline();
    }

    private void TimelineCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (ViewModel?.Tracks == null || ViewModel.Tracks.Count == 0)
        {
            SidePanel.Visibility = Visibility.Collapsed;
            if (ViewModel != null) ViewModel.SelectedKeyframe = null;
            return;
        }

        var pointerPoint = e.GetCurrentPoint(TimelineCanvas);
        var point = new Vector2((float)pointerPoint.Position.X, (float)pointerPoint.Position.Y);

        // Hit test to determine what was clicked
        var playheadX = GradientTrackRenderer.LeftMargin + (float)(ViewModel.PlayheadPosition * ViewModel.ZoomLevel);
        var lightTracksHeight = ViewModel.Tracks.Count * 50f;
        var canvasHeight = lightTracksHeight + ViewModel.EventTracks.Count * 50f;

        var hitResult = HitTestHelper.HitTest(
            TimelineCanvas,
            point,
            playheadX,
            canvasHeight,
            ViewModel.Tracks.ToList(),
            ViewModel.EventTracks.ToList(),
            ViewModel.SelectedKeyframes,
            (float)ViewModel.ZoomLevel);

        var props = pointerPoint.Properties;
        var shiftHeld = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        var ctrlHeld = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        switch (hitResult.Type)
        {
            case HitType.Playhead:
                if (props.IsLeftButtonPressed)
                {
                    StartPlayheadDrag(e);
                    e.Handled = true;
                }
                break;

            case HitType.Keyframe:
                HandleKeyframeClick(hitResult.Keyframe!, hitResult.Track!, props, shiftHeld, e);
                e.Handled = true;
                break;

            case HitType.EventTrack:
                if (props.IsLeftButtonPressed)
                {
                    SelectEventTrack(hitResult.EventTrack!);
                    e.Handled = true;
                }
                break;

            case HitType.Track:
                HandleTrackClick(hitResult.Track!, hitResult.TrackIndex, point, props, ctrlHeld, e);
                e.Handled = true;
                break;

            case HitType.None:
                // Clicked empty space - close panels
                SidePanel.Visibility = Visibility.Collapsed;
                ViewModel.SelectedKeyframe = null;
                break;
        }
    }

    private void TimelineCanvas_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (ViewModel?.Tracks == null || ViewModel.Tracks.Count == 0)
            return;

        var position = e.GetPosition(TimelineCanvas);
        var point = new Vector2((float)position.X, (float)position.Y);

        // Hit test to find keyframe under cursor
        var playheadX = GradientTrackRenderer.LeftMargin + (float)(ViewModel.PlayheadPosition * ViewModel.ZoomLevel);
        var lightTracksHeight = ViewModel.Tracks.Count * 50f;
        var canvasHeight = lightTracksHeight + ViewModel.EventTracks.Count * 50f;

        var hitResult = HitTestHelper.HitTest(
            TimelineCanvas,
            point,
            playheadX,
            canvasHeight,
            ViewModel.Tracks.ToList(),
            ViewModel.EventTracks.ToList(),
            ViewModel.SelectedKeyframes,
            (float)ViewModel.ZoomLevel);

        if (hitResult.Type == HitType.Keyframe && hitResult.Keyframe != null && hitResult.Track != null)
        {
            // Delete keyframe (if track has > 2 keyframes)
            if (hitResult.Track.Keyframes.Count > 2)
            {
                ViewModel.DeleteKeyframeCommand.Execute(hitResult.Keyframe);
                SidePanel.Visibility = Visibility.Collapsed;
                ViewModel.SelectedKeyframe = null;
                RenderTimeline();
            }
            e.Handled = true;
        }
    }

    private void StartPlayheadDrag(PointerRoutedEventArgs e)
    {
        _isDraggingPlayhead = true;
        TimelineCanvas.CapturePointer(e.Pointer);

        // Set grabbing cursor
        CanvasControlCursorHelper.SetCursor(TimelineCanvas, new CoreCursor(CoreCursorType.SizeAll, 0));

        TimelineCanvas.PointerMoved += TimelineCanvas_PlayheadDrag;
        TimelineCanvas.PointerReleased += TimelineCanvas_PlayheadDragEnd;
    }

    private async void TimelineCanvas_PlayheadDrag(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDraggingPlayhead)
            return;

        var point = e.GetCurrentPoint(TimelineCanvas);
        var timeSeconds = (point.Position.X - GradientTrackRenderer.LeftMargin) / ViewModel.ZoomLevel;

        // Clamp to valid range
        timeSeconds = Math.Max(0, Math.Min(ViewModel.DurationSeconds, timeSeconds));

        // Apply snap unless Ctrl held
        var ctrlHeld = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        if (ViewModel.IsSnapEnabled && !ctrlHeld)
        {
            timeSeconds = ViewModel.SnapToGrid(timeSeconds);
        }

        ViewModel.PlayheadPosition = timeSeconds;
        UpdatePlayheadPosition();

        // Update lights during scrubbing (throttled in ViewModel)
        await ViewModel.UpdateLightsForPlayheadAsync();
    }

    private void TimelineCanvas_PlayheadDragEnd(object sender, PointerRoutedEventArgs e)
    {
        if (_isDraggingPlayhead)
        {
            _isDraggingPlayhead = false;
            TimelineCanvas.ReleasePointerCapture(e.Pointer);

            // Restore hover cursor
            UpdateCursor(_lastHitType);

            TimelineCanvas.PointerMoved -= TimelineCanvas_PlayheadDrag;
            TimelineCanvas.PointerReleased -= TimelineCanvas_PlayheadDragEnd;
        }
    }

    private void HandleKeyframeClick(
        KeyframeViewModel keyframe,
        TrackViewModel track,
        Microsoft.UI.Input.PointerPointProperties props,
        bool shiftHeld,
        PointerRoutedEventArgs e)
    {
        if (props.IsRightButtonPressed)
        {
            // Right-click: delete (if track has > 2 keyframes)
            if (track.Keyframes.Count > 2)
            {
                ViewModel.DeleteKeyframeCommand.Execute(keyframe);
                SidePanel.Visibility = Visibility.Collapsed;
                ViewModel.SelectedKeyframe = null;
                RenderTimeline();
            }
            return;
        }

        if (props.IsLeftButtonPressed)
        {
            ViewModel.ToggleKeyframeSelection(keyframe, shiftHeld);

            if (!shiftHeld)
            {
                // Start dragging
                _isDraggingKeyframe = true;
                _draggingKeyframe = keyframe;
                _draggingTrack = track;

                TimelineCanvas.CapturePointer(e.Pointer);

                // Set grabbing cursor
                CanvasControlCursorHelper.SetCursor(TimelineCanvas, new CoreCursor(CoreCursorType.SizeAll, 0));

                TimelineCanvas.PointerMoved += TimelineCanvas_KeyframeDrag;
                TimelineCanvas.PointerReleased += TimelineCanvas_KeyframeDragEnd;

                SelectKeyframe(keyframe);
            }
            else
            {
                // Multi-select mode
                if (ViewModel.SelectedKeyframes.Count == 1)
                {
                    SelectKeyframe(keyframe);
                }
                else
                {
                    SidePanel.Visibility = Visibility.Collapsed;
                }
                RenderTimeline();
            }
        }
    }

    private void HandleTrackClick(
        TrackViewModel track,
        int trackIndex,
        Vector2 point,
        Microsoft.UI.Input.PointerPointProperties props,
        bool ctrlHeld,
        PointerRoutedEventArgs e)
    {
        if (!props.IsLeftButtonPressed)
            return;

        // Close any open panels first
        if (SidePanel.Visibility == Visibility.Visible)
        {
            SidePanel.Visibility = Visibility.Collapsed;
            ViewModel.SelectedKeyframe = null;
        }
        if (EventSidePanel.Visibility == Visibility.Visible)
        {
            EventSidePanel.Visibility = Visibility.Collapsed;
            ViewModel.SelectedEventTrack = null;
        }

        // Calculate time from click position
        var timeSeconds = (point.X - GradientTrackRenderer.LeftMargin) / ViewModel.ZoomLevel;
        timeSeconds = Math.Max(0, Math.Min(ViewModel.DurationSeconds, timeSeconds));

        // Apply snap if enabled and Ctrl not held
        if (ViewModel.IsSnapEnabled && !ctrlHeld)
        {
            timeSeconds = ViewModel.SnapToGrid(timeSeconds);
        }

        // Create new keyframe
        ViewModel.AddKeyframe(track, timeSeconds);
        RenderTimeline();

        // Select the new keyframe
        var newKeyframe = track.Keyframes.FirstOrDefault(k => Math.Abs(k.TimeSeconds - timeSeconds) < 0.1);
        if (newKeyframe != null)
        {
            SelectKeyframe(newKeyframe);
        }
    }

    private void CloseSidePanel_Click(object sender, RoutedEventArgs e)
    {
        SidePanel.Visibility = Visibility.Collapsed;
        ViewModel.SelectedKeyframe = null;
    }

    private async void GamutColorPicker_ColorChanged(object? sender, HueWindows.Core.Models.HueColor color)
    {
        if (ViewModel.SelectedKeyframe == null || _isUpdatingPanel)
            return;

        // Update keyframe color AND brightness
        ViewModel.SelectedKeyframe.Color = color;

        if (sender is Controls.GamutColorPicker picker)
        {
            ViewModel.SelectedKeyframe.Brightness = picker.Brightness;
        }

        // Mark scene as modified
        ViewModel.HasUnsavedChanges = true;

        // Invalidate timeline to show new color
        TimelineCanvas?.Invalidate();

        // Live preview on light (sends both color and brightness)
        await ViewModel.UpdateLightForKeyframeAsync(ViewModel.SelectedKeyframe);
    }


    private void TransitionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel?.SelectedKeyframe != null && TransitionComboBox.SelectedItem is ComboBoxItem item)
        {
            var tagValue = item.Tag?.ToString();
            if (Enum.TryParse<HueWindows.Core.Models.TransitionStyle>(tagValue, out var transition))
            {
                ViewModel.SelectedKeyframe.Transition = transition;
                ViewModel.HasUnsavedChanges = true;

                // Immediately update gradient bar to show new transition
                RenderTimeline();
            }
        }
    }

    private void DuplicateKeyframe_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedKeyframe != null)
        {
            ViewModel.DuplicateKeyframeCommand.Execute(ViewModel.SelectedKeyframe);
            RenderTimeline();
            if (ViewModel.SelectedKeyframe != null)
            {
                SelectKeyframe(ViewModel.SelectedKeyframe);
            }
        }
    }

    private void DeleteKeyframe_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedKeyframe != null)
        {
            ViewModel.DeleteKeyframeCommand.Execute(ViewModel.SelectedKeyframe);
            SidePanel.Visibility = Visibility.Collapsed;
            RenderTimeline();
        }
    }

    #region Event Track Handlers

    private void InitializeEventPlayback()
    {
        _nextEventFireTimes.Clear();
        _eventPulses.Clear();

        foreach (var eventTrack in ViewModel.EventTracks)
        {
            var (min, max) = eventTrack.GetInterval();
            var nextFire = _random.NextDouble() * (max - min) + min;
            _nextEventFireTimes[eventTrack.Id] = nextFire;
        }
    }

    private async void CheckEventTriggers()
    {
        if (ViewModel.EventTracks.Count == 0 || ViewModel.Tracks.Count == 0)
            return;

        var currentTime = ViewModel.PlayheadPosition;
        const double trackHeight = 50;
        var lightTracksHeight = ViewModel.Tracks.Count * trackHeight;

        foreach (var eventTrack in ViewModel.EventTracks)
        {
            if (!_nextEventFireTimes.TryGetValue(eventTrack.Id, out var nextFireTime))
                continue;

            if (currentTime >= nextFireTime)
            {
                // Pick a random light track
                var targetTrackIndex = _random.Next(ViewModel.Tracks.Count);

                // Show visual pulse on event track row
                ShowEventPulse(eventTrack, lightTracksHeight, trackHeight);

                // Show visual indicator on the targeted light track
                ShowLightTrackPulse(targetTrackIndex, trackHeight);

                // Fire the event on that specific light
                _ = ViewModel.FireEventAsync(eventTrack, targetTrackIndex);

                // Schedule next event
                var (min, max) = eventTrack.GetInterval();
                var interval = _random.NextDouble() * (max - min) + min;
                _nextEventFireTimes[eventTrack.Id] = currentTime + interval;
            }
        }
    }

    // TODO (Plan 03): Migrate to Win2D rendering
    private void ShowLightTrackPulse(int trackIndex, double trackHeight)
    {
        // Temporarily disabled - will be migrated to Win2D in Plan 03
    }

    // TODO (Plan 03): Migrate to Win2D rendering
    private void ShowEventPulse(EventTrackViewModel eventTrack, double lightTracksHeight, double trackHeight)
    {
        // Temporarily disabled - will be migrated to Win2D in Plan 03
    }

    private void AddLightningTrack_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.AddEventTrackCommand.Execute(EventPreset.LightningFlash);
        RenderTimeline();
    }

    private void AddSparkleTrack_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.AddEventTrackCommand.Execute(EventPreset.Sparkle);
        RenderTimeline();
    }

    private void AddCandleTrack_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.AddEventTrackCommand.Execute(EventPreset.CandleFlicker);
        RenderTimeline();
    }

    private void CloseEventSidePanel_Click(object sender, RoutedEventArgs e)
    {
        EventSidePanel.Visibility = Visibility.Collapsed;
        ViewModel.SelectedEventTrack = null;
    }

    private void EventPresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel?.SelectedEventTrack != null && EventPresetComboBox.SelectedItem is ComboBoxItem item)
        {
            var tagValue = item.Tag?.ToString();
            if (Enum.TryParse<EventPreset>(tagValue, out var preset))
            {
                ViewModel.SelectedEventTrack.Preset = preset;
                UpdateEventTrackPanel(ViewModel.SelectedEventTrack);
                RenderTimeline();
            }
        }
    }

    private void FrequencySlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (ViewModel?.SelectedEventTrack != null)
        {
            ViewModel.SelectedEventTrack.Frequency = e.NewValue;
        }

        // Update frequency label
        var label = e.NewValue switch
        {
            < 0.33 => "Rare",
            < 0.66 => "Medium",
            _ => "Frequent"
        };
        if (FrequencyValueText != null)
            FrequencyValueText.Text = label;
    }

    private void DeleteEventTrack_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedEventTrack != null)
        {
            ViewModel.DeleteEventTrackCommand.Execute(ViewModel.SelectedEventTrack);
            EventSidePanel.Visibility = Visibility.Collapsed;
            RenderTimeline();
        }
    }

    private void SelectEventTrack(EventTrackViewModel eventTrack)
    {
        ViewModel.SelectEventTrackCommand.Execute(eventTrack);
        SidePanel.Visibility = Visibility.Collapsed; // Hide keyframe panel
        EventSidePanel.Visibility = Visibility.Visible;
        UpdateEventTrackPanel(eventTrack);
    }

    private void UpdateEventTrackPanel(EventTrackViewModel eventTrack)
    {
        EventTrackNameText.Text = eventTrack.DisplayName;
        EventTrackIcon.Glyph = eventTrack.IconGlyph;
        FrequencySlider.Value = eventTrack.Frequency;

        // Select the right preset in combo
        foreach (ComboBoxItem item in EventPresetComboBox.Items)
        {
            if (item.Tag?.ToString() == eventTrack.Preset.ToString())
            {
                EventPresetComboBox.SelectedItem = item;
                break;
            }
        }
    }

    #endregion

    #region File Operations

    /// <summary>
    /// Checks for unsaved changes and prompts user to confirm discard.
    /// </summary>
    private async Task<bool> ConfirmDiscardChangesAsync()
    {
        if (!ViewModel.HasUnsavedChanges)
            return true;

        var dialog = new ContentDialog
        {
            Title = "Unsaved Changes",
            Content = "You have unsaved changes. Discard and continue?",
            PrimaryButtonText = "Discard",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    private async void NewScene_Click(object sender, RoutedEventArgs e)
    {
        if (!await ConfirmDiscardChangesAsync())
            return;

        ViewModel.NewScene();
        RenderTimeline();
        UpdateEmptyState();
    }

    private async void OpenScene_Click(object sender, RoutedEventArgs e)
    {
        if (!await ConfirmDiscardChangesAsync())
            return;

        // Get list of user scenes
        var scenesResult = await ViewModel.GetUserScenesAsync();
        if (scenesResult.IsFailure || scenesResult.Value == null || scenesResult.Value.Count == 0)
        {
            var noScenesDialog = new ContentDialog
            {
                Title = "No Scenes",
                Content = "You don't have any saved scenes yet. Create one first!",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await noScenesDialog.ShowAsync();
            return;
        }

        // Create scene picker dialog with simple list
        var listView = new ListView
        {
            ItemsSource = scenesResult.Value,
            SelectionMode = ListViewSelectionMode.Single,
            MaxHeight = 300,
            DisplayMemberPath = "Name"
        };

        var dialog = new ContentDialog
        {
            Title = "Open Scene",
            Content = listView,
            PrimaryButtonText = "Open",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && listView.SelectedItem is AnimatedSceneModel selectedScene)
        {
            var loadResult = await ViewModel.LoadSceneAsync(selectedScene.Id);
            if (loadResult.IsFailure)
            {
                var errorDialog = new ContentDialog
                {
                    Title = "Load Failed",
                    Content = loadResult.Error ?? "Failed to load the scene.",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await errorDialog.ShowAsync();
            }
            else
            {
                RenderTimeline();
                UpdateEmptyState();
            }
        }
    }

    private async void ImportFile_Click(object sender, RoutedEventArgs e)
    {
        if (!await ConfirmDiscardChangesAsync())
            return;

        var picker = new FileOpenPicker();
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.FileTypeFilter.Add(".json");

        // Initialize picker with window handle
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        if (file == null)
            return;

        try
        {
            var json = await Windows.Storage.FileIO.ReadTextAsync(file);
            var importResult = await ViewModel.ImportFromJsonAsync(json);

            if (importResult.IsFailure)
            {
                var errorDialog = new ContentDialog
                {
                    Title = "Import Failed",
                    Content = importResult.Error ?? "Failed to import the scene.",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await errorDialog.ShowAsync();
            }
            else
            {
                RenderTimeline();
                UpdateEmptyState();

                var successDialog = new ContentDialog
                {
                    Title = "Scene Imported",
                    Content = $"'{ViewModel.SceneName}' has been imported. Don't forget to save it!",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await successDialog.ShowAsync();
            }
        }
        catch (Exception ex)
        {
            var errorDialog = new ContentDialog
            {
                Title = "Import Failed",
                Content = $"Error reading file: {ex.Message}",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await errorDialog.ShowAsync();
        }
    }

    private async void ExportFile_Click(object sender, RoutedEventArgs e)
    {
        var exportResult = ViewModel.ExportToJson();
        if (exportResult.IsFailure)
        {
            var errorDialog = new ContentDialog
            {
                Title = "Export Failed",
                Content = exportResult.Error ?? "Failed to export the scene.",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await errorDialog.ShowAsync();
            return;
        }

        var picker = new FileSavePicker();
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.FileTypeChoices.Add("JSON File", new List<string> { ".json" });
        picker.SuggestedFileName = $"{ViewModel.SceneName.Replace(" ", "_").ToLowerInvariant()}.json";

        // Initialize picker with window handle
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSaveFileAsync();
        if (file == null)
            return;

        try
        {
            await Windows.Storage.FileIO.WriteTextAsync(file, exportResult.Value);

            var successDialog = new ContentDialog
            {
                Title = "Scene Exported",
                Content = $"Scene saved to {file.Name}",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await successDialog.ShowAsync();
        }
        catch (Exception ex)
        {
            var errorDialog = new ContentDialog
            {
                Title = "Export Failed",
                Content = $"Error writing file: {ex.Message}",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await errorDialog.ShowAsync();
        }
    }

    private async void CopyJson_Click(object sender, RoutedEventArgs e)
    {
        var exportResult = ViewModel.ExportToJson();
        if (exportResult.IsFailure)
        {
            var errorDialog = new ContentDialog
            {
                Title = "Copy Failed",
                Content = exportResult.Error ?? "Failed to export the scene.",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await errorDialog.ShowAsync();
            return;
        }

        var dataPackage = new DataPackage();
        dataPackage.SetText(exportResult.Value);
        Clipboard.SetContent(dataPackage);

        // Show brief confirmation (could use a teaching tip or info bar instead)
        var successDialog = new ContentDialog
        {
            Title = "Copied",
            Content = "Scene JSON copied to clipboard.",
            CloseButtonText = "OK",
            XamlRoot = this.XamlRoot
        };
        await successDialog.ShowAsync();
    }

    #endregion
}
