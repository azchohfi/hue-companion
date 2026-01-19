using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Extensions.DependencyInjection;
using HueWindows.Core.ViewModels;
using HueWindows.Core.Models;
using Windows.UI;
using Windows.Storage.Pickers;
using Windows.ApplicationModel.DataTransfer;

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

    // Cached playhead elements for efficient updates
    private Microsoft.UI.Xaml.Shapes.Line? _playheadHitArea;
    private Microsoft.UI.Xaml.Shapes.Line? _playheadLine;
    private Microsoft.UI.Xaml.Shapes.Polygon? _playheadHandle;
    private Microsoft.UI.Xaml.Shapes.Polygon? _rulerPlayheadMarker;

    // Event track playback state
    private readonly Dictionary<string, double> _nextEventFireTimes = new();
    private readonly Dictionary<string, Microsoft.UI.Xaml.Shapes.Ellipse> _eventPulses = new();
    private readonly Random _random = new();

    private string? _sceneIdToLoad;

    public SceneBuilderPage()
    {
        this.InitializeComponent();
        ViewModel = App.Services.GetRequiredService<SceneBuilderViewModel>();
        Loaded += Page_Loaded;
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
                return;
            }
            else
            {
                ViewModel.PlayheadPosition = ViewModel.DurationSeconds;
                ViewModel.IsPlaying = false;
                _playbackTimer?.Stop();
                UpdatePlayButtonState();
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

    private void RoomComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel == null) return;
        UpdateEmptyState();
        RenderTimeline();
    }

    private void RenderTimeline()
    {
        // Clear existing keyframes
        KeyframeCanvas.Children.Clear();

        if (ViewModel.SelectedRoom == null || ViewModel.Tracks.Count == 0)
            return;

        // Render time ruler ticks
        RenderTimeRuler();

        const double trackHeight = 50;
        var lightTracksHeight = ViewModel.Tracks.Count * trackHeight;
        var eventTracksHeight = ViewModel.EventTracks.Count * trackHeight;
        var canvasHeight = lightTracksHeight + eventTracksHeight;

        // Render track separator lines for light tracks
        for (int i = 1; i < ViewModel.Tracks.Count; i++)
        {
            var separator = new Microsoft.UI.Xaml.Shapes.Line
            {
                X1 = 0,
                Y1 = i * trackHeight,
                X2 = ViewModel.DurationSeconds * ViewModel.ZoomLevel,
                Y2 = i * trackHeight,
                Stroke = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Windows.UI.Color.FromArgb(40, 255, 255, 255)),
                StrokeThickness = 1
            };
            KeyframeCanvas.Children.Add(separator);
        }

        // Render loop region highlighting (background layer)
        RenderLoopRegion(canvasHeight);

        // Render snap grid lines
        RenderGridLines(canvasHeight);

        // Render keyframes for each track
        for (int trackIndex = 0; trackIndex < ViewModel.Tracks.Count; trackIndex++)
        {
            var track = ViewModel.Tracks[trackIndex];
            var y = trackIndex * trackHeight + trackHeight / 2;

            foreach (var keyframe in track.Keyframes)
            {
                RenderKeyframe(keyframe, track, keyframe.TimeSeconds * ViewModel.ZoomLevel, y);
            }
        }

        // Render event tracks
        RenderEventTracks(lightTracksHeight, trackHeight);

        // Render playhead
        RenderPlayhead(canvasHeight);

        // Set canvas size - add fixed buffer beyond duration for grid extension
        var durationWidth = ViewModel.DurationSeconds * ViewModel.ZoomLevel;
        // Add a fixed buffer (500px) beyond the loop region so grid extends visually
        KeyframeCanvas.Width = durationWidth + 500;
        KeyframeCanvas.Height = canvasHeight;

        // Update time display
        UpdateTimeDisplay();
    }

    private void RenderPlayhead(double height)
    {
        var x = ViewModel.PlayheadPosition * ViewModel.ZoomLevel;

        // Invisible wider hit area for easier dragging
        _playheadHitArea = new Microsoft.UI.Xaml.Shapes.Line
        {
            X1 = x,
            Y1 = 0,
            X2 = x,
            Y2 = height,
            Stroke = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Windows.UI.Color.FromArgb(0, 0, 0, 0)), // Transparent
            StrokeThickness = 12, // Wide hit area
            Tag = "PlayheadHitArea"
        };
        _playheadHitArea.PointerPressed += PlayheadHandle_PointerPressed;
        KeyframeCanvas.Children.Add(_playheadHitArea);

        // Visible playhead line
        _playheadLine = new Microsoft.UI.Xaml.Shapes.Line
        {
            X1 = x,
            Y1 = 0,
            X2 = x,
            Y2 = height,
            Stroke = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Windows.UI.Color.FromArgb(255, 255, 100, 100)),
            StrokeThickness = 2,
            IsHitTestVisible = false // Let the hit area handle input
        };
        KeyframeCanvas.Children.Add(_playheadLine);

        // Playhead handle (triangle at top) - make it draggable
        _playheadHandle = new Microsoft.UI.Xaml.Shapes.Polygon
        {
            Points = new Microsoft.UI.Xaml.Media.PointCollection
            {
                new Windows.Foundation.Point(x - 8, 0),
                new Windows.Foundation.Point(x + 8, 0),
                new Windows.Foundation.Point(x, 12)
            },
            Fill = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Windows.UI.Color.FromArgb(255, 255, 100, 100)),
            Tag = "PlayheadHandle"
        };
        _playheadHandle.PointerPressed += PlayheadHandle_PointerPressed;
        KeyframeCanvas.Children.Add(_playheadHandle);
    }

    private void RenderEventTracks(double startY, double trackHeight)
    {
        if (ViewModel.EventTracks.Count == 0)
            return;

        var width = ViewModel.DurationSeconds * ViewModel.ZoomLevel;

        for (int i = 0; i < ViewModel.EventTracks.Count; i++)
        {
            var eventTrack = ViewModel.EventTracks[i];
            var y = startY + i * trackHeight;

            // Draw background for event track (slightly different color)
            var background = new Microsoft.UI.Xaml.Shapes.Rectangle
            {
                Width = width,
                Height = trackHeight,
                Fill = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Windows.UI.Color.FromArgb(20, 255, 200, 100)), // Subtle warm tint
                IsHitTestVisible = true,
                Tag = eventTrack
            };
            background.PointerPressed += EventTrack_PointerPressed;
            Canvas.SetLeft(background, 0);
            Canvas.SetTop(background, y);
            KeyframeCanvas.Children.Add(background);

            // Draw separator line
            var separator = new Microsoft.UI.Xaml.Shapes.Line
            {
                X1 = 0,
                Y1 = y,
                X2 = width,
                Y2 = y,
                Stroke = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Windows.UI.Color.FromArgb(60, 255, 200, 100)),
                StrokeThickness = 1,
                IsHitTestVisible = false
            };
            KeyframeCanvas.Children.Add(separator);

            // Draw event pattern indicator (dashed line in the middle)
            var patternLine = new Microsoft.UI.Xaml.Shapes.Line
            {
                X1 = 0,
                Y1 = y + trackHeight / 2,
                X2 = width,
                Y2 = y + trackHeight / 2,
                Stroke = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Windows.UI.Color.FromArgb(80, 255, 200, 100)),
                StrokeThickness = 2,
                StrokeDashArray = new Microsoft.UI.Xaml.Media.DoubleCollection { 4, 4 },
                IsHitTestVisible = false
            };
            KeyframeCanvas.Children.Add(patternLine);
        }
    }

    private void EventTrack_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Microsoft.UI.Xaml.Shapes.Rectangle rect &&
            rect.Tag is EventTrackViewModel eventTrack)
        {
            SelectEventTrack(eventTrack);
            e.Handled = true;
        }
    }

    private void RenderLoopRegion(double height)
    {
        var loopEndX = ViewModel.DurationSeconds * ViewModel.ZoomLevel;

        // Draw tinted background for the loop region (accent color at ~10% opacity)
        var loopBackground = new Microsoft.UI.Xaml.Shapes.Rectangle
        {
            Width = loopEndX,
            Height = height,
            Fill = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Windows.UI.Color.FromArgb(18, 96, 165, 250)), // Subtle blue tint ~7% opacity
            IsHitTestVisible = false
        };
        Canvas.SetLeft(loopBackground, 0);
        Canvas.SetTop(loopBackground, 0);
        KeyframeCanvas.Children.Add(loopBackground);

        // Draw loop end boundary line (thicker, accent color)
        var loopEndLine = new Microsoft.UI.Xaml.Shapes.Line
        {
            X1 = loopEndX,
            Y1 = 0,
            X2 = loopEndX,
            Y2 = height,
            Stroke = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Windows.UI.Color.FromArgb(120, 96, 165, 250)), // Accent blue
            StrokeThickness = 2,
            IsHitTestVisible = false
        };
        KeyframeCanvas.Children.Add(loopEndLine);
    }

    private void RenderGridLines(double height)
    {
        var interval = ViewModel.SnapInterval;
        var zoom = ViewModel.ZoomLevel;

        // Grid extends beyond duration with a fixed buffer (matching canvas width calculation)
        var gridExtentSeconds = ViewModel.DurationSeconds + (500 / zoom);

        // Draw vertical grid lines at each snap interval, extending beyond duration
        for (double time = interval; time < gridExtentSeconds; time += interval)
        {
            var x = time * zoom;
            var gridLine = new Microsoft.UI.Xaml.Shapes.Line
            {
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = height,
                Stroke = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Windows.UI.Color.FromArgb(25, 255, 255, 255)),
                StrokeThickness = 1,
                IsHitTestVisible = false
            };
            KeyframeCanvas.Children.Add(gridLine);
        }
    }

    /// <summary>
    /// Efficiently updates just the playhead position without re-rendering everything.
    /// </summary>
    private void UpdatePlayheadPosition()
    {
        var x = ViewModel.PlayheadPosition * ViewModel.ZoomLevel;

        // Update hit area
        if (_playheadHitArea != null)
        {
            _playheadHitArea.X1 = x;
            _playheadHitArea.X2 = x;
        }

        // Update visible line
        if (_playheadLine != null)
        {
            _playheadLine.X1 = x;
            _playheadLine.X2 = x;
        }

        // Update handle
        if (_playheadHandle != null)
        {
            _playheadHandle.Points = new Microsoft.UI.Xaml.Media.PointCollection
            {
                new Windows.Foundation.Point(x - 8, 0),
                new Windows.Foundation.Point(x + 8, 0),
                new Windows.Foundation.Point(x, 12)
            };
        }

        // Update ruler marker
        if (_rulerPlayheadMarker != null)
        {
            _rulerPlayheadMarker.Points = new Microsoft.UI.Xaml.Media.PointCollection
            {
                new Windows.Foundation.Point(x - 5, 20),
                new Windows.Foundation.Point(x + 5, 20),
                new Windows.Foundation.Point(x, 12)
            };
        }

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

    private void PlayheadHandle_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _isDraggingPlayhead = true;
        KeyframeCanvas.CapturePointer(e.Pointer);

        // Wire up move and release events
        KeyframeCanvas.PointerMoved += KeyframeCanvas_PointerMoved;
        KeyframeCanvas.PointerReleased += KeyframeCanvas_PointerReleased;

        e.Handled = true;
    }

    private void KeyframeCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDraggingPlayhead)
            return;

        var point = e.GetCurrentPoint(KeyframeCanvas);
        var timeSeconds = point.Position.X / ViewModel.ZoomLevel;

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

        // Use optimized update instead of full re-render
        UpdatePlayheadPosition();
    }

    private void KeyframeCanvas_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_isDraggingPlayhead)
        {
            _isDraggingPlayhead = false;
            KeyframeCanvas.ReleasePointerCapture(e.Pointer);

            // Unwire events
            KeyframeCanvas.PointerMoved -= KeyframeCanvas_PointerMoved;
            KeyframeCanvas.PointerReleased -= KeyframeCanvas_PointerReleased;
        }
    }

    private void TimeRulerCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (ViewModel == null)
            return;

        var point = e.GetCurrentPoint(TimeRulerCanvas);
        var timeSeconds = point.Position.X / ViewModel.ZoomLevel;

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
        e.Handled = true;
    }

    private void RenderTimeRuler()
    {
        TimeRulerCanvas.Children.Clear();
        var width = ViewModel.DurationSeconds * ViewModel.ZoomLevel;
        TimeRulerCanvas.Width = width;

        // Draw tick marks every second, labels every 5 seconds
        for (int i = 0; i <= (int)ViewModel.DurationSeconds; i++)
        {
            var x = i * ViewModel.ZoomLevel;
            var tickHeight = (i % 5 == 0) ? 12.0 : 6.0;

            var tick = new Microsoft.UI.Xaml.Shapes.Line
            {
                X1 = x,
                Y1 = 20 - tickHeight,
                X2 = x,
                Y2 = 20,
                Stroke = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Microsoft.UI.Colors.Gray),
                StrokeThickness = 1
            };
            TimeRulerCanvas.Children.Add(tick);

            // Add labels every 5 seconds
            if (i % 5 == 0)
            {
                var label = new TextBlock
                {
                    Text = $"{i}s",
                    FontSize = 10,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.Colors.Gray)
                };
                Canvas.SetLeft(label, x - 8);
                Canvas.SetTop(label, 0);
                TimeRulerCanvas.Children.Add(label);
            }
        }

        // Draw playhead marker on ruler
        var playheadX = ViewModel.PlayheadPosition * ViewModel.ZoomLevel;
        _rulerPlayheadMarker = new Microsoft.UI.Xaml.Shapes.Polygon
        {
            Points = new Microsoft.UI.Xaml.Media.PointCollection
            {
                new Windows.Foundation.Point(playheadX - 5, 20),
                new Windows.Foundation.Point(playheadX + 5, 20),
                new Windows.Foundation.Point(playheadX, 12)
            },
            Fill = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Windows.UI.Color.FromArgb(255, 255, 100, 100))
        };
        TimeRulerCanvas.Children.Add(_rulerPlayheadMarker);
    }

    private void RenderKeyframe(KeyframeViewModel keyframe, TrackViewModel track, double x, double y)
    {
        // Convert HueColor to color (approximation)
        var rgb = HueColorToRgb(keyframe.Color);
        var brightness = keyframe.Brightness;
        var color = Microsoft.UI.ColorHelper.FromArgb(255,
            (byte)(rgb.r * brightness),
            (byte)(rgb.g * brightness),
            (byte)(rgb.b * brightness));

        // Check if this keyframe is selected
        var isSelected = ViewModel.SelectedKeyframes.Contains(keyframe);

        // Use constants for selection highlight color
        var selectionColor = Microsoft.UI.ColorHelper.FromArgb(255,
            Constants.AppConstants.Colors.SelectionHighlightR,
            Constants.AppConstants.Colors.SelectionHighlightG,
            Constants.AppConstants.Colors.SelectionHighlightB);

        var circle = new Microsoft.UI.Xaml.Shapes.Ellipse
        {
            Width = isSelected ? 20 : 16,
            Height = isSelected ? 20 : 16,
            Fill = new Microsoft.UI.Xaml.Media.SolidColorBrush(color),
            Stroke = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                isSelected ? selectionColor : Microsoft.UI.Colors.White),
            StrokeThickness = isSelected ? 3 : 2
        };

        var offset = isSelected ? 10 : 8;
        Canvas.SetLeft(circle, x - offset);
        Canvas.SetTop(circle, y - offset);

        // Store references for selection
        circle.Tag = (keyframe, track);
        circle.PointerPressed += Keyframe_PointerPressed;
        circle.RightTapped += Keyframe_RightTapped;

        KeyframeCanvas.Children.Add(circle);
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

    private void Keyframe_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Microsoft.UI.Xaml.Shapes.Ellipse ellipse &&
            ellipse.Tag is (KeyframeViewModel keyframe, TrackViewModel track))
        {
            var props = e.GetCurrentPoint(ellipse).Properties;
            var shiftHeld = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift)
                .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

            if (props.IsRightButtonPressed)
            {
                // Right-click: delete keyframe (if track has more than 2 keyframes)
                if (track.Keyframes.Count > 2)
                {
                    ViewModel.DeleteKeyframeCommand.Execute(keyframe);
                    SidePanel.Visibility = Visibility.Collapsed;
                    ViewModel.SelectedKeyframe = null;
                    RenderTimeline();
                }
                e.Handled = true;
                return;
            }

            if (props.IsLeftButtonPressed)
            {
                // Handle multi-select with Shift key
                ViewModel.ToggleKeyframeSelection(keyframe, shiftHeld);

                if (!shiftHeld)
                {
                    // Start dragging the keyframe
                    _isDraggingKeyframe = true;
                    _draggingKeyframe = keyframe;
                    _draggingTrack = track;

                    // Capture pointer for dragging
                    KeyframeCanvas.CapturePointer(e.Pointer);
                    KeyframeCanvas.PointerMoved += KeyframeCanvas_KeyframeDrag;
                    KeyframeCanvas.PointerReleased += KeyframeCanvas_KeyframeDragEnd;

                    // Select the keyframe
                    SelectKeyframe(keyframe);
                }
                else
                {
                    // Multi-select mode - show side panel if single selection
                    if (ViewModel.SelectedKeyframes.Count == 1)
                    {
                        SelectKeyframe(keyframe);
                    }
                    else
                    {
                        SidePanel.Visibility = Visibility.Collapsed;
                    }
                    RenderTimeline(); // Re-render to show selection highlights
                }
            }
            e.Handled = true;
        }
    }

    private void KeyframeCanvas_KeyframeDrag(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDraggingKeyframe || _draggingKeyframe == null || _draggingTrack == null)
            return;

        var point = e.GetCurrentPoint(KeyframeCanvas);
        var timeSeconds = point.Position.X / ViewModel.ZoomLevel;

        // Clamp to valid range
        if (timeSeconds < 0) timeSeconds = 0;
        if (timeSeconds > ViewModel.DurationSeconds) timeSeconds = ViewModel.DurationSeconds;

        // Apply snap unless Ctrl is held
        var ctrlHeld = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        if (!ctrlHeld)
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

    private void KeyframeCanvas_KeyframeDragEnd(object sender, PointerRoutedEventArgs e)
    {
        if (_isDraggingKeyframe)
        {
            _isDraggingKeyframe = false;
            _draggingKeyframe = null;
            _draggingTrack = null;
            KeyframeCanvas.ReleasePointerCapture(e.Pointer);

            // Unwire events
            KeyframeCanvas.PointerMoved -= KeyframeCanvas_KeyframeDrag;
            KeyframeCanvas.PointerReleased -= KeyframeCanvas_KeyframeDragEnd;
        }
    }

    private void Keyframe_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (sender is Microsoft.UI.Xaml.Shapes.Ellipse ellipse &&
            ellipse.Tag is (KeyframeViewModel keyframe, TrackViewModel track))
        {
            // Delete keyframe on right-click (if track has more than 2 keyframes)
            if (track.Keyframes.Count > 2)
            {
                ViewModel.DeleteKeyframeCommand.Execute(keyframe);
                SidePanel.Visibility = Visibility.Collapsed;
                RenderTimeline();
            }
            e.Handled = true;
        }
    }

    private async void SelectKeyframe(KeyframeViewModel keyframe)
    {
        ViewModel.SelectedKeyframe = keyframe;
        SidePanel.Visibility = Visibility.Visible;
        KeyframeTimeText.Text = $"Keyframe @ {keyframe.TimeSeconds:F1}s";

        // Update color picker
        var rgb = HueColorToRgb(keyframe.Color);
        KeyframeColorPicker.Color = Windows.UI.Color.FromArgb(255,
            (byte)rgb.r, (byte)rgb.g, (byte)rgb.b);

        // Update brightness slider
        BrightnessSlider.Value = keyframe.Brightness * 100;

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

    private void SnapToggle_Toggled(object sender, RoutedEventArgs e)
    {
        // Re-render to show/hide grid lines
        RenderTimeline();
    }

    private void KeyframeCanvas_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (ViewModel == null)
            return;

        var point = e.GetCurrentPoint(KeyframeCanvas);
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

    private void KeyframeCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (ViewModel?.Tracks == null || ViewModel.Tracks.Count == 0)
        {
            // Close panel if clicking on empty canvas
            SidePanel.Visibility = Visibility.Collapsed;
            if (ViewModel != null) ViewModel.SelectedKeyframe = null;
            return;
        }

        var point = e.GetCurrentPoint(KeyframeCanvas);

        // Only handle left-click for creating keyframes
        if (!point.Properties.IsLeftButtonPressed)
            return;

        var x = point.Position.X;
        var y = point.Position.Y;

        const double trackHeight = 50;
        var trackIndex = (int)(y / trackHeight);

        // Ensure track index is valid
        if (trackIndex < 0 || trackIndex >= ViewModel.Tracks.Count)
        {
            // Clicked outside tracks - close the panel
            SidePanel.Visibility = Visibility.Collapsed;
            ViewModel.SelectedKeyframe = null;
            return;
        }

        var track = ViewModel.Tracks[trackIndex];
        var timeSeconds = x / ViewModel.ZoomLevel;

        // Clamp time to valid range
        if (timeSeconds < 0) timeSeconds = 0;
        if (timeSeconds > ViewModel.DurationSeconds) timeSeconds = ViewModel.DurationSeconds;

        // Check if clicking near an existing keyframe - if so, don't create a new one
        // (the keyframe's own handler will handle it)
        var clickThreshold = 12.0 / ViewModel.ZoomLevel; // Match the keyframe circle size
        var nearbyKeyframe = track.Keyframes
            .FirstOrDefault(k => Math.Abs(k.TimeSeconds - timeSeconds) < clickThreshold);

        if (nearbyKeyframe != null)
        {
            // Don't create new keyframe - let the keyframe handle its own click
            return;
        }

        // If panel is open, just close it without creating a new keyframe
        if (SidePanel.Visibility == Visibility.Visible)
        {
            SidePanel.Visibility = Visibility.Collapsed;
            ViewModel.SelectedKeyframe = null;
            e.Handled = true;
            return;
        }

        // Apply snap unless Ctrl is held
        var ctrlHeld = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        if (!ctrlHeld)
        {
            timeSeconds = ViewModel.SnapToGrid(timeSeconds);
        }

        // Left-click on blank space creates a new keyframe
        ViewModel.AddKeyframe(track, timeSeconds);
        RenderTimeline();

        // Select the newly added keyframe
        var newKeyframe = track.Keyframes.FirstOrDefault(k => Math.Abs(k.TimeSeconds - timeSeconds) < 0.1);
        if (newKeyframe != null)
        {
            SelectKeyframe(newKeyframe);
        }

        e.Handled = true;
    }

    private void CloseSidePanel_Click(object sender, RoutedEventArgs e)
    {
        SidePanel.Visibility = Visibility.Collapsed;
        ViewModel.SelectedKeyframe = null;
    }

    private async void KeyframeColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
    {
        if (ViewModel?.SelectedKeyframe != null)
        {
            // Convert RGB to xy color space (approximation)
            var r = args.NewColor.R / 255.0;
            var g = args.NewColor.G / 255.0;
            var b = args.NewColor.B / 255.0;

            // Apply gamma correction
            r = (r > 0.04045) ? Math.Pow((r + 0.055) / 1.055, 2.4) : r / 12.92;
            g = (g > 0.04045) ? Math.Pow((g + 0.055) / 1.055, 2.4) : g / 12.92;
            b = (b > 0.04045) ? Math.Pow((b + 0.055) / 1.055, 2.4) : b / 12.92;

            // Convert to XYZ
            var X = r * 0.4124 + g * 0.3576 + b * 0.1805;
            var Y = r * 0.2126 + g * 0.7152 + b * 0.0722;
            var Z = r * 0.0193 + g * 0.1192 + b * 0.9505;

            // Convert to xy
            var sum = X + Y + Z;
            if (sum > 0)
            {
                var x = X / sum;
                var y = Y / sum;
                ViewModel.SelectedKeyframe.Color = new HueWindows.Core.Models.HueColor(x, y);
                RenderTimeline();

                // Live preview on actual light
                await ViewModel.UpdateLightForKeyframeAsync(ViewModel.SelectedKeyframe);
            }
        }
    }

    private async void BrightnessSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (BrightnessValueText != null)
            BrightnessValueText.Text = $"{(int)e.NewValue}%";
        if (ViewModel?.SelectedKeyframe != null)
        {
            ViewModel.SelectedKeyframe.Brightness = e.NewValue / 100.0;
            RenderTimeline();

            // Live preview on actual light
            await ViewModel.UpdateLightForKeyframeAsync(ViewModel.SelectedKeyframe);
        }
    }

    private void TransitionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel?.SelectedKeyframe != null && TransitionComboBox.SelectedItem is ComboBoxItem item)
        {
            var tagValue = item.Tag?.ToString();
            if (Enum.TryParse<HueWindows.Core.Models.TransitionStyle>(tagValue, out var transition))
            {
                ViewModel.SelectedKeyframe.Transition = transition;
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

    private void ShowLightTrackPulse(int trackIndex, double trackHeight)
    {
        var y = trackIndex * trackHeight + trackHeight / 2;
        var x = ViewModel.PlayheadPosition * ViewModel.ZoomLevel;

        // Create a highlight pulse on the light track
        var pulse = new Microsoft.UI.Xaml.Shapes.Ellipse
        {
            Width = 24,
            Height = 24,
            Fill = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Windows.UI.Color.FromArgb(180, 255, 255, 255)),
            Stroke = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Windows.UI.Color.FromArgb(255, 255, 220, 100)),
            StrokeThickness = 2,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(pulse, x - 12);
        Canvas.SetTop(pulse, y - 12);
        KeyframeCanvas.Children.Add(pulse);

        // Animate expansion and fade out
        var fadeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
        var scale = 1.0;
        var opacity = 1.0;
        fadeTimer.Tick += (s, e) =>
        {
            scale += 0.15;
            opacity -= 0.12;
            if (opacity <= 0)
            {
                fadeTimer.Stop();
                KeyframeCanvas.Children.Remove(pulse);
            }
            else
            {
                pulse.Width = 24 * scale;
                pulse.Height = 24 * scale;
                Canvas.SetLeft(pulse, x - (12 * scale));
                Canvas.SetTop(pulse, y - (12 * scale));
                pulse.Opacity = opacity;
            }
        };
        fadeTimer.Start();
    }

    private void ShowEventPulse(EventTrackViewModel eventTrack, double lightTracksHeight, double trackHeight)
    {
        var trackIndex = ViewModel.EventTracks.IndexOf(eventTrack);
        if (trackIndex < 0) return;

        var y = lightTracksHeight + trackIndex * trackHeight + trackHeight / 2;
        var x = ViewModel.PlayheadPosition * ViewModel.ZoomLevel;

        // Create a pulse circle that fades out
        var pulse = new Microsoft.UI.Xaml.Shapes.Ellipse
        {
            Width = 20,
            Height = 20,
            Fill = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Windows.UI.Color.FromArgb(200, 255, 220, 100)),
            IsHitTestVisible = false
        };
        Canvas.SetLeft(pulse, x - 10);
        Canvas.SetTop(pulse, y - 10);
        KeyframeCanvas.Children.Add(pulse);

        // Animate fade out
        var fadeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        var opacity = 1.0;
        fadeTimer.Tick += (s, e) =>
        {
            opacity -= 0.15;
            if (opacity <= 0)
            {
                fadeTimer.Stop();
                KeyframeCanvas.Children.Remove(pulse);
            }
            else
            {
                pulse.Opacity = opacity;
            }
        };
        fadeTimer.Start();
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
