# Native Hue Effects Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add native Hue bridge effects (fire, candle, sparkle, etc.) with flyout UI for quick apply and optional saving.

**Architecture:** Native effects are applied directly to the Hue bridge via the existing `ApplyEffectAsync` API, extended to support speed and brightness parameters. A reusable flyout control provides the configuration UI, used on both Room Detail and Scenes pages.

**Tech Stack:** WinUI 3, CommunityToolkit.Mvvm, HueApi NuGet package

---

## Task 1: Add Missing Effects to HueBridgeService

**Files:**
- Modify: `src/HueWindows.Core/Services/HueBridgeService.cs:607-617`

**Step 1: Add the 4 missing effect mappings**

In the `ApplyEffectAsync` method, update the switch expression to include underwater, cosmos, sunbeam, and enchant:

```csharp
var effectEnum = effect.ToLowerInvariant() switch
{
    "fire" => Effect.fire,
    "candle" => Effect.candle,
    "sparkle" => Effect.sparkle,
    "glisten" => Effect.glisten,
    "opal" => Effect.opal,
    "prism" => Effect.prism,
    "underwater" => Effect.underwater,  // NEW
    "cosmos" => Effect.cosmos,          // NEW
    "sunbeam" => Effect.sunbeam,        // NEW
    "enchant" => Effect.enchant,        // NEW
    "none" => Effect.no_effect,
    _ => Effect.no_effect
};
```

**Step 2: Verify HueApi supports these effects**

Check that `HueApi.Models.Effect` enum includes these values. If not, we may need to use string-based API calls.

**Step 3: Commit**

```bash
git add src/HueWindows.Core/Services/HueBridgeService.cs
git commit -m "feat(effects): add underwater, cosmos, sunbeam, enchant effects"
```

---

## Task 2: Extend ApplyEffectAsync with Speed and Brightness

**Files:**
- Modify: `src/HueWindows.Core/Services/Interfaces/IHueBridgeService.cs:151`
- Modify: `src/HueWindows.Core/Services/HueBridgeService.cs:602-624`

**Step 1: Update interface signature**

```csharp
/// <summary>
/// Applies a native Hue effect to a light (fire, candle, etc.).
/// </summary>
/// <param name="lightId">The light ID.</param>
/// <param name="effect">The effect to apply.</param>
/// <param name="speed">Effect speed (0.0-1.0). Null uses default.</param>
/// <param name="brightness">Brightness level (0.0-1.0). Null uses current.</param>
Task ApplyEffectAsync(Guid lightId, string effect, double? speed = null, double? brightness = null);
```

**Step 2: Update implementation**

```csharp
public async Task ApplyEffectAsync(Guid lightId, string effect, double? speed = null, double? brightness = null)
{
    if (_hueApi == null) return;

    var effectEnum = effect.ToLowerInvariant() switch
    {
        "fire" => Effect.fire,
        "candle" => Effect.candle,
        "sparkle" => Effect.sparkle,
        "glisten" => Effect.glisten,
        "opal" => Effect.opal,
        "prism" => Effect.prism,
        "underwater" => Effect.underwater,
        "cosmos" => Effect.cosmos,
        "sunbeam" => Effect.sunbeam,
        "enchant" => Effect.enchant,
        "none" => Effect.no_effect,
        _ => Effect.no_effect
    };

    var command = new UpdateLight
    {
        Effects = new HueApi.Models.Effects { Effect = effectEnum }
    };

    // Add brightness if specified
    if (brightness.HasValue)
    {
        command.Dimming = new HueApi.Models.Dimming
        {
            Brightness = brightness.Value * 100 // API expects 0-100
        };
    }

    // Note: Speed may require EffectsV2 - check HueApi support
    // For now, apply what we can

    await _hueApi.Light.UpdateAsync(lightId, command);
}
```

**Step 3: Commit**

```bash
git add src/HueWindows.Core/Services/Interfaces/IHueBridgeService.cs
git add src/HueWindows.Core/Services/HueBridgeService.cs
git commit -m "feat(effects): extend ApplyEffectAsync with speed and brightness params"
```

---

## Task 3: Create NativeEffectInfo Model

**Files:**
- Create: `src/HueWindows.Core/Models/NativeEffectInfo.cs`

**Step 1: Create the model**

```csharp
namespace HueWindows.Core.Models;

/// <summary>
/// Information about a native Hue effect.
/// </summary>
public class NativeEffectInfo
{
    /// <summary>
    /// Effect identifier (matches API value).
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Display name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Short description.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Segoe Fluent Icons glyph for display.
    /// </summary>
    public string IconGlyph { get; init; } = "\uE7B1"; // Sparkle

    /// <summary>
    /// Default speed (0.0-1.0).
    /// </summary>
    public double DefaultSpeed { get; init; } = 0.5;

    /// <summary>
    /// Default brightness (0.0-1.0).
    /// </summary>
    public double DefaultBrightness { get; init; } = 1.0;

    /// <summary>
    /// Gets all available native effects.
    /// </summary>
    public static IReadOnlyList<NativeEffectInfo> All { get; } = new List<NativeEffectInfo>
    {
        new() { Id = "fire", Name = "Fire", Description = "Warm flickering flames", IconGlyph = "\uE7B1" },
        new() { Id = "candle", Name = "Candle", Description = "Soft candle flicker", IconGlyph = "\uE7B1" },
        new() { Id = "sparkle", Name = "Sparkle", Description = "Twinkling sparkle", IconGlyph = "\uE7B1" },
        new() { Id = "glisten", Name = "Glisten", Description = "Gentle shimmer", IconGlyph = "\uE7B1" },
        new() { Id = "opal", Name = "Opal", Description = "Soft opalescent flow", IconGlyph = "\uE7B1" },
        new() { Id = "prism", Name = "Prism", Description = "Color-shifting prism", IconGlyph = "\uE7B1" },
        new() { Id = "underwater", Name = "Underwater", Description = "Blue-green underwater", IconGlyph = "\uE7B1" },
        new() { Id = "cosmos", Name = "Cosmos", Description = "Space galaxy drift", IconGlyph = "\uE7B1" },
        new() { Id = "sunbeam", Name = "Sunbeam", Description = "Warm sunlight rays", IconGlyph = "\uE7B1" },
        new() { Id = "enchant", Name = "Enchant", Description = "Magical transitions", IconGlyph = "\uE7B1" },
    };
}
```

**Step 2: Commit**

```bash
git add src/HueWindows.Core/Models/NativeEffectInfo.cs
git commit -m "feat(effects): add NativeEffectInfo model with effect catalog"
```

---

## Task 4: Create NativeEffectFlyout Control

**Files:**
- Create: `src/HueWindows/Controls/NativeEffectFlyout.xaml`
- Create: `src/HueWindows/Controls/NativeEffectFlyout.xaml.cs`

**Step 1: Create the XAML**

```xml
<?xml version="1.0" encoding="utf-8"?>
<UserControl
    x:Class="HueWindows.Controls.NativeEffectFlyout"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:local="using:HueWindows.Controls"
    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
    mc:Ignorable="d">

    <Grid x:Name="RootGrid" CornerRadius="12" Padding="20" MinWidth="280"
          RenderTransformOrigin="0.5,0">
        <Grid.RenderTransform>
            <ScaleTransform x:Name="RootScaleTransform" ScaleX="1" ScaleY="1"/>
        </Grid.RenderTransform>
        <Grid.Background>
            <LinearGradientBrush StartPoint="0,0" EndPoint="1,1">
                <GradientStop Color="#FF1A1A1E" Offset="0"/>
                <GradientStop Color="#FF252528" Offset="0.5"/>
                <GradientStop Color="#FF1E1E22" Offset="1"/>
            </LinearGradientBrush>
        </Grid.Background>

        <StackPanel Spacing="16">
            <!-- Header -->
            <StackPanel Spacing="4">
                <StackPanel Orientation="Horizontal" Spacing="8">
                    <FontIcon x:Name="EffectIcon" Glyph="&#xE7B1;" FontSize="18" Foreground="White"/>
                    <TextBlock x:Name="EffectNameText"
                               Style="{StaticResource SubtitleTextBlockStyle}"
                               Foreground="White"/>
                </StackPanel>
                <TextBlock x:Name="EffectDescriptionText"
                           Style="{StaticResource CaptionTextBlockStyle}"
                           Foreground="{ThemeResource TextFillColorSecondaryBrush}"/>
            </StackPanel>

            <!-- Speed Slider -->
            <StackPanel Spacing="8">
                <Grid>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="*"/>
                        <ColumnDefinition Width="Auto"/>
                    </Grid.ColumnDefinitions>
                    <TextBlock Text="Speed" Style="{StaticResource BodyTextBlockStyle}"/>
                    <TextBlock x:Name="SpeedValueText" Grid.Column="1"
                               Style="{StaticResource BodyTextBlockStyle}"
                               Foreground="{ThemeResource TextFillColorSecondaryBrush}"/>
                </Grid>
                <Slider x:Name="SpeedSlider"
                        Minimum="0" Maximum="100" Value="50"
                        ValueChanged="SpeedSlider_ValueChanged"/>
            </StackPanel>

            <!-- Brightness Slider -->
            <StackPanel Spacing="8">
                <Grid>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="*"/>
                        <ColumnDefinition Width="Auto"/>
                    </Grid.ColumnDefinitions>
                    <TextBlock Text="Brightness" Style="{StaticResource BodyTextBlockStyle}"/>
                    <TextBlock x:Name="BrightnessValueText" Grid.Column="1"
                               Style="{StaticResource BodyTextBlockStyle}"
                               Foreground="{ThemeResource TextFillColorSecondaryBrush}"/>
                </Grid>
                <Slider x:Name="BrightnessSlider"
                        Minimum="0" Maximum="100" Value="100"
                        ValueChanged="BrightnessSlider_ValueChanged"/>
            </StackPanel>

            <!-- Buttons -->
            <Grid Margin="0,8,0,0">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="8"/>
                    <ColumnDefinition Width="*"/>
                </Grid.ColumnDefinitions>
                <Button x:Name="SaveButton" Grid.Column="0"
                        Content="Save as Scene"
                        HorizontalAlignment="Stretch"
                        Click="SaveButton_Click"/>
                <Button x:Name="ApplyButton" Grid.Column="2"
                        Content="Apply"
                        Style="{StaticResource AccentButtonStyle}"
                        HorizontalAlignment="Stretch"
                        Click="ApplyButton_Click"/>
            </Grid>
        </StackPanel>
    </Grid>
</UserControl>
```

**Step 2: Create the code-behind**

```csharp
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using HueWindows.Constants;
using HueWindows.Core.Models;

namespace HueWindows.Controls;

/// <summary>
/// Flyout control for configuring and applying native Hue effects.
/// </summary>
public sealed partial class NativeEffectFlyout : UserControl
{
    private NativeEffectInfo? _effect;

    /// <summary>
    /// Event raised when user clicks Apply.
    /// </summary>
    public event EventHandler<NativeEffectApplyEventArgs>? ApplyRequested;

    /// <summary>
    /// Event raised when user clicks Save as Scene.
    /// </summary>
    public event EventHandler<NativeEffectApplyEventArgs>? SaveRequested;

    /// <summary>
    /// Gets or sets the effect to configure.
    /// </summary>
    public NativeEffectInfo? Effect
    {
        get => _effect;
        set
        {
            _effect = value;
            UpdateEffectDisplay();
        }
    }

    /// <summary>
    /// Gets the current speed value (0.0-1.0).
    /// </summary>
    public double Speed => SpeedSlider.Value / 100.0;

    /// <summary>
    /// Gets the current brightness value (0.0-1.0).
    /// </summary>
    public double Brightness => BrightnessSlider.Value / 100.0;

    public NativeEffectFlyout()
    {
        this.InitializeComponent();
        UpdateValueLabels();
    }

    private void UpdateEffectDisplay()
    {
        if (_effect == null) return;

        EffectNameText.Text = _effect.Name;
        EffectDescriptionText.Text = _effect.Description;
        EffectIcon.Glyph = _effect.IconGlyph;
        SpeedSlider.Value = _effect.DefaultSpeed * 100;
        BrightnessSlider.Value = _effect.DefaultBrightness * 100;
        UpdateValueLabels();
    }

    private void UpdateValueLabels()
    {
        SpeedValueText.Text = $"{(int)SpeedSlider.Value}%";
        BrightnessValueText.Text = $"{(int)BrightnessSlider.Value}%";
    }

    private void SpeedSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        UpdateValueLabels();
    }

    private void BrightnessSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        UpdateValueLabels();
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_effect == null) return;
        ApplyRequested?.Invoke(this, new NativeEffectApplyEventArgs(_effect, Speed, Brightness));
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_effect == null) return;
        SaveRequested?.Invoke(this, new NativeEffectApplyEventArgs(_effect, Speed, Brightness));
    }

    /// <summary>
    /// Plays the entrance animation.
    /// </summary>
    public void AnimateEntrance()
    {
        RootGrid.Opacity = 0;
        RootScaleTransform.ScaleX = 0.95;
        RootScaleTransform.ScaleY = 0.95;

        var duration = TimeSpan.FromMilliseconds(AppConstants.Animation.FastDurationMs);
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };

        var opacityAnim = new DoubleAnimation { To = 1.0, Duration = new Duration(duration), EasingFunction = easing };
        var scaleXAnim = new DoubleAnimation { To = 1.0, Duration = new Duration(duration), EasingFunction = easing };
        var scaleYAnim = new DoubleAnimation { To = 1.0, Duration = new Duration(duration), EasingFunction = easing };

        var storyboard = new Storyboard();
        storyboard.Children.Add(opacityAnim);
        storyboard.Children.Add(scaleXAnim);
        storyboard.Children.Add(scaleYAnim);

        Storyboard.SetTarget(opacityAnim, RootGrid);
        Storyboard.SetTargetProperty(opacityAnim, "Opacity");
        Storyboard.SetTarget(scaleXAnim, RootScaleTransform);
        Storyboard.SetTargetProperty(scaleXAnim, "ScaleX");
        Storyboard.SetTarget(scaleYAnim, RootScaleTransform);
        Storyboard.SetTargetProperty(scaleYAnim, "ScaleY");

        storyboard.Begin();
    }
}

/// <summary>
/// Event args for native effect apply/save requests.
/// </summary>
public class NativeEffectApplyEventArgs : EventArgs
{
    public NativeEffectInfo Effect { get; }
    public double Speed { get; }
    public double Brightness { get; }

    public NativeEffectApplyEventArgs(NativeEffectInfo effect, double speed, double brightness)
    {
        Effect = effect;
        Speed = speed;
        Brightness = brightness;
    }
}
```

**Step 3: Commit**

```bash
git add src/HueWindows/Controls/NativeEffectFlyout.xaml
git add src/HueWindows/Controls/NativeEffectFlyout.xaml.cs
git commit -m "feat(effects): add NativeEffectFlyout control"
```

---

## Task 5: Add Native Effects to RoomDetailPage

**Files:**
- Modify: `src/HueWindows/Views/RoomDetailPage.xaml:229-291`
- Modify: `src/HueWindows/Views/RoomDetailPage.xaml.cs`
- Modify: `src/HueWindows.Core/ViewModels/RoomDetailViewModel.cs`

**Step 1: Add NativeEffects property to ViewModel**

In `RoomDetailViewModel.cs`, add:

```csharp
/// <summary>
/// Available native Hue effects.
/// </summary>
public IReadOnlyList<NativeEffectInfo> NativeEffects => NativeEffectInfo.All;
```

**Step 2: Add Hue Effects section to XAML**

After the Animations section (around line 291), add:

```xml
<!-- Hue Effects Section -->
<StackPanel>
    <TextBlock Text="Hue Effects"
               Style="{StaticResource SectionHeaderTextStyle}"/>

    <ItemsRepeater ItemsSource="{x:Bind ViewModel.NativeEffects}">
        <ItemsRepeater.Layout>
            <UniformGridLayout MinItemWidth="110"
                               MinItemHeight="88"
                               MinRowSpacing="8"
                               MinColumnSpacing="8"
                               ItemsStretch="Fill"/>
        </ItemsRepeater.Layout>
        <ItemsRepeater.ItemTemplate>
            <DataTemplate x:DataType="models:NativeEffectInfo">
                <Button Style="{StaticResource EffectCardButtonStyle}"
                        Click="NativeEffect_Click"
                        Tag="{x:Bind}">
                    <StackPanel HorizontalAlignment="Center" Spacing="4">
                        <FontIcon Glyph="&#xE7B1;" FontSize="16"
                                  Foreground="{ThemeResource AccentTextFillColorPrimaryBrush}"/>
                        <TextBlock Text="{x:Bind Name}"
                                   Style="{StaticResource CaptionTextBlockStyle}"
                                   TextTrimming="CharacterEllipsis"
                                   MaxWidth="90"
                                   TextAlignment="Center"
                                   HorizontalAlignment="Center"/>
                    </StackPanel>
                </Button>
            </DataTemplate>
        </ItemsRepeater.ItemTemplate>
    </ItemsRepeater>
</StackPanel>
```

**Step 3: Add EffectCardButtonStyle to AppStyles.xaml**

```xml
<!-- Effect card button with subtle warm tint -->
<Style x:Key="EffectCardButtonStyle" TargetType="Button" BasedOn="{StaticResource SceneCardButtonStyle}">
    <Setter Property="Background">
        <Setter.Value>
            <LinearGradientBrush StartPoint="0,0" EndPoint="1,1">
                <GradientStop Color="#FF1E1A1A" Offset="0"/>
                <GradientStop Color="#FF282225" Offset="0.5"/>
                <GradientStop Color="#FF221E1E" Offset="1"/>
            </LinearGradientBrush>
        </Setter.Value>
    </Setter>
</Style>
```

**Step 4: Add click handler and flyout in code-behind**

In `RoomDetailPage.xaml.cs`, add:

```csharp
private void NativeEffect_Click(object sender, RoutedEventArgs e)
{
    if (sender is Button button && button.Tag is NativeEffectInfo effect)
    {
        var flyout = new Flyout
        {
            ShouldConstrainToRootBounds = false,
            FlyoutPresenterStyle = (Style)Resources["EffectFlyoutPresenterStyle"]
        };

        var flyoutContent = new NativeEffectFlyout { Effect = effect };
        flyoutContent.ApplyRequested += async (s, args) =>
        {
            flyout.Hide();
            await ApplyNativeEffectAsync(args);
        };
        flyoutContent.SaveRequested += async (s, args) =>
        {
            flyout.Hide();
            await SaveNativeEffectAsSceneAsync(args);
        };

        flyout.Content = flyoutContent;
        flyout.Opening += (s, args) => flyoutContent.AnimateEntrance();
        flyout.ShowAt(button);
    }
}

private async Task ApplyNativeEffectAsync(NativeEffectApplyEventArgs args)
{
    foreach (var light in ViewModel.Lights)
    {
        await ViewModel.ApplyEffectToLightAsync(light.LightId, args.Effect.Id, args.Speed, args.Brightness);
    }
}

private async Task SaveNativeEffectAsSceneAsync(NativeEffectApplyEventArgs args)
{
    // TODO: Implement save dialog and scene creation
    await ApplyNativeEffectAsync(args); // Apply for now
}
```

**Step 5: Add ApplyEffectToLightAsync to ViewModel**

In `RoomDetailViewModel.cs`:

```csharp
public async Task ApplyEffectToLightAsync(Guid lightId, string effect, double speed, double brightness)
{
    await _bridgeService.ApplyEffectAsync(lightId, effect, speed, brightness);
}
```

**Step 6: Add using directive and models namespace to XAML**

Ensure `xmlns:models="using:HueWindows.Core.Models"` is present.

**Step 7: Add flyout presenter style to page resources**

```xml
<Page.Resources>
    <Style x:Key="EffectFlyoutPresenterStyle" TargetType="FlyoutPresenter">
        <Setter Property="Background" Value="Transparent"/>
        <Setter Property="Padding" Value="0"/>
        <Setter Property="CornerRadius" Value="12"/>
    </Style>
</Page.Resources>
```

**Step 8: Commit**

```bash
git add src/HueWindows/Views/RoomDetailPage.xaml
git add src/HueWindows/Views/RoomDetailPage.xaml.cs
git add src/HueWindows.Core/ViewModels/RoomDetailViewModel.cs
git add src/HueWindows/Styles/AppStyles.xaml
git commit -m "feat(effects): add Hue Effects section to Room Detail page"
```

---

## Task 6: Add Hue Effects Section to ScenesPage

**Files:**
- Modify: `src/HueWindows/Views/ScenesPage.xaml:156-204`
- Modify: `src/HueWindows/Views/ScenesPage.xaml.cs`
- Modify: `src/HueWindows.Core/ViewModels/ScenesPageViewModel.cs`

**Step 1: Add NativeEffects property to ViewModel**

```csharp
public IReadOnlyList<NativeEffectInfo> NativeEffects => NativeEffectInfo.All;
```

**Step 2: Add Hue Effects section to XAML**

Insert after "My Scenes" section (around line 156), before "Preset Scenes":

```xml
<!-- Hue Effects Section -->
<StackPanel Spacing="16">
    <TextBlock Text="Hue Effects"
               Style="{StaticResource SubtitleTextBlockStyle}"/>

    <ItemsRepeater ItemsSource="{x:Bind ViewModel.NativeEffects}">
        <ItemsRepeater.Layout>
            <UniformGridLayout MinItemWidth="200"
                               MinItemHeight="100"
                               MinRowSpacing="12"
                               MinColumnSpacing="12"
                               ItemsStretch="Fill"/>
        </ItemsRepeater.Layout>
        <ItemsRepeater.ItemTemplate>
            <DataTemplate x:DataType="models:NativeEffectInfo">
                <Button Click="NativeEffect_Click"
                        Tag="{x:Bind}"
                        HorizontalAlignment="Stretch"
                        VerticalAlignment="Stretch"
                        Padding="16"
                        Style="{StaticResource EffectCardButtonStyle}">
                    <StackPanel Spacing="8">
                        <StackPanel Orientation="Horizontal" Spacing="8">
                            <FontIcon Glyph="&#xE7B1;" FontSize="16"
                                      Foreground="{ThemeResource AccentTextFillColorPrimaryBrush}"/>
                            <TextBlock Text="{x:Bind Name}"
                                       Style="{StaticResource BodyStrongTextBlockStyle}"/>
                        </StackPanel>
                        <TextBlock Text="{x:Bind Description}"
                                   Style="{StaticResource CaptionTextBlockStyle}"
                                   Foreground="{ThemeResource TextFillColorSecondaryBrush}"/>
                    </StackPanel>
                </Button>
            </DataTemplate>
        </ItemsRepeater.ItemTemplate>
    </ItemsRepeater>
</StackPanel>
```

**Step 3: Add click handler in code-behind**

Similar to RoomDetailPage but applies to selected room:

```csharp
private void NativeEffect_Click(object sender, RoutedEventArgs e)
{
    if (ViewModel.SelectedRoom == null)
    {
        // Show message to select a room first
        return;
    }

    if (sender is Button button && button.Tag is NativeEffectInfo effect)
    {
        var flyout = new Flyout
        {
            ShouldConstrainToRootBounds = false,
            FlyoutPresenterStyle = (Style)Resources["EffectFlyoutPresenterStyle"]
        };

        var flyoutContent = new NativeEffectFlyout { Effect = effect };
        flyoutContent.ApplyRequested += async (s, args) =>
        {
            flyout.Hide();
            await ApplyNativeEffectAsync(args);
        };
        flyoutContent.SaveRequested += async (s, args) =>
        {
            flyout.Hide();
            await SaveNativeEffectAsSceneAsync(args);
        };

        flyout.Content = flyoutContent;
        flyout.Opening += (s, args) => flyoutContent.AnimateEntrance();
        flyout.ShowAt(button);
    }
}

private async Task ApplyNativeEffectAsync(NativeEffectApplyEventArgs args)
{
    if (ViewModel.SelectedRoom == null) return;
    await ViewModel.ApplyEffectToRoomAsync(args.Effect.Id, args.Speed, args.Brightness);
}

private async Task SaveNativeEffectAsSceneAsync(NativeEffectApplyEventArgs args)
{
    // TODO: Implement save dialog
    await ApplyNativeEffectAsync(args);
}
```

**Step 4: Add ApplyEffectToRoomAsync to ViewModel**

```csharp
public async Task ApplyEffectToRoomAsync(string effect, double speed, double brightness)
{
    if (SelectedRoom == null) return;

    var lights = await _bridgeService.GetLightsForRoomAsync(SelectedRoom.Id);
    if (lights.IsSuccess)
    {
        foreach (var light in lights.Value!)
        {
            await _bridgeService.ApplyEffectAsync(light.Id, effect, speed, brightness);
        }
    }
}
```

**Step 5: Commit**

```bash
git add src/HueWindows/Views/ScenesPage.xaml
git add src/HueWindows/Views/ScenesPage.xaml.cs
git add src/HueWindows.Core/ViewModels/ScenesPageViewModel.cs
git commit -m "feat(effects): add Hue Effects section to Scenes page"
```

---

## Task 7: Implement Save as Scene Dialog

**Files:**
- Create: `src/HueWindows/Dialogs/SaveEffectDialog.xaml`
- Create: `src/HueWindows/Dialogs/SaveEffectDialog.xaml.cs`
- Modify: `src/HueWindows/Views/RoomDetailPage.xaml.cs`
- Modify: `src/HueWindows/Views/ScenesPage.xaml.cs`

**Step 1: Create the dialog XAML**

```xml
<?xml version="1.0" encoding="utf-8"?>
<ContentDialog
    x:Class="HueWindows.Dialogs.SaveEffectDialog"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    Title="Save Effect as Scene"
    PrimaryButtonText="Save"
    CloseButtonText="Cancel"
    DefaultButton="Primary"
    PrimaryButtonClick="ContentDialog_PrimaryButtonClick">

    <StackPanel Spacing="16" MinWidth="300">
        <TextBox x:Name="SceneNameTextBox"
                 Header="Scene Name"
                 PlaceholderText="My Cozy Fire"/>

        <TextBlock Style="{StaticResource CaptionTextBlockStyle}"
                   Foreground="{ThemeResource TextFillColorSecondaryBrush}">
            <Run Text="Effect: "/>
            <Run x:Name="EffectNameRun" FontWeight="SemiBold"/>
        </TextBlock>

        <TextBlock Style="{StaticResource CaptionTextBlockStyle}"
                   Foreground="{ThemeResource TextFillColorSecondaryBrush}">
            <Run Text="Speed: "/>
            <Run x:Name="SpeedValueRun"/>
            <Run Text=" | Brightness: "/>
            <Run x:Name="BrightnessValueRun"/>
        </TextBlock>
    </StackPanel>
</ContentDialog>
```

**Step 2: Create dialog code-behind**

```csharp
using Microsoft.UI.Xaml.Controls;
using HueWindows.Core.Models;

namespace HueWindows.Dialogs;

public sealed partial class SaveEffectDialog : ContentDialog
{
    public string SceneName => SceneNameTextBox.Text;
    public NativeEffectInfo Effect { get; private set; } = null!;
    public double Speed { get; private set; }
    public double Brightness { get; private set; }

    public SaveEffectDialog()
    {
        this.InitializeComponent();
    }

    public void SetEffect(NativeEffectInfo effect, double speed, double brightness)
    {
        Effect = effect;
        Speed = speed;
        Brightness = brightness;

        SceneNameTextBox.Text = $"My {effect.Name}";
        EffectNameRun.Text = effect.Name;
        SpeedValueRun.Text = $"{(int)(speed * 100)}%";
        BrightnessValueRun.Text = $"{(int)(brightness * 100)}%";
    }

    private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (string.IsNullOrWhiteSpace(SceneNameTextBox.Text))
        {
            args.Cancel = true;
        }
    }
}
```

**Step 3: Update SaveNativeEffectAsSceneAsync in RoomDetailPage**

```csharp
private async Task SaveNativeEffectAsSceneAsync(NativeEffectApplyEventArgs args)
{
    var dialog = new SaveEffectDialog();
    dialog.XamlRoot = this.XamlRoot;
    dialog.SetEffect(args.Effect, args.Speed, args.Brightness);

    var result = await dialog.ShowAsync();

    if (result == ContentDialogResult.Primary)
    {
        // Create and save the scene
        var scene = new AnimatedSceneModel
        {
            Id = $"user_{Guid.NewGuid():N}",
            Name = dialog.SceneName,
            Description = $"{args.Effect.Name} effect",
            Category = "effect",
            Animations = new List<AnimationDefinition>
            {
                new AnimationDefinition
                {
                    Id = $"{args.Effect.Id}_effect",
                    Name = args.Effect.Id,
                    Type = AnimationType.NativeEffect,
                    LightAssignment = LightAssignment.All,
                    RepeatMode = RepeatMode.Loop
                }
            }
        };

        // Save via service
        await ViewModel.SaveUserSceneAsync(scene);

        // Apply the effect
        await ApplyNativeEffectAsync(args);
    }
}
```

**Step 4: Add SaveUserSceneAsync to ViewModel**

```csharp
public async Task SaveUserSceneAsync(AnimatedSceneModel scene)
{
    await _sceneStorageService.SaveSceneAsync(scene);
}
```

**Step 5: Commit**

```bash
git add src/HueWindows/Dialogs/SaveEffectDialog.xaml
git add src/HueWindows/Dialogs/SaveEffectDialog.xaml.cs
git add src/HueWindows/Views/RoomDetailPage.xaml.cs
git add src/HueWindows/Views/ScenesPage.xaml.cs
git commit -m "feat(effects): add Save Effect as Scene dialog"
```

---

## Task 8: Build and Test

**Step 1: Build the solution**

```bash
dotnet build src/HueWindows/HueWindows.csproj
```

Expected: Build succeeds with 0 errors.

**Step 2: Run and test**

```bash
dotnet run --project src/HueWindows/HueWindows.csproj
```

Test checklist:
- [ ] Navigate to Room Detail page
- [ ] "Hue Effects" section visible with 10 effect cards
- [ ] Cards show sparkle icon and effect name
- [ ] Tap effect card → flyout appears with sliders
- [ ] Adjust speed/brightness sliders
- [ ] Click "Apply" → effect applies to room lights
- [ ] Click "Save as Scene" → dialog appears
- [ ] Enter name and save → scene appears in My Scenes
- [ ] Navigate to Scenes page
- [ ] "Hue Effects" section visible
- [ ] Same flyout interaction works

**Step 3: Commit any fixes**

```bash
git add -A
git commit -m "fix: address issues found during testing"
```

**Step 4: Final commit**

```bash
git push
```

---

## Summary

| Task | Description | Files |
|------|-------------|-------|
| 1 | Add missing effects | HueBridgeService.cs |
| 2 | Extend API with params | IHueBridgeService.cs, HueBridgeService.cs |
| 3 | Create NativeEffectInfo | NativeEffectInfo.cs |
| 4 | Create flyout control | NativeEffectFlyout.xaml/.cs |
| 5 | RoomDetailPage integration | RoomDetailPage.xaml/.cs, ViewModel, Styles |
| 6 | ScenesPage integration | ScenesPage.xaml/.cs, ViewModel |
| 7 | Save dialog | SaveEffectDialog.xaml/.cs |
| 8 | Build and test | - |
