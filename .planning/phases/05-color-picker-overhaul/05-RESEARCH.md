# Phase 5: Color Picker Overhaul - Research

**Researched:** 2026-01-21
**Domain:** Custom color picker with gamut constraints, Win2D rendering, popup positioning
**Confidence:** HIGH

## Summary

This phase replaces the WinUI 3 built-in ColorPicker with a custom gamut-aware color picker. The current implementation uses `Microsoft.UI.Xaml.Controls.ColorPicker` which allows selecting colors that are impossible on Philips Hue bulbs. Additionally, there are bugs with color representation in the gradient bar and keyframe dragging that corrupt colors.

The research identified:
1. **Root cause of impossible colors:** WinUI ColorPicker operates in full RGB gamut while Hue bulbs use constrained CIE xy gamut triangles (A, B, C)
2. **Root cause of gradient bar mismatch:** Multiple different RGB-to-XY conversion implementations exist in the codebase
3. **Root cause of keyframe drag color corruption:** No evidence of corruption in current code - drag handler only updates `TimeSeconds`, not `Color`
4. **Solution for adaptive saturation:** Custom Win2D-rendered hue ring with per-angle saturation limits calculated from gamut triangle

**Primary recommendation:** Build a custom color picker control using Win2D CanvasControl with:
- Outer hue ring (full 360 degrees)
- Inner saturation area with adaptive limits per hue angle
- Single consistent color conversion path through existing HueColor class

## Current Implementation Analysis

### Files Involved

| File | Purpose | Issues |
|------|---------|--------|
| `Controls/ColorPickerFlyout.xaml` | Reusable color picker wrapper | Uses built-in ColorPicker |
| `Controls/ColorPickerFlyout.xaml.cs` | Debounced color events | No gamut awareness |
| `Views/SceneBuilderPage.xaml` | Timeline with inline ColorPicker | ColorPicker allows impossible colors |
| `Views/SceneBuilderPage.xaml.cs` | Color changed handler (lines 1140-1174) | Inline RGB-to-XY conversion duplicates HueColor.FromRgb() |
| `Views/Rendering/GradientTrackRenderer.cs` | Gradient bar between keyframes | Different XY-to-RGB conversion (lines 312-333) |
| `Views/Rendering/KeyframeLayerRenderer.cs` | Keyframe circle colors | Same different XY-to-RGB (lines 151-172) |
| `Core/Models/HueColor.cs` | Canonical color conversion | Uses sRGB D65 matrix |
| `Core/Models/ColorConverter.cs` | HSV interpolation | Uses slightly different matrix values |
| `Views/LightDetailPage.xaml` | Light detail color picker | Same built-in ColorPicker |

### Color Conversion Inconsistency Analysis

The codebase has **three different XY-to-RGB conversion implementations:**

**1. HueColor.ToRgb() (Core/Models/HueColor.cs:54-99)**
```csharp
// Uses sRGB matrix (correct)
var r = X_xyz * 3.2404542 - Y_xyz * 1.5371385 - Z_xyz * 0.4985314;
var g = -X_xyz * 0.9692660 + Y_xyz * 1.8760108 + Z_xyz * 0.0415560;
var b = X_xyz * 0.0556434 - Y_xyz * 0.2040259 + Z_xyz * 1.0572252;
```

**2. GradientTrackRenderer.HueColorToRgb() (Views/Rendering/GradientTrackRenderer.cs:312-333)**
```csharp
// Uses Wide Gamut RGB matrix (DIFFERENT)
var r = X * 1.656492 - Y * 0.354851 - Z * 0.255038;
var g = -X * 0.707196 + Y * 1.655397 + Z * 0.036152;
var b = X * 0.051713 - Y * 0.121364 + Z * 1.011530;
```

**3. KeyframeLayerRenderer.HueColorToRgb() (Views/Rendering/KeyframeLayerRenderer.cs:151-172)**
```csharp
// Same Wide Gamut matrix as GradientTrackRenderer (DIFFERENT from HueColor)
var r = X * 1.656492 - Y * 0.354851 - Z * 0.255038;
```

**Impact:** The same HueColor will display differently:
- In the ColorPicker (uses its own conversion)
- On the gradient bar (Wide Gamut matrix)
- On the keyframe circles (Wide Gamut matrix)
- On the actual Hue light (HueColor.ToRgb via bridge)

**Solution:** Consolidate all conversions to use `HueColor.ToRgb()` consistently.

### Keyframe Drag Investigation

Investigated `TimelineCanvas_KeyframeDrag` (lines 586-620):

```csharp
private void TimelineCanvas_KeyframeDrag(object sender, PointerRoutedEventArgs e)
{
    if (!_isDraggingKeyframe || _draggingKeyframe == null || _draggingTrack == null)
        return;

    var point = e.GetCurrentPoint(TimelineCanvas);
    var timeSeconds = (point.Position.X - GradientTrackRenderer.LeftMargin) / ViewModel.ZoomLevel;

    // ... snap logic ...

    _draggingKeyframe.TimeSeconds = timeSeconds;  // <-- ONLY updates TimeSeconds

    // Re-sort keyframes in track
    var sorted = _draggingTrack.Keyframes.OrderBy(k => k.TimeSeconds).ToList();
    // ...
}
```

**Finding:** The drag handler only updates `TimeSeconds`, never `Color`. The `Color` property is not touched during drag operations.

**Possible sources of perceived corruption:**
1. Color picker auto-selecting a new color when panel re-opens
2. Interpolation when creating new keyframes inherits from neighbors
3. Visual mismatch from inconsistent conversions (see above)

**Recommendation:** COLOR-02 may already be fixed if consistent conversions are implemented. Add test case to verify.

### SidePanel Positioning

The SidePanel (keyframe editor) is currently a `<Border>` positioned with:
```xaml
<Border x:Name="SidePanel"
        Width="280"
        MaxHeight="500"
        Margin="0,0,16,16"
        HorizontalAlignment="Right"
        VerticalAlignment="Bottom"
```

This is positioned within the Grid Row 2 (Timeline Area), so it cannot extend beyond the timeline card bounds.

**Options for CPICK-05:**
1. Use `Popup` with `ShouldConstrainToRootBounds="False"` (has WinUI 3 bugs)
2. Move SidePanel to root Grid layer with manual positioning
3. Keep current position but ensure it's large enough for smaller picker

## Standard Stack

### Core (No new dependencies needed)
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Win2D | 1.0.5+ (existing) | GPU-accelerated 2D rendering | Already used for timeline |
| Microsoft.UI.Xaml | 1.5+ (existing) | XAML framework | Standard WinUI 3 |

### Supporting (Already in project)
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| CommunityToolkit.Mvvm | 8.x | MVVM pattern | ViewModel binding |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Custom Win2D picker | Syncfusion ColorPicker | Third-party dependency, not gamut-aware |
| Win2D rendering | XAML shapes | XAML would be slower, less control |
| Popup for overflow | Flyout | Flyout has same constraint issues |

## Architecture Patterns

### Recommended Control Structure
```
Controls/
├── GamutColorPicker.xaml          # Custom control XAML
├── GamutColorPicker.xaml.cs       # Code-behind with rendering
└── ColorPickerFlyout.xaml         # (modified to use GamutColorPicker)

Core/Models/
├── HueColor.cs                    # (existing) Add gamut methods
├── ColorGamut.cs                  # NEW: Gamut definitions and algorithms
└── HueColors.cs                   # (existing)
```

### Pattern 1: Custom UserControl with Win2D Canvas

**What:** UserControl containing CanvasControl for rendering hue ring and saturation area
**When to use:** Complex custom graphics that need GPU acceleration

```csharp
public sealed partial class GamutColorPicker : UserControl
{
    // Gamut triangle for the current light
    public ColorGamut Gamut { get; set; } = ColorGamut.GamutC;

    // Selected color in xy space
    public HueColor SelectedColor { get; set; }

    // Cached saturation limits per hue angle (0-359)
    private float[] _maxSaturationPerHue = new float[360];

    private void Canvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        var ds = args.DrawingSession;
        DrawHueRing(ds);
        DrawSaturationArea(ds);
        DrawSelectionIndicator(ds);
    }
}
```

### Pattern 2: Gamut-Aware Saturation Calculation

**What:** Pre-calculate max achievable saturation for each hue angle
**When to use:** Startup or when gamut changes

```csharp
// Algorithm from HUE-COLORS.md research
private void CalculateMaxSaturationPerHue(ColorGamut gamut)
{
    var center = (0.3127, 0.3290); // D65 white point

    for (int hue = 0; hue < 360; hue++)
    {
        // Convert hue to xy direction
        var (xy, _) = ColorConverter.HsvToXy(hue, 1.0, 1.0);

        // Ray from center toward full saturation
        var dx = xy.X - center.X;
        var dy = xy.Y - center.Y;

        // Find intersection with gamut triangle edges
        var maxT = FindGamutEdgeIntersection(center, dx, dy, gamut);

        _maxSaturationPerHue[hue] = Math.Min(1.0f, (float)maxT);
    }
}
```

### Pattern 3: Shader-Based Color Wheel Rendering

**What:** Use GPU shader for efficient hue/saturation rendering
**When to use:** Real-time preview during drag

```csharp
// Per-pixel calculation in draw method
// Reference: https://medium.com/@yarolegovich/color-wheel-efficient-drawing-with-shaders
private void DrawHueSaturationWheel(CanvasDrawingSession ds, float cx, float cy, float radius)
{
    // For each pixel in the wheel area:
    // 1. Calculate angle from center (= hue)
    // 2. Calculate distance from center (= saturation)
    // 3. Clamp saturation to _maxSaturationPerHue[angle]
    // 4. Convert HSV to RGB for display
    // 5. Gray out pixels beyond max saturation
}
```

### Anti-Patterns to Avoid
- **Multiple RGB/XY conversion implementations:** Creates visual inconsistency
- **Per-frame gamut calculation:** Expensive - pre-calculate on load
- **Full ColorPicker replacement in one PR:** Break into smaller increments
- **Ignoring brightness dimension:** Brightness affects achievable saturation

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| RGB/HSV/XY conversion | Custom math | `HueColor` + `ColorConverter` classes | Already tested, handles edge cases |
| Gamut triangle math | Custom geometry | Add methods to new `ColorGamut` class | Centralizes gamut logic |
| Circular gradient | Pixel-by-pixel loop | Win2D `RadialGradient` + overlay | GPU accelerated |
| Point-in-triangle | Custom algorithm | Use algorithm from HUE-COLORS.md | Well-tested barycentric approach |

**Key insight:** The existing HUE-COLORS.md research already contains all the algorithms needed. The task is implementation and integration, not algorithm design.

## Common Pitfalls

### Pitfall 1: Color Space Confusion
**What goes wrong:** Mixing up RGB, HSV, HSL, XY, and CIE coordinates
**Why it happens:** Each step uses different color space
**How to avoid:** Document color space at each conversion step, use explicit type names
**Warning signs:** Colors look different in picker vs. on light

### Pitfall 2: Inconsistent Brightness Handling
**What goes wrong:** Saturation limits calculated at brightness=1.0 but user selects lower brightness
**Why it happens:** CIE xy is chromaticity only, brightness affects achievable colors
**How to avoid:** Either fix brightness during picker interaction or recalculate limits
**Warning signs:** Selected colors clip unexpectedly at lower brightness

### Pitfall 3: WinUI 3 Popup Bugs
**What goes wrong:** `ShouldConstrainToRootBounds="False"` doesn't work (Issue #5958)
**Why it happens:** Known WinUI 3 bug, `IsConstrainedToRootBounds` always returns true
**How to avoid:** Don't rely on Popup extending beyond window; use overlay positioning instead
**Warning signs:** Popup clips at window edge despite setting

### Pitfall 4: Win2D Resource Lifecycle
**What goes wrong:** Memory leak or crash from undisposed Win2D resources
**Why it happens:** CanvasControl resources need explicit disposal
**How to avoid:** Follow existing pattern in SceneBuilderPage: `RemoveFromVisualTree()` on unload
**Warning signs:** Growing memory usage, crash on page navigation

### Pitfall 5: Event Handler Stacking
**What goes wrong:** ColorChanged events fire during programmatic updates
**Why it happens:** Setting color to update picker triggers handler
**How to avoid:** Use flag pattern (already in ColorPickerFlyout: `_isUpdatingColor`)
**Warning signs:** Infinite loops, flickering, performance degradation

## Code Examples

### Example 1: Consolidated XY-to-RGB (Fix COLOR-01)
```csharp
// In GradientTrackRenderer.cs - replace HueColorToRgb
private Color HueColorToWindowsColor(HueColor color, double brightness = 1.0)
{
    // Use canonical conversion from HueColor
    var (r, g, b) = color.ToRgb(brightness);
    return Color.FromArgb(255, r, g, b);
}
```

### Example 2: ColorGamut Class (From HUE-COLORS.md)
```csharp
// New file: Core/Models/ColorGamut.cs
public class ColorGamut
{
    public (double X, double Y) Red { get; }
    public (double X, double Y) Green { get; }
    public (double X, double Y) Blue { get; }

    public static readonly ColorGamut GamutA = new(
        (0.704, 0.296), (0.2151, 0.7106), (0.138, 0.08));
    public static readonly ColorGamut GamutB = new(
        (0.675, 0.322), (0.4091, 0.518), (0.167, 0.04));
    public static readonly ColorGamut GamutC = new(
        (0.6915, 0.3083), (0.17, 0.7), (0.1532, 0.0475));

    public bool Contains(double x, double y) { /* point-in-triangle */ }
    public (double X, double Y) ClipToGamut(double x, double y) { /* nearest point */ }
    public double GetMaxSaturationForHue(double hue) { /* ray intersection */ }
}
```

### Example 3: Win2D Hue Ring Drawing
```csharp
// Reference: Win2D HueToRgbEffect documentation
private void DrawHueRing(CanvasDrawingSession ds, float cx, float cy, float outerR, float innerR)
{
    // Draw segmented ring (36 segments for smooth appearance)
    const int segments = 36;
    for (int i = 0; i < segments; i++)
    {
        float startAngle = i * (360f / segments);
        float endAngle = (i + 1) * (360f / segments);

        // Create arc geometry for this segment
        using var path = new CanvasPathBuilder(ds);
        // ... arc drawing code ...

        // Fill with hue color at full saturation
        var hueColor = HsvToColor(startAngle, 1.0f, 1.0f);
        ds.FillGeometry(geometry, hueColor);
    }
}
```

### Example 4: SidePanel Repositioning (Fix CPICK-05)
```csharp
// Option: Move SidePanel to root Grid layer
// In SceneBuilderPage.xaml, move SidePanel outside Grid.Row="2"
// Position manually relative to selected keyframe

private void UpdateSidePanelPosition()
{
    if (ViewModel.SelectedKeyframe == null) return;

    // Calculate position based on keyframe location
    var keyframeX = GradientTrackRenderer.LeftMargin +
        ViewModel.SelectedKeyframe.TimeSeconds * ViewModel.ZoomLevel;
    var keyframeY = GetTrackYPosition(ViewModel.SelectedKeyframe);

    // Position panel to right of keyframe, or left if near right edge
    var panelX = keyframeX + 20;
    if (panelX + SidePanel.ActualWidth > ActualWidth - 20)
        panelX = keyframeX - SidePanel.ActualWidth - 20;

    Canvas.SetLeft(SidePanel, panelX);
    Canvas.SetTop(SidePanel, Math.Max(0, keyframeY - SidePanel.ActualHeight / 2));
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| WinUI ColorPicker ring | Custom gamut-constrained picker | This phase | Every selection achievable |
| Multiple XY-to-RGB paths | Single canonical conversion | This phase | Consistent color display |
| Fixed SidePanel position | Dynamic positioning | This phase | Picker visible anywhere |

**Deprecated/outdated:**
- Raw RGB input: Removed per CPICK-03 (users can't reason about RGB in Hue context)
- Built-in ColorPicker for Hue: Allows impossible colors

## Open Questions

1. **Per-light gamut vs. fixed gamut?**
   - What we know: Hue API returns gamut type per light
   - What's unclear: Should picker adapt per-light or use GamutC (most restrictive)?
   - Recommendation: Default to GamutC (covers all modern bulbs), future enhancement for per-light

2. **Brightness integration?**
   - What we know: At low brightness, fewer colors are achievable
   - What's unclear: Should picker show reduced saturation at low brightness?
   - Recommendation: V1 ignores brightness in saturation limits; add later if needed

3. **COLOR-02 root cause?**
   - What we know: Drag handler doesn't modify color
   - What's unclear: Is this a real bug or visual artifact from inconsistent conversions?
   - Recommendation: Fix COLOR-01 first, then verify COLOR-02 with test case

## Sources

### Primary (HIGH confidence)
- `.planning/research/HUE-COLORS.md` - Gamut algorithms, conversion formulas, gamut coordinates
- `src/HueCompanion.Core/Models/HueColor.cs` - Canonical color conversion implementation
- `src/HueCompanion/Views/SceneBuilderPage.xaml.cs` - Current color picker integration
- `src/HueCompanion/Views/Rendering/GradientTrackRenderer.cs` - Current gradient rendering

### Secondary (MEDIUM confidence)
- [Win2D HueToRgbEffect](https://microsoft.github.io/Win2D/WinUI3/html/T_Microsoft_Graphics_Canvas_Effects_HueToRgbEffect.htm) - GPU HSV conversion
- [WinUI ColorPicker API](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.controls.colorpicker) - Built-in control limitations
- [Color Wheel Shader Article](https://medium.com/@yarolegovich/color-wheel-efficient-drawing-with-shaders) - Rendering algorithm

### Tertiary (LOW confidence)
- [WinUI Popup Issue #5958](https://github.com/microsoft/microsoft-ui-xaml/issues/5958) - `ShouldConstrainToRootBounds` bug
- [WinUI Popup Issue #7933](https://github.com/microsoft/microsoft-ui-xaml/issues/7933) - Popup exceeding window bounds

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - No new dependencies needed, Win2D already in use
- Architecture: HIGH - Follows existing patterns in codebase
- Pitfalls: HIGH - Verified with codebase analysis and official documentation

**Research date:** 2026-01-21
**Valid until:** 2026-03-21 (60 days - stable domain, algorithms well-established)
