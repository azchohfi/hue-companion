# Remaining Scene Features - Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Complete save-as-scene, static scene editing, scene assignment, animated previews, and expanded library.

**Architecture:** Add scene CRUD methods to HueBridgeService, new RoomSceneAssignmentService for pinned animations, AnimatedPreviewControl for visual previews, and generate additional preset scenes.

**Tech Stack:** WinUI 3, HueApi CLIP v2, CommunityToolkit.Mvvm, JSON storage

---

## Task 1: Save Current Room as Scene

**Files:**
- Modify: `src/HueWindows.Core/Services/Interfaces/IHueBridgeService.cs`
- Modify: `src/HueWindows.Core/Services/HueBridgeService.cs`
- Modify: `src/HueWindows.Core/ViewModels/RoomDetailViewModel.cs`
- Modify: `src/HueWindows/Views/RoomDetailPage.xaml`
- Modify: `src/HueWindows/Views/RoomDetailPage.xaml.cs`

**Step 1: Add interface method**

In `IHueBridgeService.cs`, add after `ActivateSceneAsync`:

```csharp
/// <summary>
/// Creates a new scene from the current state of lights in a room/zone.
/// </summary>
/// <param name="groupId">The room or zone ID.</param>
/// <param name="sceneName">Name for the new scene.</param>
/// <param name="isZone">True if groupId is a zone, false for room.</param>
/// <returns>The created scene ID on success.</returns>
Task<Result<Guid>> CreateSceneFromCurrentStateAsync(Guid groupId, string sceneName, bool isZone = false);
```

**Step 2: Implement in HueBridgeService**

Add after `ActivateSceneAsync`:

```csharp
public async Task<Result<Guid>> CreateSceneFromCurrentStateAsync(Guid groupId, string sceneName, bool isZone = false)
{
    if (_hueApi == null)
        return Result<Guid>.Failure("Not connected to bridge.");

    try
    {
        // Get current light states
        var lights = isZone
            ? await GetLightsInZoneAsync(groupId)
            : await GetLightsInRoomAsync(groupId);

        if (!lights.IsSuccess)
            return Result<Guid>.Failure(lights.ErrorMessage ?? "Failed to get lights.");

        // Build scene actions from current light states
        var actions = new List<SceneAction>();
        foreach (var light in lights.Value!)
        {
            var action = new SceneAction
            {
                Target = new ResourceIdentifier { Rid = light.Id, Rtype = "light" },
                Action = new LightAction
                {
                    On = new On { IsOn = light.IsOn },
                    Dimming = new Dimming { Brightness = light.Brightness * 100 },
                    Color = light.Color != null ? new HueApi.Models.Color
                    {
                        Xy = new XyPosition { X = light.Color.X, Y = light.Color.Y }
                    } : null
                }
            };
            actions.Add(action);
        }

        // Create the scene
        var createScene = new CreateScene
        {
            Metadata = new SceneMetadata { Name = sceneName },
            Group = new ResourceIdentifier { Rid = groupId, Rtype = isZone ? "zone" : "room" },
            Actions = actions
        };

        var result = await _hueApi.Scene.CreateAsync(createScene);
        if (result?.Data?.FirstOrDefault()?.Rid is Guid sceneId)
        {
            return Result<Guid>.Success(sceneId);
        }

        return Result<Guid>.Failure("Failed to create scene.");
    }
    catch (Exception ex)
    {
        return Result<Guid>.Failure($"Failed to create scene: {ex.Message}");
    }
}
```

**Step 3: Add ViewModel command**

In `RoomDetailViewModel.cs`, add:

```csharp
[RelayCommand]
private async Task SaveAsSceneAsync(string sceneName)
{
    if (string.IsNullOrWhiteSpace(sceneName))
        return;

    var isZone = _groupType == LightGroupType.Zone;
    var result = await _bridgeService.CreateSceneFromCurrentStateAsync(_groupId, sceneName, isZone);

    if (result.IsSuccess)
    {
        // Refresh scenes list
        await LoadScenesAsync();
    }
    else
    {
        ErrorMessage = result.ErrorMessage;
    }
}
```

**Step 4: Add UI button and dialog**

In `RoomDetailPage.xaml`, add button in header (after the toggle, around line 125):

```xml
<Button Grid.Column="4"
        Style="{StaticResource IconButtonStyle}"
        ToolTipService.ToolTip="Save as Scene"
        Click="SaveAsScene_Click"
        Margin="8,0,0,0">
    <FontIcon Glyph="&#xE74E;" FontSize="18"/>
</Button>
```

Update column definitions to add column 4:
```xml
<Grid.ColumnDefinitions>
    <ColumnDefinition Width="Auto"/>
    <ColumnDefinition Width="*"/>
    <ColumnDefinition Width="Auto"/>
    <ColumnDefinition Width="Auto"/>
    <ColumnDefinition Width="Auto"/>
</Grid.ColumnDefinitions>
```

**Step 5: Add click handler**

In `RoomDetailPage.xaml.cs`:

```csharp
private async void SaveAsScene_Click(object sender, RoutedEventArgs e)
{
    var dialog = new ContentDialog
    {
        Title = "Save as Scene",
        PrimaryButtonText = "Save",
        CloseButtonText = "Cancel",
        DefaultButton = ContentDialogButton.Primary,
        XamlRoot = this.XamlRoot
    };

    var input = new TextBox
    {
        PlaceholderText = "Scene name",
        Text = $"{ViewModel.RoomName} - Custom"
    };
    dialog.Content = input;

    var result = await dialog.ShowAsync();
    if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(input.Text))
    {
        await ViewModel.SaveAsSceneCommand.ExecuteAsync(input.Text);
    }
}
```

**Step 6: Build and test**

```bash
dotnet build src/HueWindows/HueWindows.csproj
```

Run app, go to room detail, click save icon, enter name, verify scene appears in list.

**Step 7: Commit**

```bash
git add -A
git commit -m "feat: add save current room as scene"
```

---

## Task 2: Delete User Scenes

**Files:**
- Modify: `src/HueWindows.Core/Services/Interfaces/IHueBridgeService.cs`
- Modify: `src/HueWindows.Core/Services/HueBridgeService.cs`
- Modify: `src/HueWindows.Core/ViewModels/RoomDetailViewModel.cs`
- Modify: `src/HueWindows/Views/RoomDetailPage.xaml`
- Modify: `src/HueWindows/Views/RoomDetailPage.xaml.cs`

**Step 1: Add interface method**

```csharp
/// <summary>
/// Deletes a scene from the bridge.
/// </summary>
Task<Result> DeleteSceneAsync(Guid sceneId);
```

**Step 2: Implement in HueBridgeService**

```csharp
public async Task<Result> DeleteSceneAsync(Guid sceneId)
{
    if (_hueApi == null)
        return Result.Failure("Not connected to bridge.");

    try
    {
        await _hueApi.Scene.DeleteAsync(sceneId);
        return Result.Success();
    }
    catch (Exception ex)
    {
        return Result.Failure($"Failed to delete scene: {ex.Message}");
    }
}
```

**Step 3: Add ViewModel command**

```csharp
[RelayCommand]
private async Task DeleteSceneAsync(SceneItemViewModel scene)
{
    var result = await _bridgeService.DeleteSceneAsync(scene.Id);
    if (result.IsSuccess)
    {
        Scenes.Remove(scene);
    }
    else
    {
        ErrorMessage = result.ErrorMessage;
    }
}
```

**Step 4: Add context menu to scene cards**

In `RoomDetailPage.xaml`, update the scene card DataTemplate to add context menu:

```xml
<Button Style="{StaticResource SceneCardButtonStyle}"
        Background="{x:Bind Color1Hex, Converter={StaticResource HexToGradientBackgroundConverter}}"
        Command="{x:Bind ActivateCommand}">
    <Button.ContextFlyout>
        <MenuFlyout>
            <MenuFlyoutItem Text="Delete"
                            Click="DeleteScene_Click"
                            Tag="{x:Bind}">
                <MenuFlyoutItem.Icon>
                    <FontIcon Glyph="&#xE74D;"/>
                </MenuFlyoutItem.Icon>
            </MenuFlyoutItem>
        </MenuFlyout>
    </Button.ContextFlyout>
    <!-- existing content -->
</Button>
```

**Step 5: Add delete handler**

```csharp
private async void DeleteScene_Click(object sender, RoutedEventArgs e)
{
    if (sender is MenuFlyoutItem item && item.Tag is SceneItemViewModel scene)
    {
        var dialog = new ContentDialog
        {
            Title = "Delete Scene",
            Content = $"Delete \"{scene.Name}\"? This cannot be undone.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            await ViewModel.DeleteSceneCommand.ExecuteAsync(scene);
        }
    }
}
```

**Step 6: Build and test**

**Step 7: Commit**

```bash
git add -A
git commit -m "feat: add delete scene functionality"
```

---

## Task 3: Scene Assignment Service

**Files:**
- Create: `src/HueWindows.Core/Services/Interfaces/IRoomSceneAssignmentService.cs`
- Create: `src/HueWindows.Core/Services/RoomSceneAssignmentService.cs`
- Modify: `src/HueWindows/App.xaml.cs` (register service)

**Step 1: Create interface**

```csharp
namespace HueWindows.Core.Services.Interfaces;

/// <summary>
/// Service for managing animated scene assignments to rooms.
/// </summary>
public interface IRoomSceneAssignmentService
{
    /// <summary>
    /// Gets the animated scene IDs assigned to a room.
    /// </summary>
    Task<IReadOnlyList<string>> GetAssignedScenesAsync(Guid roomId);

    /// <summary>
    /// Assigns an animated scene to a room.
    /// </summary>
    Task AssignSceneToRoomAsync(Guid roomId, string sceneId);

    /// <summary>
    /// Removes an animated scene assignment from a room.
    /// </summary>
    Task RemoveSceneFromRoomAsync(Guid roomId, string sceneId);

    /// <summary>
    /// Gets all room assignments.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, List<string>>> GetAllAssignmentsAsync();
}
```

**Step 2: Create implementation**

```csharp
using System.Text.Json;
using HueWindows.Core.Services.Interfaces;

namespace HueWindows.Core.Services;

public class RoomSceneAssignmentService : IRoomSceneAssignmentService
{
    private readonly string _filePath;
    private Dictionary<Guid, List<string>> _assignments = new();
    private bool _loaded;

    public RoomSceneAssignmentService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var folder = Path.Combine(appData, "HueWindows");
        Directory.CreateDirectory(folder);
        _filePath = Path.Combine(folder, "room-scene-assignments.json");
    }

    private async Task EnsureLoadedAsync()
    {
        if (_loaded) return;

        if (File.Exists(_filePath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_filePath);
                var data = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json);
                if (data != null)
                {
                    _assignments = data.ToDictionary(
                        kvp => Guid.Parse(kvp.Key),
                        kvp => kvp.Value
                    );
                }
            }
            catch
            {
                _assignments = new();
            }
        }
        _loaded = true;
    }

    private async Task SaveAsync()
    {
        var data = _assignments.ToDictionary(
            kvp => kvp.Key.ToString(),
            kvp => kvp.Value
        );
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_filePath, json);
    }

    public async Task<IReadOnlyList<string>> GetAssignedScenesAsync(Guid roomId)
    {
        await EnsureLoadedAsync();
        return _assignments.TryGetValue(roomId, out var scenes) ? scenes : Array.Empty<string>();
    }

    public async Task AssignSceneToRoomAsync(Guid roomId, string sceneId)
    {
        await EnsureLoadedAsync();

        if (!_assignments.TryGetValue(roomId, out var scenes))
        {
            scenes = new List<string>();
            _assignments[roomId] = scenes;
        }

        if (!scenes.Contains(sceneId))
        {
            scenes.Add(sceneId);
            await SaveAsync();
        }
    }

    public async Task RemoveSceneFromRoomAsync(Guid roomId, string sceneId)
    {
        await EnsureLoadedAsync();

        if (_assignments.TryGetValue(roomId, out var scenes))
        {
            scenes.Remove(sceneId);
            await SaveAsync();
        }
    }

    public async Task<IReadOnlyDictionary<Guid, List<string>>> GetAllAssignmentsAsync()
    {
        await EnsureLoadedAsync();
        return _assignments;
    }
}
```

**Step 3: Register service in App.xaml.cs**

Find where services are registered and add:

```csharp
services.AddSingleton<IRoomSceneAssignmentService, RoomSceneAssignmentService>();
```

**Step 4: Build and test**

**Step 5: Commit**

```bash
git add -A
git commit -m "feat: add room scene assignment service"
```

---

## Task 4: Display Pinned Animations on Room Detail

**Files:**
- Modify: `src/HueWindows.Core/ViewModels/RoomDetailViewModel.cs`
- Modify: `src/HueWindows/Views/RoomDetailPage.xaml`
- Modify: `src/HueWindows/Views/RoomDetailPage.xaml.cs`

**Step 1: Add pinned scenes collection and load method**

In `RoomDetailViewModel.cs`:

```csharp
private readonly IRoomSceneAssignmentService _assignmentService;

[ObservableProperty]
private ObservableCollection<AnimatedSceneModel> _pinnedAnimations = new();

// Update constructor to inject IRoomSceneAssignmentService
public RoomDetailViewModel(
    IHueBridgeService bridgeService,
    IAnimationService animationService,
    ISceneStorageService sceneStorageService,
    IRoomSceneAssignmentService assignmentService)
{
    _bridgeService = bridgeService;
    _animationService = animationService;
    _sceneStorageService = sceneStorageService;
    _assignmentService = assignmentService;
    // ... rest
}

private async Task LoadPinnedAnimationsAsync()
{
    var assignedIds = await _assignmentService.GetAssignedScenesAsync(_groupId);
    var allScenes = await _sceneStorageService.GetAllScenesAsync();

    PinnedAnimations.Clear();
    foreach (var id in assignedIds)
    {
        var scene = allScenes.FirstOrDefault(s => s.Id == id);
        if (scene != null)
        {
            PinnedAnimations.Add(scene);
        }
    }
}

[RelayCommand]
private async Task UnpinAnimationAsync(AnimatedSceneModel scene)
{
    await _assignmentService.RemoveSceneFromRoomAsync(_groupId, scene.Id);
    PinnedAnimations.Remove(scene);
}
```

Call `LoadPinnedAnimationsAsync()` in the existing `LoadAsync` method.

**Step 2: Add UI section**

In `RoomDetailPage.xaml`, add before the "Animations" section:

```xml
<!-- Pinned Animations Section -->
<StackPanel Visibility="{x:Bind HasItems(ViewModel.PinnedAnimations.Count), Mode=OneWay}">
    <TextBlock Text="Pinned Animations"
               Style="{StaticResource SectionHeaderTextStyle}"/>

    <ItemsRepeater ItemsSource="{x:Bind ViewModel.PinnedAnimations, Mode=OneWay}">
        <ItemsRepeater.Layout>
            <UniformGridLayout MinItemWidth="110" MinItemHeight="88"
                               MinRowSpacing="8" MinColumnSpacing="8"
                               ItemsStretch="Fill"/>
        </ItemsRepeater.Layout>
        <ItemsRepeater.ItemTemplate>
            <DataTemplate x:DataType="models:AnimatedSceneModel">
                <Button Style="{StaticResource SceneCardButtonStyle}"
                        Click="PinnedAnimation_Click"
                        Tag="{x:Bind}">
                    <Button.ContextFlyout>
                        <MenuFlyout>
                            <MenuFlyoutItem Text="Unpin" Click="UnpinAnimation_Click" Tag="{x:Bind}">
                                <MenuFlyoutItem.Icon>
                                    <FontIcon Glyph="&#xE77A;"/>
                                </MenuFlyoutItem.Icon>
                            </MenuFlyoutItem>
                        </MenuFlyout>
                    </Button.ContextFlyout>
                    <StackPanel HorizontalAlignment="Center" Spacing="4">
                        <FontIcon Glyph="&#xE768;" FontSize="16"
                                  Foreground="{ThemeResource AccentTextFillColorPrimaryBrush}"/>
                        <TextBlock Text="{x:Bind Name}"
                                   Style="{StaticResource CaptionTextBlockStyle}"
                                   TextTrimming="CharacterEllipsis"
                                   MaxWidth="90" TextAlignment="Center"/>
                    </StackPanel>
                </Button>
            </DataTemplate>
        </ItemsRepeater.ItemTemplate>
    </ItemsRepeater>
</StackPanel>
```

**Step 3: Add helper and handlers**

```csharp
private bool HasItems(int count) => count > 0;

private async void PinnedAnimation_Click(object sender, RoutedEventArgs e)
{
    if (sender is Button btn && btn.Tag is AnimatedSceneModel scene)
    {
        await ViewModel.PlayAnimatedSceneCommand.ExecuteAsync(scene);
    }
}

private async void UnpinAnimation_Click(object sender, RoutedEventArgs e)
{
    if (sender is MenuFlyoutItem item && item.Tag is AnimatedSceneModel scene)
    {
        await ViewModel.UnpinAnimationCommand.ExecuteAsync(scene);
    }
}
```

**Step 4: Build and test**

**Step 5: Commit**

```bash
git add -A
git commit -m "feat: display pinned animations on room detail"
```

---

## Task 5: Pin Animation from Scenes Page

**Files:**
- Modify: `src/HueWindows/Views/ScenesPage.xaml`
- Modify: `src/HueWindows/Views/ScenesPage.xaml.cs`
- Modify: `src/HueWindows.Core/ViewModels/ScenesViewModel.cs`

**Step 1: Add assignment service to ScenesViewModel**

Inject `IRoomSceneAssignmentService` and add command:

```csharp
private readonly IRoomSceneAssignmentService _assignmentService;

[RelayCommand]
private async Task PinToRoomAsync(AnimatedSceneModel scene)
{
    if (SelectedRoom == null) return;
    await _assignmentService.AssignSceneToRoomAsync(SelectedRoom.Id, scene.Id);
}
```

**Step 2: Add context menu to scene cards**

In `ScenesPage.xaml`, add to animated scene buttons:

```xml
<Button.ContextFlyout>
    <MenuFlyout>
        <MenuFlyoutItem Text="Pin to Room" Click="PinToRoom_Click" Tag="{x:Bind}">
            <MenuFlyoutItem.Icon>
                <FontIcon Glyph="&#xE718;"/>
            </MenuFlyoutItem.Icon>
        </MenuFlyoutItem>
    </MenuFlyout>
</Button.ContextFlyout>
```

**Step 3: Add handler**

```csharp
private async void PinToRoom_Click(object sender, RoutedEventArgs e)
{
    if (sender is MenuFlyoutItem item && item.Tag is AnimatedSceneModel scene)
    {
        if (ViewModel.SelectedRoom == null)
        {
            // Show message to select room first
            return;
        }
        await ViewModel.PinToRoomCommand.ExecuteAsync(scene);
        // Show confirmation
    }
}
```

**Step 4: Build and test**

**Step 5: Commit**

```bash
git add -A
git commit -m "feat: add pin-to-room from scenes page"
```

---

## Task 6: Animated Preview Control

**Files:**
- Create: `src/HueWindows/Controls/AnimatedPreviewControl.xaml`
- Create: `src/HueWindows/Controls/AnimatedPreviewControl.xaml.cs`

**Step 1: Create XAML**

```xml
<?xml version="1.0" encoding="utf-8"?>
<UserControl
    x:Class="HueWindows.Controls.AnimatedPreviewControl"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
    mc:Ignorable="d"
    Loaded="UserControl_Loaded"
    Unloaded="UserControl_Unloaded">

    <Canvas x:Name="PreviewCanvas"
            Width="80" Height="40"
            Background="#20FFFFFF"/>
</UserControl>
```

**Step 2: Create code-behind**

```csharp
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using HueWindows.Core.Models;
using Windows.UI;

namespace HueWindows.Controls;

public sealed partial class AnimatedPreviewControl : UserControl
{
    private DispatcherTimer? _animationTimer;
    private double _elapsed;
    private List<Ellipse> _lightDots = new();

    public static readonly DependencyProperty SceneProperty =
        DependencyProperty.Register(nameof(Scene), typeof(AnimatedSceneModel),
            typeof(AnimatedPreviewControl), new PropertyMetadata(null, OnSceneChanged));

    public AnimatedSceneModel? Scene
    {
        get => (AnimatedSceneModel?)GetValue(SceneProperty);
        set => SetValue(SceneProperty, value);
    }

    public static readonly DependencyProperty IsAnimatingProperty =
        DependencyProperty.Register(nameof(IsAnimating), typeof(bool),
            typeof(AnimatedPreviewControl), new PropertyMetadata(true, OnIsAnimatingChanged));

    public bool IsAnimating
    {
        get => (bool)GetValue(IsAnimatingProperty);
        set => SetValue(IsAnimatingProperty, value);
    }

    public AnimatedPreviewControl()
    {
        InitializeComponent();
    }

    private static void OnSceneChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AnimatedPreviewControl ctrl)
            ctrl.SetupPreview();
    }

    private static void OnIsAnimatingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AnimatedPreviewControl ctrl)
        {
            if ((bool)e.NewValue)
                ctrl.StartAnimation();
            else
                ctrl.StopAnimation();
        }
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        SetupPreview();
        if (IsAnimating) StartAnimation();
    }

    private void UserControl_Unloaded(object sender, RoutedEventArgs e)
    {
        StopAnimation();
    }

    private void SetupPreview()
    {
        PreviewCanvas.Children.Clear();
        _lightDots.Clear();

        if (Scene?.PaletteColors == null || Scene.PaletteColors.Count == 0)
            return;

        // Create 4 dots representing lights
        var dotCount = Math.Min(4, Math.Max(2, Scene.PaletteColors.Count));
        var spacing = 70.0 / (dotCount + 1);

        for (int i = 0; i < dotCount; i++)
        {
            var dot = new Ellipse
            {
                Width = 12,
                Height = 12,
                Fill = new SolidColorBrush(Colors.White)
            };
            Canvas.SetLeft(dot, 5 + spacing * (i + 1) - 6);
            Canvas.SetTop(dot, 14);
            PreviewCanvas.Children.Add(dot);
            _lightDots.Add(dot);
        }

        UpdateColors(0);
    }

    private void StartAnimation()
    {
        if (_animationTimer != null) return;

        _animationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(66) // ~15fps
        };
        _animationTimer.Tick += OnAnimationTick;
        _animationTimer.Start();
    }

    private void StopAnimation()
    {
        _animationTimer?.Stop();
        _animationTimer = null;
    }

    private void OnAnimationTick(object? sender, object e)
    {
        if (Scene == null) return;

        var duration = GetSceneDuration();
        _elapsed = (_elapsed + 0.066) % duration;
        UpdateColors(_elapsed);
    }

    private double GetSceneDuration()
    {
        var keyframeAnim = Scene?.Animations?.FirstOrDefault(a => a.Type == AnimationType.Keyframe);
        return keyframeAnim?.DurationSeconds ?? 10;
    }

    private void UpdateColors(double time)
    {
        if (Scene?.PaletteColors == null || _lightDots.Count == 0)
            return;

        var colors = Scene.PaletteColors;
        var duration = GetSceneDuration();
        var progress = time / duration;

        for (int i = 0; i < _lightDots.Count; i++)
        {
            // Offset each light slightly for visual interest
            var offset = (progress + i * 0.25) % 1.0;
            var colorIndex = (int)(offset * colors.Count) % colors.Count;
            var nextIndex = (colorIndex + 1) % colors.Count;
            var t = (offset * colors.Count) % 1.0;

            var c1 = colors[colorIndex];
            var c2 = colors[nextIndex];

            // Simple CIE xy to RGB approximation
            var rgb1 = HueColorToRgb(c1);
            var rgb2 = HueColorToRgb(c2);

            var r = (byte)(rgb1.R + (rgb2.R - rgb1.R) * t);
            var g = (byte)(rgb1.G + (rgb2.G - rgb1.G) * t);
            var b = (byte)(rgb1.B + (rgb2.B - rgb1.B) * t);

            _lightDots[i].Fill = new SolidColorBrush(Color.FromArgb(255, r, g, b));
        }
    }

    private static (byte R, byte G, byte B) HueColorToRgb(HueColor color)
    {
        // Simplified CIE xy to RGB
        var x = color.X;
        var y = color.Y;
        var z = 1.0 - x - y;
        var Y = 1.0;
        var X = (Y / y) * x;
        var Z = (Y / y) * z;

        var r = X * 3.2406 - Y * 1.5372 - Z * 0.4986;
        var g = -X * 0.9689 + Y * 1.8758 + Z * 0.0415;
        var b = X * 0.0557 - Y * 0.2040 + Z * 1.0570;

        r = Math.Max(0, Math.Min(1, r));
        g = Math.Max(0, Math.Min(1, g));
        b = Math.Max(0, Math.Min(1, b));

        return ((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
    }
}
```

**Step 3: Build and test**

**Step 4: Commit**

```bash
git add -A
git commit -m "feat: add animated preview control"
```

---

## Task 7: Use Animated Preview in Scene Cards

**Files:**
- Modify: `src/HueWindows/Views/ScenesPage.xaml`

**Step 1: Add namespace and update template**

Add namespace:
```xml
xmlns:controls="using:HueWindows.Controls"
```

Update animated scene card template to include preview:

```xml
<DataTemplate x:DataType="models:AnimatedSceneModel">
    <Button Click="AnimatedScene_Click" Tag="{x:Bind}"
            HorizontalAlignment="Stretch" VerticalAlignment="Stretch"
            Padding="8">
        <StackPanel Spacing="4">
            <controls:AnimatedPreviewControl Scene="{x:Bind}"
                                              Width="80" Height="40"
                                              HorizontalAlignment="Center"/>
            <TextBlock Text="{x:Bind Name}"
                       Style="{StaticResource CaptionTextBlockStyle}"
                       TextTrimming="CharacterEllipsis"
                       MaxWidth="100" TextAlignment="Center"/>
        </StackPanel>
    </Button>
</DataTemplate>
```

**Step 2: Build and test**

**Step 3: Commit**

```bash
git add -A
git commit -m "feat: use animated previews in scene cards"
```

---

## Task 8: Generate Remaining Scenes

**Files:**
- Create: `src/HueWindows/Assets/Scenes/scene_fireflies.json`
- Create: `src/HueWindows/Assets/Scenes/scene_autumn_warmth.json`
- Create: `src/HueWindows/Assets/Scenes/scene_rainforest.json`
- Create: `src/HueWindows/Assets/Scenes/scene_disco.json`
- Create: `src/HueWindows/Assets/Scenes/scene_chill_lounge.json`
- Create: `src/HueWindows/Assets/Scenes/scene_reading.json`
- Create: `src/HueWindows/Assets/Scenes/scene_meditation.json`
- Create: `src/HueWindows/Assets/Scenes/scene_gaming_rgb.json`

Use the LLM prompt template at `tools/scene-generator-prompt.md` to generate each scene.

**Scene descriptions:**
1. **Fireflies** - Soft dark green ambient with random yellow-green sparkles
2. **Autumn Warmth** - Slow transitions between orange, amber, and deep red
3. **Rainforest** - Lush greens with occasional blue rain-like flashes
4. **Disco** - Fast cycling through vibrant RGB colors
5. **Chill Lounge** - Slow purple/pink/blue ambient rotation
6. **Reading** - Static warm white, medium brightness
7. **Meditation** - Very slow deep blue/purple breathing effect
8. **Gaming RGB** - Medium-paced rainbow color cycling

**Step 1: Generate and save each scene**

**Step 2: Build and verify scenes load**

**Step 3: Commit**

```bash
git add src/HueWindows/Assets/Scenes/scene_*.json
git commit -m "feat: add 8 new preset scenes"
```

---

## Task 9: Final Integration and Testing

**Step 1: Full build**

```bash
dotnet build src/HueWindows/HueWindows.csproj
```

**Step 2: Manual testing checklist**
- [ ] Save current room as scene works
- [ ] Delete scene works
- [ ] Pin animation to room works
- [ ] Pinned animations appear on room detail
- [ ] Unpin animation works
- [ ] Animated previews render and animate
- [ ] All new scenes load and play correctly

**Step 3: Final commit**

```bash
git add -A
git commit -m "feat: complete remaining scene features from issue #3"
git push
```

---

## Summary

| Task | Description | Files |
|------|-------------|-------|
| 1 | Save room as scene | Service + VM + UI |
| 2 | Delete user scenes | Service + VM + UI |
| 3 | Scene assignment service | New service |
| 4 | Display pinned animations | VM + UI |
| 5 | Pin from scenes page | VM + UI |
| 6 | Animated preview control | New control |
| 7 | Use previews in cards | UI update |
| 8 | Generate new scenes | 8 JSON files |
| 9 | Final integration | Testing |
