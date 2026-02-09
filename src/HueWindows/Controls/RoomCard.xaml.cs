using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using HueWindows.Constants;
using HueWindows.Converters;
using HueWindows.Core.ViewModels;
using HueWindows.Utilities;
using Windows.UI;

namespace HueWindows.Controls;

/// <summary>
/// Widget-like room card control for the dashboard.
/// Supports tap to navigate and drag to adjust brightness.
/// Features Composition-based glow effect when active.
/// </summary>
public sealed partial class RoomCard : UserControl
{
    private bool _isDragging;
    private double _startBrightness;
    private double _cumulativeDeltaY;
    private DateTime _lastBrightnessUpdate = DateTime.MinValue;
    private double _pendingBrightness;
    private bool _isLoaded;
    private bool _isHovering;

    // Current accent color
    private Color _accentColor = Colors.White;
    private IRoomCardViewModel? _currentViewModel;

    public IRoomCardViewModel? ViewModel => DataContext as IRoomCardViewModel;

    public RoomCard()
    {
        this.InitializeComponent();
        this.DataContextChanged += OnDataContextChanged;
        this.Loaded += OnLoaded;
        this.Unloaded += OnUnloaded;
        this.SizeChanged += OnSizeChanged;
        this.ActualThemeChanged += OnActualThemeChanged;
    }

    private void OnActualThemeChanged(FrameworkElement sender, object args)
    {
        ApplyThemeBackground();
    }

    private void ApplyThemeBackground()
    {
        var isDark = ActualTheme == ElementTheme.Dark;

        if (_isHovering)
        {
            CardRoot.Background = CreateHoverGradient(isDark);
        }
        else
        {
            CardRoot.Background = CreateCardGradient(isDark);
        }
    }

    private static LinearGradientBrush CreateCardGradient(bool isDark)
        => CardGradientHelper.CreateCardGradient(isDark);

    private static LinearGradientBrush CreateHoverGradient(bool isDark)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Windows.Foundation.Point(0, 0),
            EndPoint = new Windows.Foundation.Point(1, 1)
        };

        if (isDark)
        {
            brush.GradientStops.Add(new GradientStop { Color = ColorHelper.FromArgb(230, 34, 34, 38), Offset = 0 });
            brush.GradientStops.Add(new GradientStop { Color = ColorHelper.FromArgb(230, 45, 45, 48), Offset = 0.5 });
            brush.GradientStops.Add(new GradientStop { Color = ColorHelper.FromArgb(230, 38, 38, 41), Offset = 1 });
        }
        else
        {
            brush.GradientStops.Add(new GradientStop { Color = ColorHelper.FromArgb(230, 237, 237, 237), Offset = 0 });
            brush.GradientStops.Add(new GradientStop { Color = ColorHelper.FromArgb(230, 240, 240, 240), Offset = 0.5 });
            brush.GradientStops.Add(new GradientStop { Color = ColorHelper.FromArgb(230, 238, 238, 238), Offset = 1 });
        }

        return brush;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        ApplyThemeBackground();
        UpdateActiveState();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_currentViewModel != null)
        {
            _currentViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            _currentViewModel = null;
        }
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateBrightnessBar();
    }



    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (_currentViewModel != null)
        {
            _currentViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        _currentViewModel = ViewModel;

        if (_currentViewModel != null)
        {
            _currentViewModel.PropertyChanged += ViewModel_PropertyChanged;

            if (_isLoaded)
            {
                UpdateActiveState();
            }
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RoomCardViewModel.IsOn) ||
            e.PropertyName == nameof(RoomCardViewModel.BackgroundColorRgb) ||
            e.PropertyName == nameof(RoomCardViewModel.LightColors))
        {
            if (DispatcherQueue?.HasThreadAccess == true)
            {
                UpdateActiveState();
            }
            else
            {
                DispatcherQueue?.TryEnqueue(UpdateActiveState);
            }
        }
        else if (e.PropertyName == nameof(RoomCardViewModel.BrightnessPercent))
        {
            if (DispatcherQueue?.HasThreadAccess == true)
            {
                UpdateBrightnessBar();
            }
            else
            {
                DispatcherQueue?.TryEnqueue(UpdateBrightnessBar);
            }
        }
    }

    private void UpdateActiveState()
    {
        if (!_isLoaded || ViewModel == null) return;

        var isActive = ViewModel.IsOn;

        // Get room color
        var lightColors = ViewModel.LightColors;
        if (lightColors.Count > 0 && isActive)
        {
            var (r, g, b) = lightColors[0];
            _accentColor = Color.FromArgb(255, r, g, b);
        }
        else
        {
            _accentColor = Colors.Gray;
        }

        // Update glow and outline (Set properties before animation starts)
        UpdateBorderEffect(isActive);

        // Update visual state (Triggers animation)
        VisualStateManager.GoToState(this, isActive ? "Active" : "Inactive", true);

        // Update toggle color
        UpdateToggleColor(isActive);

        // Update icon color
        UpdateIconColor(isActive);

        // Update brightness bar
        UpdateBrightnessBar();

        // Notify MainWindow of color change for ambient wash
        NotifyAmbientColorChange(isActive);
    }

    private void NotifyAmbientColorChange(bool isActive)
    {
        if (ViewModel == null) return;

        // Only update ambient color if this card is active (on)
        if (!isActive) return;

        try
        {
            var mainWindow = App.MainWindow;
            var colors = ViewModel.LightColors;
            if (colors.Count > 0)
            {
                mainWindow.UpdateAmbientColor(colors);
                var (r, g, b) = colors[0];
                mainWindow.UpdateNavIndicatorColor(Color.FromArgb(255, r, g, b));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RoomCard] Non-critical error in NotifyAmbientColorChange: {ex.Message}");
        }
    }

    private void UpdateBorderEffect(bool isActive)
    {
        if (ViewModel == null) return;

        // Build the gradient brush from light colors
        var colors = ViewModel.LightColors;
        var borderBrush = colors.Count > 0
            ? BrushFactory.CreateDiagonalGradient(colors)
            : BrushFactory.CreateDiagonalGradient(new[] { (_accentColor.R, _accentColor.G, _accentColor.B) });

        // Set the brush FIRST, then animate opacity
        OutlineBorder.BorderBrush = borderBrush;

        // Animate border opacity
        var targetOpacity = isActive ? 1.0 : 0.0;
        AnimationHelper.AnimateOpacity(OutlineBorder, targetOpacity);

        // Update glow effect
        var glowColor = isActive
            ? Color.FromArgb(25, _accentColor.R, _accentColor.G, _accentColor.B)
            : Color.FromArgb(0, _accentColor.R, _accentColor.G, _accentColor.B);
        GlowBorder.Background = new SolidColorBrush(glowColor);
        AnimationHelper.AnimateOpacity(GlowBorder, isActive ? 1.0 : 0.0);
    }

    private void UpdateToggleColor(bool isActive)
    {
        try
        {
            if (RoomToggle == null) return;

            Brush fillBrush;

            if (isActive && ViewModel != null && ViewModel.LightColors.Count > 1)
            {
                // Create gradient for toggle (horizontal)
                fillBrush = BrushFactory.CreateHorizontalGradient(ViewModel.LightColors);
            }
            else
            {
                fillBrush = new SolidColorBrush(_accentColor);
            }

            if (isActive)
            {
                // New logic: Just set background and let the template bind to it
                RoomToggle.Background = fillBrush;
            }
            else
            {
                RoomToggle.Background = new SolidColorBrush(Colors.Transparent);
            }
            
            // Note: FindDescendant/ApplyTemplate no longer needed with new style
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RoomCard] Non-critical error in UpdateToggleColor: {ex.Message}");
        }
    }



    private void UpdateIconColor(bool isActive)
    {
        if (RoomIcon == null) return;

        if (isActive && ViewModel != null)
        {
            var colors = ViewModel.LightColors;
            if (colors.Count > 1)
            {
                // Create gradient for icon matching toggle/border
                RoomIcon.Foreground = BrushFactory.CreateDiagonalGradient(colors);
            }
            else
            {
                RoomIcon.Foreground = new SolidColorBrush(_accentColor);
            }
        }
        else
        {
            // Use theme-aware inactive color
            var isDark = ActualTheme == ElementTheme.Dark;
            var inactiveColor = isDark
                ? Color.FromArgb(AppConstants.Colors.InactiveIconAlpha, 255, 255, 255)
                : Color.FromArgb(AppConstants.Colors.InactiveIconAlpha, 0, 0, 0);
            RoomIcon.Foreground = new SolidColorBrush(inactiveColor);
        }
    }

    private void UpdateBrightnessBar()
    {
        if (BrightnessFill == null || ViewModel == null) return;

        var parentGrid = BrightnessFill.Parent as Grid;
        if (parentGrid == null) return;

        var totalWidth = parentGrid.ActualWidth;
        if (totalWidth <= 0) return;

        var fillWidth = totalWidth * (ViewModel.BrightnessPercent / 100.0);
        BrightnessFill.Width = fillWidth;

        // Update fill color based on active state
        if (ViewModel.IsOn)
        {
            BrightnessFill.Background = new SolidColorBrush(_accentColor);
        }
        else
        {
            // Use theme-aware inactive color
            var isDark = ActualTheme == ElementTheme.Dark;
            var inactiveColor = isDark
                ? Color.FromArgb(AppConstants.Colors.InactiveBrightnessBarAlpha, 255, 255, 255)
                : Color.FromArgb(AppConstants.Colors.InactiveBrightnessBarAlpha, 0, 0, 0);
            BrightnessFill.Background = new SolidColorBrush(inactiveColor);
        }
    }

    private void CardRoot_ManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e)
    {
        if (ViewModel == null) return;

        _isDragging = false;
        _cumulativeDeltaY = 0;
        _startBrightness = ViewModel.Brightness;
    }

    private void CardRoot_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
    {
        if (ViewModel == null) return;

        _cumulativeDeltaY += e.Delta.Translation.Y;

        if (Math.Abs(_cumulativeDeltaY) > AppConstants.BrightnessDrag.DragThreshold)
        {
            if (!_isDragging)
            {
                _isDragging = true;
                VisualStateManager.GoToState(this, "Dragging", true);
            }

            var brightnessChange = -_cumulativeDeltaY / (AppConstants.BrightnessDrag.PixelsPerPercent * 100);
            var newBrightness = Math.Clamp(_startBrightness + brightnessChange, 0.0, 1.0);
            _pendingBrightness = newBrightness;

            var now = DateTime.UtcNow;
            if ((now - _lastBrightnessUpdate).TotalMilliseconds >= AppConstants.BrightnessDrag.ThrottleMs)
            {
                _lastBrightnessUpdate = now;
                ViewModel.SetBrightnessCommand.Execute(newBrightness);
            }
        }

        e.Handled = true;
    }

    private void CardRoot_ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
    {
        if (_isDragging && ViewModel != null)
        {
            ViewModel.SetBrightnessCommand.Execute(_pendingBrightness);
        }

        _isDragging = false;
        VisualStateManager.GoToState(this, "Normal", true);
    }

    private void RoomToggle_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.IsOn = !ViewModel.IsOn;
        }
        e.Handled = true;
    }

    private void CardRoot_Tapped(object sender, TappedRoutedEventArgs e)
    {
        // Prevent navigation if clicking the toggle or scene chips
        if (e.OriginalSource is DependencyObject obj)
        {
            if (obj.IsDescendantOf(RoomToggle))
                return;
            if (obj.IsDescendantOf(SceneChipsPanel))
                return;
        }

        if (!_isDragging && ViewModel != null)
        {
            // Prepare connected animation before navigation
            ConnectedAnimationService.GetForCurrentView()
                .PrepareToAnimate("RoomCardToHeader", CardRoot);

            ViewModel.TapRoomCommand.Execute(null);
        }

        e.Handled = true;
    }

    private void CardRoot_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging)
        {
            _isHovering = true;
            ApplyThemeBackground();
            AnimateScale(1.015);
            VisualStateManager.GoToState(this, "Hover", true);

            // Lazy load scenes on first hover (only for dashboard cards)
            if (DataContext is DashboardCardViewModel dashVm)
                _ = LoadAndDisplayScenesAsync(dashVm);
        }
    }

    private void CardRoot_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging)
        {
            _isHovering = false;
            ApplyThemeBackground();
            AnimateScale(1.0);
            VisualStateManager.GoToState(this, "Default", true);
        }
    }

    private void AnimateScale(double target)
    {
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        var duration = new Duration(TimeSpan.FromMilliseconds(200));

        var animX = new DoubleAnimation { To = target, Duration = duration, EasingFunction = easing };
        var animY = new DoubleAnimation { To = target, Duration = duration, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };

        var sb = new Storyboard();
        Storyboard.SetTarget(animX, CardScale);
        Storyboard.SetTargetProperty(animX, "ScaleX");
        Storyboard.SetTarget(animY, CardScale);
        Storyboard.SetTargetProperty(animY, "ScaleY");
        sb.Children.Add(animX);
        sb.Children.Add(animY);
        sb.Begin();
    }

    // Scene chip buttons and their child elements, indexed by slot
    private Button[] SceneChipButtons => new[] { SceneChip0, SceneChip1, SceneChip2 };
    private Microsoft.UI.Xaml.Shapes.Ellipse[] SceneChipDots => new[] { SceneChip0Dot, SceneChip1Dot, SceneChip2Dot };
    private TextBlock[] SceneChipTexts => new[] { SceneChip0Text, SceneChip1Text, SceneChip2Text };

    private async Task LoadAndDisplayScenesAsync(DashboardCardViewModel dashVm)
    {
        await dashVm.LoadScenesAsync();
        UpdateSceneChips(dashVm);
    }

    private void UpdateSceneChips(DashboardCardViewModel dashVm)
    {
        var chips = dashVm.SceneChips;
        var hasChips = chips.Count > 0;

        SceneChipsPanel.Visibility = hasChips ? Visibility.Visible : Visibility.Collapsed;

        var converter = new HexToSolidColorBrushConverter();
        var bgConverter = new HexToGradientBackgroundConverter();

        for (int i = 0; i < 3; i++)
        {
            if (i < chips.Count)
            {
                var chip = chips[i];
                SceneChipButtons[i].Visibility = Visibility.Visible;
                SceneChipButtons[i].Tag = chip.SceneId;
                SceneChipButtons[i].Background = bgConverter.Convert(chip.Color1Hex, typeof(Brush), null!, null!) as Brush
                    ?? new SolidColorBrush(Colors.Transparent);
                SceneChipDots[i].Fill = converter.Convert(chip.Color1Hex, typeof(Brush), null!, null!) as Brush
                    ?? new SolidColorBrush(Colors.Gray);
                SceneChipTexts[i].Text = chip.Name;
            }
            else
            {
                SceneChipButtons[i].Visibility = Visibility.Collapsed;
            }
        }

        // Show "More" button if there are more scenes than chips shown
        MoreScenesButton.Visibility = dashVm.HasScenes ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void SceneChip_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid sceneId && DataContext is DashboardCardViewModel dashVm)
        {
            await dashVm.ActivateSceneAsync(sceneId);
        }
    }

    private void AllScenesFlyout_Opening(object sender, object e)
    {
        if (DataContext is not DashboardCardViewModel dashVm) return;

        var allItems = dashVm.GetAllSceneItems();
        ScenesFlyoutContent.Children.Clear();

        // Header
        ScenesFlyoutContent.Children.Add(new TextBlock
        {
            Text = "Scenes",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 13,
            Margin = new Thickness(0, 0, 0, 8)
        });

        var converter = new HexToSolidColorBrushConverter();

        foreach (var item in allItems)
        {
            var btn = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(8, 6, 8, 6),
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(0),
                Background = new SolidColorBrush(Colors.Transparent),
                Tag = item.SceneId
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var dot = new Microsoft.UI.Xaml.Shapes.Ellipse
            {
                Width = 10, Height = 10,
                Margin = new Thickness(0, 0, 8, 0),
                Fill = converter.Convert(item.Color1Hex, typeof(Brush), null!, null!) as Brush
                    ?? new SolidColorBrush(Colors.Gray)
            };
            Grid.SetColumn(dot, 0);
            grid.Children.Add(dot);

            var name = new TextBlock
            {
                Text = item.Name,
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(name, 1);
            grid.Children.Add(name);

            if (item.IsFavorite)
            {
                var star = new FontIcon
                {
                    Glyph = "\uE735",
                    FontSize = 12,
                    Foreground = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"]
                };
                Grid.SetColumn(star, 2);
                grid.Children.Add(star);
            }

            btn.Content = grid;
            btn.Click += SceneFlyoutItem_Click;
            ScenesFlyoutContent.Children.Add(btn);
        }
    }

    private async void SceneFlyoutItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid sceneId && DataContext is DashboardCardViewModel dashVm)
        {
            AllScenesFlyout.Hide();
            await dashVm.ActivateSceneAsync(sceneId);
        }
    }

}
