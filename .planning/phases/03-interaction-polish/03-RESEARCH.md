# Phase 3: Interaction Polish - Research

**Researched:** 2026-01-21
**Domain:** Win2D hover effects, WinUI 3 cursor management, accessibility hit targets
**Confidence:** MEDIUM

## Summary

Phase 3 adds hover states, cursor management, and accessibility-compliant touch targets to the Win2D timeline. The research covers Win2D glow effects using ShadowEffect and GaussianBlurEffect, WinUI 3 cursor APIs (CoreCursor with ProtectedCursor), WCAG 2.5.5 target size requirements (44x44px for AAA), and hover state tracking with CanvasControl pointer events.

The standard approach is to render hover/selection glows using Win2D's ShadowEffect with color-matched glow, track hover state via PointerMoved with hit testing, change cursors using CoreCursor.Hand/Arrow via ProtectedCursor subclass, and maintain 44px minimum hit areas while rendering smaller visuals.

**Primary recommendation:** Use Win2D ShadowEffect for color-matched keyframe glows, track hover state via continuous hit testing in PointerMoved, subclass CanvasControl to expose ProtectedCursor for dynamic cursor changes, and ensure 44px playhead hit area via HitTestHelper while rendering 2-4px visual line.

## Standard Stack

The established libraries/tools for this domain:

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Win2D | 1.27.0+ | GPU-accelerated 2D rendering | Built-in blur/shadow effects for glows, DrawImage compositing |
| WinUI 3 | 1.6+ | UI framework | ProtectedCursor API, pointer events (PointerMoved, PointerEntered) |
| Windows.UI.Core | Built-in | Cursor management | CoreCursor/CoreCursorType enum for system cursors |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| GaussianBlurEffect | Win2D | Gaussian blur with configurable BlurAmount | Alternative to ShadowEffect for custom glow compositing |
| CompositeEffect | Win2D | Multi-layer image compositing | When combining multiple effects (blur + original image for glow) |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| ShadowEffect | GaussianBlurEffect + CompositeEffect | More control but more complex (manually composite blur over original) |
| PointerMoved tracking | PointerEntered/Exited | Cleaner API but misses elements (events don't fire reliably on Win2D canvas) |
| CoreCursor | Custom .cur file via InputDesktopResourceCursor | Full control but requires asset files and interop |

**Installation:**
Win2D and WinUI 3 are already installed in the codebase. No additional packages required.

## Architecture Patterns

### Recommended Hover State Management
```
SceneBuilderPage.xaml.cs:
  PointerMoved handler →
    1. Hit test at current position (HitTestHelper)
    2. Compare with last hover state
    3. If different:
       a. Update hover tracking fields
       b. Update cursor via SetProtectedCursor()
       c. Invalidate() to trigger redraw

  TimelineCanvas_Draw →
    KeyframeLayerRenderer.Draw(hoveredKeyframe) →
      Render normal keyframes
      Render hovered/selected keyframes with glow effect
```

### Pattern 1: Win2D Glow Effect Using ShadowEffect
**What:** Render a colored glow around keyframe circles by drawing a blurred shadow in the same color as the keyframe, then drawing the keyframe on top.
**When to use:** Hover states, selection indicators, any "highlight" effect needing soft glow.
**Example:**
```csharp
// Source: Win2D official docs (microsoft.github.io/Win2D)
using Microsoft.Graphics.Canvas.Effects;

// In KeyframeLayerRenderer.Draw() for hovered/selected keyframes:
void DrawKeyframeWithGlow(CanvasDrawingSession ds, Vector2 position, float radius, Color keyframeColor, float glowRadius)
{
    // Create a CommandList to hold the keyframe circle (needed as Source for ShadowEffect)
    using var cl = new CanvasCommandList(ds);
    using (var clDs = cl.CreateDrawingSession())
    {
        clDs.FillCircle(position, radius, keyframeColor);
    }

    // Create glow effect using ShadowEffect with color matching keyframe
    var glowEffect = new ShadowEffect
    {
        Source = cl,
        BlurAmount = glowRadius,  // 8-12 for subtle glow, 15-20 for strong glow
        ShadowColor = keyframeColor  // Match keyframe color for cohesive look
    };

    // Draw glow layer
    ds.DrawImage(glowEffect, position);

    // Draw original keyframe on top (for crisp edge)
    ds.FillCircle(position, radius, keyframeColor);
}
```

### Pattern 2: Dynamic Cursor Management via ProtectedCursor Subclass
**What:** WinUI 3's ProtectedCursor property is protected, requiring a subclass to expose it. Create a custom CanvasControl subclass that allows setting cursors.
**When to use:** Any time you need to change cursors dynamically (hover states, drag operations).
**Example:**
```csharp
// Source: microsoft-ui-xaml-specs ElementCursor.md
using Windows.UI.Core;

// Create subclass exposing cursor property
public class TimelineCanvasControl : CanvasControl
{
    public void SetCursor(CoreCursor cursor)
    {
        this.ProtectedCursor = cursor;
    }
}

// Usage in SceneBuilderPage.xaml.cs:
private void UpdateCursorForHoverState(HitTestResult hitResult)
{
    CoreCursor newCursor = hitResult.Type switch
    {
        HitType.Keyframe => new CoreCursor(CoreCursorType.Hand, 0),
        HitType.Playhead => new CoreCursor(CoreCursorType.Hand, 0),
        _ => new CoreCursor(CoreCursorType.Arrow, 0)
    };

    if (TimelineCanvas is TimelineCanvasControl tcc)
    {
        tcc.SetCursor(newCursor);
    }
}

// During drag operations:
void OnPointerPressed()
{
    // Change to "grabbing" cursor (SizeAll is closest to closed fist)
    timelineCanvas.SetCursor(new CoreCursor(CoreCursorType.SizeAll, 0));
}
```

### Pattern 3: Hover State Tracking via PointerMoved
**What:** Continuously hit test during PointerMoved to track which element is hovered, invalidating only when hover state changes.
**When to use:** Win2D canvas elements where PointerEntered/Exited don't work reliably.
**Example:**
```csharp
// Current codebase pattern (SceneBuilderPage.xaml.cs)
private KeyframeViewModel? _hoveredKeyframe;
private bool _isPlayheadHovered;

private void TimelineCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
{
    if (_isDraggingKeyframe || _isDraggingPlayhead)
        return; // Don't update hover during drag

    var point = e.GetCurrentPoint(TimelineCanvas);
    var position = new Vector2((float)point.Position.X, (float)point.Position.Y);

    // Hit test at current position
    var hitResult = HitTestHelper.HitTest(
        TimelineCanvas,
        position,
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
        UpdateCursorForHoverState(hitResult);
        TimelineCanvas.Invalidate(); // Trigger redraw with new hover state
    }
}
```

### Pattern 4: WCAG-Compliant Hit Testing with Invisible Padding
**What:** Hit test uses larger hit area (44x44px) while rendering smaller visual (2-4px line, 8px circle).
**When to use:** Meeting WCAG 2.5.5 target size requirements without oversized visuals.
**Example:**
```csharp
// Current implementation in HitTestHelper.cs shows this pattern:
private const float PlayheadHitWidth = 12f; // Wide hit area (needs expansion to 44px)

private static bool HitTestPlayhead(Vector2 point, float playheadX, float height)
{
    // Rectangular hit test with expanded width (12px currently, should be 44px)
    var hitLeft = playheadX - PlayheadHitWidth / 2;
    var hitRight = playheadX + PlayheadHitWidth / 2;
    return point.X >= hitLeft && point.X <= hitRight && point.Y >= 0 && point.Y <= height;
}

// Expand to meet WCAG 2.5.5:
private const float PlayheadHitWidth = 44f; // WCAG AAA compliant
private const float PlayheadHitHeight = 44f; // Expanded vertical hit area at top

private static bool HitTestPlayhead(Vector2 point, float playheadX, float height)
{
    // Check triangular handle hit area first (top 44px height)
    if (point.Y <= PlayheadHitHeight)
    {
        var hitLeft = playheadX - PlayheadHitWidth / 2;
        var hitRight = playheadX + PlayheadHitWidth / 2;
        if (point.X >= hitLeft && point.X <= hitRight)
            return true;
    }

    // Fallback to narrower line hit test for rest of height
    var hitLeft = playheadX - 6f; // 12px wide hit area for line
    var hitRight = playheadX + 6f;
    return point.X >= hitLeft && point.X <= hitRight && point.Y >= 0 && point.Y <= height;
}
```

### Anti-Patterns to Avoid
- **Creating effects every frame:** ShadowEffect should be created once per draw call, not cached globally (causes device loss issues)
- **Using PointerEntered/Exited on Win2D canvas:** These events don't fire reliably for drawn elements, use PointerMoved with hit testing instead
- **Invalidating on every PointerMoved:** Only invalidate when hover state actually changes (huge performance impact)
- **Accessing ProtectedCursor directly:** Property is protected, must use subclass approach

## Don't Hand-Roll

Problems that look simple but have existing solutions:

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Glow/blur effects | Manual pixel manipulation, multiple DrawCircle calls with alpha fade | Win2D ShadowEffect or GaussianBlurEffect | GPU-accelerated, handles edge cases, automatic alpha channel handling |
| Cursor shapes | Loading .cur files, P/Invoke to Win32 SetCursor | CoreCursor with CoreCursorType enum | Built-in system cursors, DPI-aware, maintains OS cursor theme |
| Hit area expansion | Multiple overlapping invisible XAML elements | Single geometry hit test with expanded bounds | Avoids XAML layout overhead, works with Win2D coordinate space |
| Hover state diffing | Manual comparison of all properties | Reference equality check on ViewModels | ViewModels are reference types, identity check is sufficient |

**Key insight:** Win2D effects are composable GPU operations with automatic resource management. Hand-rolling blur/glow with CPU operations or DrawCircle loops will be 10-100x slower and won't handle edge cases (clipping, alpha blending, DPI scaling).

## Common Pitfalls

### Pitfall 1: Glow Effect Performance Degradation
**What goes wrong:** Creating ShadowEffect or GaussianBlurEffect instances in a tight loop (e.g., for every keyframe on every frame) causes stuttering and memory pressure.
**Why it happens:** Each effect creation allocates GPU resources. Win2D's IDisposable pattern requires explicit cleanup, and high allocation rate overwhelms garbage collector.
**How to avoid:**
- Create effect instances once per draw call using `using` statements for automatic disposal
- Don't cache effects globally (causes issues on device loss/DPI change)
- Render normal keyframes first, then hovered/selected ones with effects (reduces effect instances)
**Warning signs:** Frame rate drops below 30fps when hovering over keyframes, memory usage climbing steadily.

### Pitfall 2: PointerEntered/Exited Not Firing on CanvasControl
**What goes wrong:** Subscribing to PointerEntered/Exited events on CanvasControl expecting them to fire when mouse moves over drawn shapes. Events never fire or fire only for canvas bounds.
**Why it happens:** PointerEntered/Exited are XAML visual tree events. Drawn content via Win2D is not part of visual tree, so events don't trigger for individual drawn elements.
**How to avoid:**
- Use PointerMoved event with continuous hit testing instead
- Cache last hover state to avoid unnecessary invalidations
- Only invalidate when hover state changes (not on every PointerMoved)
**Warning signs:** Hover effects never appear, cursor doesn't change over keyframes, event handlers have breakpoints that never hit.

### Pitfall 3: ProtectedCursor Access Violation
**What goes wrong:** Attempting to set `element.ProtectedCursor` directly causes compile error "ProtectedCursor is inaccessible due to its protection level."
**Why it happens:** ProtectedCursor is a protected property, only accessible within derived classes. This design is intentional for control authors vs. app developers.
**How to avoid:**
- Create custom CanvasControl subclass with public SetCursor method
- Call SetCursor from event handlers, not property assignment
- Use CoreCursor constructor with CoreCursorType enum values
**Warning signs:** Compile errors, attempts to use reflection/interop to work around protection level.

### Pitfall 4: WCAG Hit Target Misunderstanding
**What goes wrong:** Making visual elements 44x44px to meet WCAG, creating oversized playhead/keyframes that look wrong.
**Why it happens:** Misunderstanding that WCAG 2.5.5 refers to interactive hit area, not visual size. Hit area can be larger than visual representation.
**How to avoid:**
- Hit testing uses expanded bounds (44px)
- Rendering uses aesthetic size (2-4px line, 8-10px circle)
- Document why hit test bounds differ from visual bounds (accessibility requirement)
**Warning signs:** Designer feedback that playhead is "too thick," hit testing feels imprecise (clicking far from visual hits element).

### Pitfall 5: Time Ruler Misalignment After Resize
**What goes wrong:** After window resize, time ruler tick marks don't align with keyframe positions, creating visual mismatch.
**Why it happens:** TimeRulerCanvas and TimelineCanvas are separate CanvasControl instances. If their Width properties aren't synchronized, zoom level calculations produce different pixel positions.
**How to avoid:**
- Ensure both canvases have same Width (currently both set to `ViewModel.DurationSeconds * ViewModel.ZoomLevel`)
- Apply LeftMargin consistently in both TimeRulerRenderer and other renderers (currently GradientTrackRenderer.LeftMargin = 14px)
- Trigger both Invalidate() calls together when zoom/duration changes
**Warning signs:** Ruler shows "5s" at different X position than keyframe at 5 seconds, misalignment worsens with zoom.

## Code Examples

Verified patterns from official sources and current codebase:

### Win2D Glow Effect for Keyframe Hover
```csharp
// Source: microsoft.github.io/Win2D ShadowEffect documentation
// Modified for keyframe glow use case

public void DrawKeyframeWithGlow(
    CanvasDrawingSession ds,
    Vector2 position,
    float radius,
    Color keyframeColor,
    bool isHovered,
    bool isSelected)
{
    // Determine glow parameters
    var shouldGlow = isHovered || isSelected;
    var glowRadius = isSelected ? 12f : 8f; // Selected glow is slightly larger
    var glowOpacity = isSelected ? 0.8f : 0.6f; // Selected glow is more opaque

    if (shouldGlow)
    {
        // Create CommandList with keyframe shape (ShadowEffect needs ICanvasImage source)
        using var commandList = new CanvasCommandList(ds);
        using (var clDs = commandList.CreateDrawingSession())
        {
            clDs.FillCircle(position, radius, keyframeColor);
        }

        // Apply glow effect matching keyframe color
        var glowColor = Color.FromArgb(
            (byte)(255 * glowOpacity),
            keyframeColor.R,
            keyframeColor.G,
            keyframeColor.B);

        using var glowEffect = new ShadowEffect
        {
            Source = commandList,
            BlurAmount = glowRadius,
            ShadowColor = glowColor
        };

        // Draw glow (slightly offset for depth, or centered for selection)
        var glowOffset = isSelected ? Vector2.Zero : new Vector2(0, 1);
        ds.DrawImage(glowEffect, position + glowOffset);
    }

    // Draw keyframe on top (crisp edge, full color)
    ds.FillCircle(position, radius, keyframeColor);
}
```

### Hover State Tracking with Cursor Management
```csharp
// Source: Current codebase HitTestHelper.cs + microsoft-ui-xaml-specs ElementCursor.md

// In SceneBuilderPage.xaml.cs:
private KeyframeViewModel? _hoveredKeyframe;
private bool _isPlayheadHovered;
private HitType _lastHitType = HitType.None;

protected override void OnNavigatedTo(NavigationEventArgs e)
{
    base.OnNavigatedTo(e);

    // Subscribe to pointer events
    TimelineCanvas.PointerMoved += TimelineCanvas_PointerMoved;
    TimelineCanvas.PointerExited += TimelineCanvas_PointerExited;
}

private void TimelineCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
{
    // Skip during drag operations
    if (_isDraggingKeyframe || _isDraggingPlayhead)
        return;

    var point = e.GetCurrentPoint(TimelineCanvas);
    var position = new Vector2((float)point.Position.X, (float)point.Position.Y);

    // Calculate current playhead position
    var playheadX = GradientTrackRenderer.LeftMargin +
                    (float)(ViewModel.PlayheadPosition * ViewModel.ZoomLevel);
    var canvasHeight = TimelineCanvas.Height;

    // Hit test at pointer position
    var hitResult = HitTestHelper.HitTest(
        TimelineCanvas,
        position,
        playheadX,
        (float)canvasHeight,
        ViewModel.Tracks.ToList(),
        ViewModel.EventTracks.ToList(),
        ViewModel.SelectedKeyframes,
        (float)ViewModel.ZoomLevel);

    // Check if hover state changed
    var hoveredKeyframe = hitResult.Type == HitType.Keyframe ? hitResult.Keyframe : null;
    var playheadHovered = hitResult.Type == HitType.Playhead;

    var stateChanged = _hoveredKeyframe != hoveredKeyframe ||
                       _isPlayheadHovered != playheadHovered;

    if (stateChanged)
    {
        _hoveredKeyframe = hoveredKeyframe;
        _isPlayheadHovered = playheadHovered;
        _lastHitType = hitResult.Type;

        // Update cursor for new hover state
        UpdateCursor(hitResult.Type);

        // Invalidate to render hover effects
        TimelineCanvas.Invalidate();
    }
}

private void TimelineCanvas_PointerExited(object sender, PointerRoutedEventArgs e)
{
    // Clear hover state when pointer leaves canvas
    if (_hoveredKeyframe != null || _isPlayheadHovered)
    {
        _hoveredKeyframe = null;
        _isPlayheadHovered = false;
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

    // Assumes TimelineCanvas is custom subclass exposing SetCursor
    if (TimelineCanvas is TimelineCanvasControl tcc)
    {
        tcc.SetCursor(cursor);
    }
}

// During drag operations, change to "grabbing" cursor
private void StartPlayheadDrag(PointerRoutedEventArgs e)
{
    _isDraggingPlayhead = true;
    TimelineCanvas.CapturePointer(e.Pointer);

    // Change to grabbing cursor (SizeAll is closest to closed fist)
    UpdateCursor(HitType.None);
    if (TimelineCanvas is TimelineCanvasControl tcc)
    {
        tcc.SetCursor(new CoreCursor(CoreCursorType.SizeAll, 0));
    }

    TimelineCanvas.PointerMoved += TimelineCanvas_PlayheadDrag;
    TimelineCanvas.PointerReleased += TimelineCanvas_PlayheadDragEnd;
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
```

### Playhead with Hover Thickness Change
```csharp
// Source: Current PlayheadRenderer.cs, modified for hover state
// In PlayheadRenderer.cs:

public void Draw(
    CanvasDrawingSession ds,
    float playheadX,
    float height,
    bool isHovered,
    bool isDragging)
{
    var playheadColor = Color.FromArgb(255, 255, 100, 100); // Red

    // Determine line thickness based on hover/drag state
    var lineThickness = (isHovered || isDragging) ? 4.0f : 2.0f;

    // Draw vertical playhead line
    ds.DrawLine(
        new Vector2(playheadX, 0),
        new Vector2(playheadX, height),
        playheadColor,
        lineThickness
    );

    // Draw triangle handle at top (stays same size regardless of hover)
    EnsureTriangleGeometry(ds);
    if (_triangleGeometry != null)
    {
        var transform = Matrix3x2.CreateTranslation(playheadX, 0);
        ds.Transform = transform;
        ds.FillGeometry(_triangleGeometry, playheadColor);
        ds.Transform = Matrix3x2.Identity;
    }
}
```

### WCAG-Compliant Playhead Hit Testing
```csharp
// Source: W3C WCAG 2.5.5 + current HitTestHelper.cs
// In HitTestHelper.cs:

private const float PlayheadHitWidth = 44f; // WCAG 2.5.5 AAA minimum
private const float PlayheadTriangleHitHeight = 44f; // Expanded hit area for triangle handle

private static bool HitTestPlayhead(Vector2 point, float playheadX, float height)
{
    // Top area: 44x44px hit zone for triangle handle (WCAG compliant)
    if (point.Y <= PlayheadTriangleHitHeight)
    {
        var hitLeft = playheadX - PlayheadHitWidth / 2;
        var hitRight = playheadX + PlayheadHitWidth / 2;

        if (point.X >= hitLeft && point.X <= hitRight)
            return true;
    }

    // Rest of line: narrower hit area (12px wide) for visual accuracy
    var lineHitWidth = 12f;
    var hitLeft = playheadX - lineHitWidth / 2;
    var hitRight = playheadX + lineHitWidth / 2;

    return point.X >= hitLeft && point.X <= hitRight &&
           point.Y >= 0 && point.Y <= height;
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| XAML Shapes with pointer events | Win2D rendering with manual hit testing | Phase 1 (Win2D migration) | Hover states require custom tracking vs. automatic XAML |
| Brightness/scale hover effects | Glow effects using GPU blur | Industry standard (DAWs use glow) | More polished, matches professional tools |
| 24x24px touch targets | 44x44px touch targets | WCAG 2.1 (2018) | Better accessibility, especially for motor impairments |
| PointerEntered/Exited | PointerMoved with hit testing | Win2D adoption | More reliable for canvas-drawn elements |

**Deprecated/outdated:**
- **PointerEntered/Exited on Win2D canvas:** Not reliable, use PointerMoved with hit testing instead
- **Manual pixel manipulation for glow:** Use Win2D ShadowEffect/GaussianBlurEffect (GPU-accelerated)
- **24x24px minimum touch target:** WCAG 2.5.5 AAA requires 44x44px (Level AA is 24px via WCAG 2.5.8, but best practice is 44px)

## Open Questions

Things that couldn't be fully resolved:

1. **Grabbing (Closed Fist) Cursor Availability**
   - What we know: CoreCursorType enum includes Hand (open hand), SizeAll (cross arrows), but no explicit "grabbing" type
   - What's unclear: Whether CoreCursorType.SizeAll is acceptable substitute, or if custom .cur file is needed
   - Recommendation: Use SizeAll during drag operations (closest semantic match), test with users to see if it feels right. Custom cursor is possible via InputDesktopResourceCursor but requires interop.

2. **CanvasControl Invalidate Performance at 60fps**
   - What we know: Invalidate() throttles automatically at display refresh rate, but old GitHub issues (2014-2017) mentioned performance problems
   - What's unclear: Whether these issues persist with modern Win2D + WinUI 3 on current hardware
   - Recommendation: Profile hover state invalidation in real app. If <60fps, consider reducing hover effect complexity (smaller blur radius) or caching effect geometry.

3. **Selection vs. Hover Glow Differentiation**
   - What we know: Both use color-matched glow, need visual distinction
   - What's unclear: Best way to distinguish (size, opacity, color tint, persistent vs. transient)
   - Recommendation: Use opacity and radius (selected: 12px radius at 0.8 opacity, hovered: 8px radius at 0.6 opacity). Selected glow persists, hovered glow is transient.

4. **Time Ruler Alignment After DPI Change**
   - What we know: Win2D handles DPI scaling via CreateResources event with DpiChanged reason
   - What's unclear: Whether ruler/timeline canvases automatically re-sync on DPI change, or if manual re-layout needed
   - Recommendation: Test on multi-DPI setup (moving window between monitors). Likely need to re-calculate canvas Width in CreateResources handler.

## Sources

### Primary (HIGH confidence)
- [Win2D GaussianBlurEffect](https://microsoft.github.io/Win2D/WinUI2/html/T_Microsoft_Graphics_Canvas_Effects_GaussianBlurEffect.htm) - Blur API details
- [Win2D ShadowEffect](https://microsoft.github.io/Win2D/WinUI2/html/T_Microsoft_Graphics_Canvas_Effects_ShadowEffect.htm) - Glow implementation via ShadowColor
- [CoreCursorType Enum](https://learn.microsoft.com/en-us/uwp/api/windows.ui.core.corecursortype?view=winrt-26100) - Complete cursor types list
- [WCAG 2.5.5 Target Size](https://www.w3.org/WAI/WCAG21/Understanding/target-size.html) - 44x44px requirement
- [CanvasControl.Invalidate](https://microsoft.github.io/Win2D/WinUI3/html/M_Microsoft_Graphics_Canvas_UI_Xaml_CanvasControl_Invalidate.htm) - Invalidate throttling behavior
- Current codebase files: SceneBuilderPage.xaml.cs, HitTestHelper.cs, PlayheadRenderer.cs, KeyframeLayerRenderer.cs

### Secondary (MEDIUM confidence)
- [WinUI 3 ElementCursor spec](https://github.com/microsoft/microsoft-ui-xaml-specs/blob/master/active/UIElement/ElementCursor.md) - ProtectedCursor design
- [Win2D CompositeEffect](https://microsoft.github.io/Win2D/WinUI2/html/T_Microsoft_Graphics_Canvas_Effects_BlendEffect.htm) - Multi-layer effects
- [Changing cursor in WinUI 3 discussion](https://github.com/microsoft/WindowsAppSDK/discussions/1816) - Community approaches
- [WCAG Target Size Guide](https://www.wcag.com/developers/2-5-8-target-size-minimum-level-aa/) - Implementation best practices

### Tertiary (LOW confidence)
- [Win2D performance discussions (2014-2017)](https://github.com/Microsoft/Win2D/issues/497) - Dated but shows historical invalidate performance concerns
- [PointerEntered issues on WinUI](https://github.com/microsoft/microsoft-ui-xaml/issues/5505) - Confirms unreliability, but specific to certain controls

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - Win2D and WinUI 3 APIs are official, well-documented
- Architecture: MEDIUM - Hover tracking pattern verified in codebase, glow effect pattern from official docs but not yet implemented
- Pitfalls: MEDIUM - Based on documentation, GitHub issues, and codebase analysis, but not all scenarios tested
- WCAG requirements: HIGH - Official W3C specification, clear and authoritative
- Cursor API: MEDIUM - CoreCursor documented, but ProtectedCursor subclass approach is from spec (not final implementation)

**Research date:** 2026-01-21
**Valid until:** 60 days (Win2D and WinUI 3 are stable, accessibility standards don't change frequently)
