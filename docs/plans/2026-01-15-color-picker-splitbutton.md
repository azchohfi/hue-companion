# Color Picker SplitButton Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace icon-click color picker entry point with an explicit SplitButton showing current color, placed adjacent to the on/off toggle.

**Architecture:** Add a SplitButton control next to each toggle switch that displays the current room/light color as its background. Clicking opens a flyout with the ColorPickerFlyout. Remove the implicit icon-tap behavior. Apply the same color animation pattern used for toggle switches.

**Tech Stack:** WinUI 3, SplitButton, Flyout, ColorAnimation, existing ColorPickerFlyout control

---

## Task 1: Add SplitButton to RoomDetailPage Header

**Files:**
- Modify: `src/HueWindows/Views/RoomDetailPage.xaml:55-103`
- Modify: `src/HueWindows/Views/RoomDetailPage.xaml.cs`

**Step 1: Update XAML - Remove flyout from icon, add SplitButton**

In `RoomDetailPage.xaml`, locate the header Grid (around line 57) and:
1. Remove the `FlyoutBase.AttachedFlyout` from `HeaderIconContainer`
2. Remove the `Tapped="HeaderIconContainer_Tapped"` handler
3. Add a new column for the SplitButton
4. Add the SplitButton with ColorPickerFlyout

Replace the header Grid section (lines 57-103) with:

```xaml
<!-- Header: Icon, Name, Color Button, Toggle -->
<Grid>
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="Auto"/>
        <ColumnDefinition Width="*"/>
        <ColumnDefinition Width="Auto"/>
        <ColumnDefinition Width="Auto"/>
    </Grid.ColumnDefinitions>

    <!-- Room icon (no longer tappable for color) -->
    <Border x:Name="HeaderIconContainer" Grid.Column="0" Width="48" Height="48" CornerRadius="24"
            Background="#20FFFFFF" Margin="0,0,16,0">
        <FontIcon x:Name="RoomIcon"
                  Glyph="&#xE781;"
                  FontSize="22"
                  Foreground="#80FFFFFF"/>
    </Border>

    <StackPanel Grid.Column="1" VerticalAlignment="Center">
        <TextBlock Text="{x:Bind ViewModel.RoomName, Mode=OneWay}"
                   Style="{StaticResource PageHeaderTextStyle}"/>
        <TextBlock Style="{StaticResource PageSubtitleTextStyle}">
            <Run Text="{x:Bind ViewModel.Lights.Count, Mode=OneWay}"/>
            <Run Text=" lights"/>
        </TextBlock>
    </StackPanel>

    <!-- Color SplitButton -->
    <SplitButton x:Name="ColorSplitButton"
                 Grid.Column="2"
                 VerticalAlignment="Center"
                 Margin="0,0,16,0"
                 Padding="0"
                 CornerRadius="8"
                 Visibility="{x:Bind ViewModel.SupportsColor, Mode=OneWay}">
        <Border x:Name="ColorButtonContent" Width="40" Height="40" CornerRadius="6"
                Background="#40FFFFFF">
            <FontIcon Glyph="&#xE790;" FontSize="18" Foreground="White"/>
        </Border>
        <SplitButton.Flyout>
            <Flyout x:Name="ColorPickerFlyout"
                    Placement="Bottom"
                    ShouldConstrainToRootBounds="False">
                <Flyout.FlyoutPresenterStyle>
                    <Style TargetType="FlyoutPresenter">
                        <Setter Property="Background" Value="Transparent"/>
                        <Setter Property="Padding" Value="0"/>
                        <Setter Property="CornerRadius" Value="12"/>
                    </Style>
                </Flyout.FlyoutPresenterStyle>
                <controls:ColorPickerFlyout x:Name="ColorFlyout" ColorChanged="ColorFlyout_ColorChanged"/>
            </Flyout>
        </SplitButton.Flyout>
    </SplitButton>

    <ToggleSwitch x:Name="RoomToggle"
                  Grid.Column="3"
                  Style="{StaticResource GradientToggleSwitchStyle}"
                  VerticalAlignment="Center"
                  IsOn="{x:Bind ViewModel.IsOn, Mode=TwoWay}"
                  Tapped="RoomToggle_Tapped"/>
</Grid>
```

**Step 2: Update code-behind - Remove icon tap handler, add color button handling**

In `RoomDetailPage.xaml.cs`:

1. Remove the `HeaderIconContainer_Tapped` method (lines 378-393)

2. Add field for color button brush:
```csharp
private SolidColorBrush? _colorButtonBrush;
private Color _currentColorButtonColor = Colors.Gray;
```

3. Add method to update color button:
```csharp
private void UpdateColorButtonColor(bool isActive, List<(byte R, byte G, byte B)> colors)
{
    if (ColorButtonContent == null) return;

    var targetColor = isActive && colors.Count > 0
        ? Color.FromArgb(255, colors[0].R, colors[0].G, colors[0].B)
        : Color.FromArgb(64, 255, 255, 255); // #40FFFFFF when inactive

    if (_colorButtonBrush == null)
    {
        _colorButtonBrush = new SolidColorBrush(_currentColorButtonColor);
        ColorButtonContent.Background = _colorButtonBrush;
    }

    AnimateSolidBrushColor(_colorButtonBrush, _currentColorButtonColor, targetColor);
    _currentColorButtonColor = targetColor;
}
```

4. Call `UpdateColorButtonColor` from `UpdateHeaderActiveState`:
```csharp
private void UpdateHeaderActiveState()
{
    // ... existing code ...
    UpdateColorButtonColor(isActive, colors);
}
```

5. Update `ColorFlyout_ColorChanged` to set initial color on flyout open:
```csharp
private void ColorSplitButton_Click(SplitButton sender, SplitButtonClickEventArgs args)
{
    // Set initial color before flyout opens
    var colors = ViewModel.LightColors;
    if (colors.Count > 0)
    {
        var (r, g, b) = colors[0];
        ColorFlyout.InitialColor = Color.FromArgb(255, r, g, b);
    }
}
```

6. Add the Click handler to XAML: `Click="ColorSplitButton_Click"`

**Step 3: Build and verify**

Run: `dotnet build src/HueWindows/HueWindows.csproj`
Expected: Build succeeded with 0 errors

**Step 4: Commit**

```bash
git add src/HueWindows/Views/RoomDetailPage.xaml src/HueWindows/Views/RoomDetailPage.xaml.cs
git commit -m "feat(room): replace icon color picker with SplitButton"
```

---

## Task 2: Add SupportsColor Property to RoomDetailViewModel

**Files:**
- Modify: `src/HueWindows.Core/ViewModels/RoomDetailViewModel.cs`

**Step 1: Add SupportsColor property**

Add computed property that checks if any light in the room supports color:

```csharp
public bool SupportsColor => Lights.Any(l => l.SupportsColor);
```

**Step 2: Build and verify**

Run: `dotnet build src/HueWindows.Core/HueWindows.Core.csproj`
Expected: Build succeeded

**Step 3: Commit**

```bash
git add src/HueWindows.Core/ViewModels/RoomDetailViewModel.cs
git commit -m "feat(room): add SupportsColor property to RoomDetailViewModel"
```

---

## Task 3: Add SplitButton to LightDetailPage Header

**Files:**
- Modify: `src/HueWindows/Views/LightDetailPage.xaml:44-87`
- Modify: `src/HueWindows/Views/LightDetailPage.xaml.cs`

**Step 1: Update XAML - Remove flyout from icon, add SplitButton**

In `LightDetailPage.xaml`, replace the header Grid section with:

```xaml
<!-- Header: Icon, Name, Color Button, Toggle -->
<Grid>
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="Auto"/>
        <ColumnDefinition Width="*"/>
        <ColumnDefinition Width="Auto"/>
        <ColumnDefinition Width="Auto"/>
    </Grid.ColumnDefinitions>

    <!-- Light icon (no longer tappable for color) -->
    <Border x:Name="HeaderIconContainer" Grid.Column="0" Width="48" Height="48" CornerRadius="24"
            Background="#20FFFFFF" Margin="0,0,16,0">
        <FontIcon x:Name="LightIcon"
                  Glyph="&#xE781;"
                  FontSize="22"
                  Foreground="#80FFFFFF"/>
    </Border>

    <StackPanel Grid.Column="1" VerticalAlignment="Center">
        <TextBlock Text="{x:Bind ViewModel.LightName, Mode=OneWay}"
                   Style="{StaticResource PageHeaderTextStyle}"/>
    </StackPanel>

    <!-- Color SplitButton -->
    <SplitButton x:Name="ColorSplitButton"
                 Grid.Column="2"
                 VerticalAlignment="Center"
                 Margin="0,0,16,0"
                 Padding="0"
                 CornerRadius="8"
                 Click="ColorSplitButton_Click"
                 Visibility="{x:Bind ViewModel.SupportsColor, Mode=OneWay}">
        <Border x:Name="ColorButtonContent" Width="40" Height="40" CornerRadius="6"
                Background="#40FFFFFF">
            <FontIcon Glyph="&#xE790;" FontSize="18" Foreground="White"/>
        </Border>
        <SplitButton.Flyout>
            <Flyout x:Name="ColorPickerFlyout"
                    Placement="Bottom"
                    ShouldConstrainToRootBounds="False">
                <Flyout.FlyoutPresenterStyle>
                    <Style TargetType="FlyoutPresenter">
                        <Setter Property="Background" Value="Transparent"/>
                        <Setter Property="Padding" Value="0"/>
                        <Setter Property="CornerRadius" Value="12"/>
                    </Style>
                </Flyout.FlyoutPresenterStyle>
                <controls:ColorPickerFlyout x:Name="ColorFlyout" ColorChanged="ColorFlyout_ColorChanged"/>
            </Flyout>
        </SplitButton.Flyout>
    </SplitButton>

    <ToggleSwitch x:Name="LightToggle"
                  Grid.Column="3"
                  Style="{StaticResource GradientToggleSwitchStyle}"
                  VerticalAlignment="Center"
                  IsOn="{x:Bind ViewModel.IsOn, Mode=TwoWay}"
                  Tapped="LightToggle_Tapped"/>
</Grid>
```

**Step 2: Update code-behind**

In `LightDetailPage.xaml.cs`:

1. Remove the `HeaderIconContainer_Tapped` method

2. Add fields:
```csharp
private SolidColorBrush? _colorButtonBrush;
private Color _currentColorButtonColor = Colors.Gray;
```

3. Add `UpdateColorButtonColor` method:
```csharp
private void UpdateColorButtonColor(bool isActive)
{
    if (ColorButtonContent == null) return;

    var targetColor = isActive
        ? _accentColor
        : Color.FromArgb(64, 255, 255, 255);

    if (_colorButtonBrush == null)
    {
        _colorButtonBrush = new SolidColorBrush(_currentColorButtonColor);
        ColorButtonContent.Background = _colorButtonBrush;
    }

    AnimateSolidBrushColor(_colorButtonBrush, _currentColorButtonColor, targetColor);
    _currentColorButtonColor = targetColor;
}
```

4. Call from `UpdateHeaderActiveState`:
```csharp
UpdateColorButtonColor(isActive);
```

5. Add click handler:
```csharp
private void ColorSplitButton_Click(SplitButton sender, SplitButtonClickEventArgs args)
{
    if (ViewModel.CurrentColor != null)
    {
        var rgb = ViewModel.CurrentColor.ToRgb(1.0);
        ColorFlyout.InitialColor = Color.FromArgb(255, rgb.R, rgb.G, rgb.B);
    }
}
```

**Step 3: Build and verify**

Run: `dotnet build src/HueWindows/HueWindows.csproj`
Expected: Build succeeded

**Step 4: Commit**

```bash
git add src/HueWindows/Views/LightDetailPage.xaml src/HueWindows/Views/LightDetailPage.xaml.cs
git commit -m "feat(light): replace icon color picker with SplitButton"
```

---

## Task 4: Remove Color Picker from RoomCard and LightCard Icons

**Files:**
- Modify: `src/HueWindows/Controls/RoomCard.xaml`
- Modify: `src/HueWindows/Controls/RoomCard.xaml.cs`
- Modify: `src/HueWindows/Controls/LightCard.xaml`
- Modify: `src/HueWindows/Controls/LightCard.xaml.cs`

**Step 1: Clean up RoomCard**

In `RoomCard.xaml`:
- Remove `Tapped="IconContainer_Tapped"` from the icon Border
- Remove the `FlyoutBase.AttachedFlyout` section

In `RoomCard.xaml.cs`:
- Remove `IconContainer_Tapped` method
- Remove `ColorFlyout_ColorChanged` method
- Remove any related fields

**Step 2: Clean up LightCard**

In `LightCard.xaml`:
- Remove `Tapped` handler from icon Border
- Remove the flyout attachment

In `LightCard.xaml.cs`:
- Remove icon tap handler
- Remove color flyout handler

**Step 3: Build and verify**

Run: `dotnet build src/HueWindows/HueWindows.csproj`
Expected: Build succeeded

**Step 4: Commit**

```bash
git add src/HueWindows/Controls/RoomCard.xaml src/HueWindows/Controls/RoomCard.xaml.cs
git add src/HueWindows/Controls/LightCard.xaml src/HueWindows/Controls/LightCard.xaml.cs
git commit -m "refactor: remove icon-tap color pickers from cards"
```

---

## Task 5: Style the SplitButton for Premium Look

**Files:**
- Modify: `src/HueWindows/Styles/AppStyles.xaml`

**Step 1: Add ColorSplitButtonStyle**

Add a new style for the color SplitButton:

```xaml
<!-- Color SplitButton Style -->
<Style x:Key="ColorSplitButtonStyle" TargetType="SplitButton">
    <Setter Property="Background" Value="Transparent"/>
    <Setter Property="BorderBrush" Value="#30FFFFFF"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="CornerRadius" Value="8"/>
    <Setter Property="Padding" Value="0"/>
    <Setter Property="MinHeight" Value="40"/>
    <Setter Property="MinWidth" Value="56"/>
</Style>
```

**Step 2: Apply style to SplitButtons**

Update both detail pages to use the style:
```xaml
Style="{StaticResource ColorSplitButtonStyle}"
```

**Step 3: Build and verify**

Run: `dotnet build src/HueWindows/HueWindows.csproj`
Expected: Build succeeded

**Step 4: Commit**

```bash
git add src/HueWindows/Styles/AppStyles.xaml
git add src/HueWindows/Views/RoomDetailPage.xaml src/HueWindows/Views/LightDetailPage.xaml
git commit -m "style: add premium ColorSplitButtonStyle"
```

---

## Task 6: Visual Testing and Final Polish

**Step 1: Run the app**

Run: `dotnet run --project src/HueWindows/HueWindows.csproj`

**Step 2: Test scenarios**

1. Navigate to a room with color-capable lights
   - Verify SplitButton appears next to toggle
   - Verify SplitButton background matches room color when on
   - Verify SplitButton is gray/transparent when room is off
   - Click SplitButton dropdown arrow - verify flyout opens
   - Pick a color - verify all lights change

2. Navigate to a light detail page
   - Verify SplitButton appears for color-capable lights
   - Verify SplitButton hidden for non-color lights
   - Verify color changes work

3. Verify icon no longer opens color picker when tapped

**Step 3: Capture screenshot**

Use MCP tool to capture screenshot for verification.

**Step 4: Final commit**

```bash
git add -A
git commit -m "feat: complete color picker SplitButton implementation"
```

---

## Summary

| Task | Description | Files Modified |
|------|-------------|----------------|
| 1 | Add SplitButton to RoomDetailPage | RoomDetailPage.xaml, .cs |
| 2 | Add SupportsColor to ViewModel | RoomDetailViewModel.cs |
| 3 | Add SplitButton to LightDetailPage | LightDetailPage.xaml, .cs |
| 4 | Remove icon-tap from cards | RoomCard, LightCard |
| 5 | Add SplitButton style | AppStyles.xaml |
| 6 | Visual testing | - |

**Total estimated tasks:** 6
**Files modified:** 10
