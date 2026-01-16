using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.Extensions.DependencyInjection;
using HueWindows.Core.ViewModels;
using Windows.UI;

namespace HueWindows.Views;

/// <summary>
/// Scene Builder page with DAW-style timeline editor for creating animated scenes.
/// </summary>
public sealed partial class SceneBuilderPage : Page
{
    public SceneBuilderViewModel ViewModel { get; }

    public SceneBuilderPage()
    {
        this.InitializeComponent();
        ViewModel = App.Services.GetRequiredService<SceneBuilderViewModel>();
        Loaded += Page_Loaded;
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.InitializeAsync();
        UpdateEmptyState();
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

        // Render keyframes for each track
        const double trackHeight = 50;
        for (int trackIndex = 0; trackIndex < ViewModel.Tracks.Count; trackIndex++)
        {
            var track = ViewModel.Tracks[trackIndex];
            var y = trackIndex * trackHeight + trackHeight / 2;

            foreach (var keyframe in track.Keyframes)
            {
                RenderKeyframe(keyframe, track, keyframe.TimeSeconds * ViewModel.ZoomLevel, y);
            }
        }

        // Set canvas size
        KeyframeCanvas.Width = ViewModel.DurationSeconds * ViewModel.ZoomLevel;
        KeyframeCanvas.Height = ViewModel.Tracks.Count * trackHeight;
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
    }

    private void RenderKeyframe(KeyframeViewModel keyframe, TrackViewModel track, double x, double y)
    {
        // Convert HueColor to Windows.UI.Color (approximation)
        var rgb = HueColorToRgb(keyframe.Color);
        var brightness = keyframe.Brightness;
        var color = Windows.UI.Color.FromArgb(255,
            (byte)(rgb.r * brightness),
            (byte)(rgb.g * brightness),
            (byte)(rgb.b * brightness));

        var circle = new Microsoft.UI.Xaml.Shapes.Ellipse
        {
            Width = 16,
            Height = 16,
            Fill = new Microsoft.UI.Xaml.Media.SolidColorBrush(color),
            Stroke = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White),
            StrokeThickness = 2
        };

        Canvas.SetLeft(circle, x - 8);
        Canvas.SetTop(circle, y - 8);

        // Store references for selection
        circle.Tag = (keyframe, track);
        circle.PointerPressed += Keyframe_PointerPressed;

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
            SelectKeyframe(keyframe);
            e.Handled = true;
        }
    }

    private void SelectKeyframe(KeyframeViewModel keyframe)
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
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack)
        {
            Frame.GoBack();
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Implement save
    }

    private void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Implement play
        ViewModel.IsPlaying = !ViewModel.IsPlaying;
        UpdatePlayButtonState();
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Implement stop
        ViewModel.IsPlaying = false;
        UpdatePlayButtonState();
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
        // TODO: Implement add keyframe
    }

    private void KeyframeCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        // TODO: Handle click on canvas to add/select keyframe
    }

    private void CloseSidePanel_Click(object sender, RoutedEventArgs e)
    {
        SidePanel.Visibility = Visibility.Collapsed;
        ViewModel.SelectedKeyframe = null;
    }

    private void KeyframeColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
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
            }
        }
    }

    private void BrightnessSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (BrightnessValueText != null)
            BrightnessValueText.Text = $"{(int)e.NewValue}%";
        if (ViewModel?.SelectedKeyframe != null)
        {
            ViewModel.SelectedKeyframe.Brightness = e.NewValue / 100.0;
            RenderTimeline();
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
}
