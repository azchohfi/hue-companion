# Phase 2: Gradient Track Visuals - Research

**Researched:** 2026-01-21
**Domain:** Win2D gradient rendering with color interpolation and easing
**Confidence:** HIGH

## Summary

Gradient track visuals require continuous color gradients rendered behind keyframe markers showing how light colors evolve over time. Win2D provides CanvasLinearGradientBrush for GPU-accelerated gradient rendering using CanvasGradientStop arrays. The key challenge is matching gradient interpolation to keyframe TransitionStyle (Linear, EaseIn, EaseOut, EaseInOut, Instant) already defined in the codebase.

Current implementation uses discrete keyframe circles on Timeline canvas. Phase 1 migrated to Win2D CanvasControl with immediate mode rendering. This phase adds gradient strips using FillRoundedRectangle with CanvasLinearGradientBrush. Critical findings: (1) gradient stops must be calculated with easing functions to match TransitionStyle, (2) brush reuse is essential for performance with 8+ tracks, (3) rounded pill shape uses border-radius = half height for full-track-height gradients.

**Key findings:**
- CanvasLinearGradientBrush accepts CanvasGradientStop[] with position (0.0-1.0) and Color
- Gradient stops position maps directly to normalized timeline segment (0.0 = segment start, 1.0 = segment end)
- Easing.cs already implements TransitionStyle formulas (EaseIn: t², EaseOut: 1-(1-t)², EaseInOut: piecewise cubic)
- FillRoundedRectangle draws rounded corners in single call without geometry creation
- Brush caching critical: creating gradient brushes is expensive, reuse across frames
- RGB interpolation in sRGB space is Direct2D default, matches existing HueApi color model

**Primary recommendation:** Create one CanvasLinearGradientBrush per track segment during draw, sample keyframe colors using Easing.Apply() at regular intervals (10-20 stops), draw gradient with FillRoundedRectangle, cache brushes only if profiling shows recreation overhead.

## Standard Stack

The established libraries/tools for gradient rendering in Win2D:

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Microsoft.Graphics.Win2D | 1.3.0 | CanvasLinearGradientBrush, FillRoundedRectangle | Already in project, GPU-accelerated |
| System.Numerics | Built-in | Vector2 for gradient start/end points | Required by Win2D APIs |
| Existing Easing.cs | Current | TransitionStyle interpolation | Already implements exact formulas needed |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| CanvasGradientStop[] | 1.3.0 | Define gradient color positions | One array per segment between keyframes |
| Windows.UI.Color | Built-in | Color values for gradient stops | Convert from HueColor |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| CanvasLinearGradientBrush | CanvasRadialGradientBrush | Wrong shape, gradients are horizontal |
| FillRoundedRectangle | FillGeometry with cached rounded rect | More code, negligible performance difference |
| Calculate stops per-segment | Pre-bake gradients to CanvasRenderTarget | Memory overhead, inflexible for zoom changes |

**Installation:**
Already installed. No additional packages needed.

## Architecture Patterns

### Recommended Project Structure
```
Views/
├── SceneBuilderPage.xaml.cs        # Existing Win2D rendering
└── Rendering/
    ├── TimelineRenderer.cs         # Orchestrates Draw event (existing)
    ├── GradientTrackRenderer.cs    # NEW: Draws gradient strips
    ├── KeyframeLayerRenderer.cs    # Existing: Draws keyframes on top
    └── RenderCache.cs              # Existing: Manages cached resources
```

### Pattern 1: Per-Segment Gradient with Easing
**What:** Create gradient stops by sampling keyframe interpolation at regular intervals
**When to use:** Standard approach for all gradient segments
**Example:**
```csharp
// GradientTrackRenderer.cs
private CanvasLinearGradientBrush CreateSegmentGradient(
    CanvasControl sender,
    KeyframeViewModel startKf,
    KeyframeViewModel endKf,
    double segmentStartX,
    double segmentEndX)
{
    const int numStops = 15; // Sufficient for smooth visual
    var stops = new CanvasGradientStop[numStops];

    for (int i = 0; i < numStops; i++)
    {
        // Normalized position within segment (0.0 to 1.0)
        float position = i / (float)(numStops - 1);

        // Apply easing based on end keyframe's transition style
        double easedT = Easing.Apply(position, endKf.TransitionStyle);

        // Interpolate color (hue shows in gradient, brightness ignored per CONTEXT)
        var color = InterpolateHueColor(startKf.Color, endKf.Color, easedT);

        stops[i] = new CanvasGradientStop
        {
            Position = position,
            Color = color
        };
    }

    // Gradient start/end in canvas coordinates
    var startPoint = new Vector2((float)segmentStartX, 0);
    var endPoint = new Vector2((float)segmentEndX, 0);

    return new CanvasLinearGradientBrush(sender, stops)
    {
        StartPoint = startPoint,
        EndPoint = endPoint
    };
}
```

### Pattern 2: Rounded Pill Shape with Border
**What:** Draw gradient strip with rounded corners and subtle dark border
**When to use:** Every gradient segment rendering
**Example:**
```csharp
private void DrawGradientSegment(
    CanvasDrawingSession ds,
    CanvasLinearGradientBrush gradientBrush,
    double x,
    double y,
    double width,
    double height)
{
    // Pill shape: radius = half height for full rounding
    float cornerRadius = (float)(height / 2);

    var rect = new Rect(x, y, width, height);

    // Fill with gradient
    ds.FillRoundedRectangle(rect, cornerRadius, cornerRadius, gradientBrush);

    // Draw border (subtle dark outline)
    var borderColor = Color.FromArgb(80, 0, 0, 0); // Semi-transparent black
    ds.DrawRoundedRectangle(rect, cornerRadius, cornerRadius, borderColor, 1.0f);
}
```
**Source:** [FillRoundedRectangle - Win2D](https://microsoft.github.io/Win2D/WinUI3/html/M_Microsoft_Graphics_Canvas_CanvasDrawingSession_FillRoundedRectangle_9.htm)

### Pattern 3: Edge Extension for First/Last Keyframes
**What:** Extend first keyframe color to timeline start, last keyframe color to timeline end
**When to use:** Tracks with keyframes that don't span full timeline
**Example:**
```csharp
private void DrawTrackGradient(CanvasDrawingSession ds, TrackViewModel track, double trackY)
{
    var keyframes = track.Keyframes.OrderBy(k => k.TimeSeconds).ToList();
    if (keyframes.Count == 0) return;

    double trackHeight = 50;
    var firstKf = keyframes[0];
    var lastKf = keyframes[^1];

    // Extend first keyframe color backward to 0s (solid fill)
    if (firstKf.TimeSeconds > 0)
    {
        var extendRect = new Rect(0, trackY, firstKf.TimeSeconds * ZoomLevel, trackHeight);
        var solidBrush = new CanvasSolidColorBrush(ds, ToColor(firstKf.Color));
        ds.FillRoundedRectangle(extendRect, (float)(trackHeight / 2), (float)(trackHeight / 2), solidBrush);
    }

    // Draw gradient segments between keyframes
    for (int i = 0; i < keyframes.Count - 1; i++)
    {
        var startKf = keyframes[i];
        var endKf = keyframes[i + 1];
        var gradientBrush = CreateSegmentGradient(sender, startKf, endKf, ...);
        DrawGradientSegment(ds, gradientBrush, ...);
    }

    // Extend last keyframe color forward to duration (solid fill)
    if (lastKf.TimeSeconds < DurationSeconds)
    {
        var extendX = lastKf.TimeSeconds * ZoomLevel;
        var extendWidth = (DurationSeconds - lastKf.TimeSeconds) * ZoomLevel;
        var extendRect = new Rect(extendX, trackY, extendWidth, trackHeight);
        var solidBrush = new CanvasSolidColorBrush(ds, ToColor(lastKf.Color));
        ds.FillRoundedRectangle(extendRect, (float)(trackHeight / 2), (float)(trackHeight / 2), solidBrush);
    }
}
```

### Pattern 4: Keyframe Contrast Outline
**What:** Add white or dark outline to keyframes for visibility on any gradient color
**When to use:** Always render keyframes after gradient strips
**Example:**
```csharp
private void DrawKeyframeWithContrast(CanvasDrawingSession ds, KeyframeViewModel kf, double x, double y)
{
    float radius = kf.IsSelected ? 10f : 8f;

    // Determine contrast color based on keyframe color brightness
    var contrastColor = GetBrightness(kf.Color) > 0.5
        ? Colors.Black  // Dark outline on bright colors
        : Colors.White; // White outline on dark colors

    // Draw contrast outline (slightly larger)
    ds.FillCircle(new Vector2((float)x, (float)y), radius + 2, contrastColor);

    // Draw keyframe circle on top
    ds.FillCircle(new Vector2((float)x, (float)y), radius, ToColor(kf.Color));

    // Selected keyframe gets highlight border
    if (kf.IsSelected)
    {
        ds.DrawCircle(new Vector2((float)x, (float)y), radius + 4, Colors.Yellow, 2.0f);
    }
}
```

### Anti-Patterns to Avoid
- **Creating CanvasLinearGradientBrush every frame without profiling:** May be acceptable given GPU optimization, but measure first. If slow, cache brushes keyed by (startColor, endColor, transitionStyle).
- **Using hundreds of gradient stops:** Diminishing returns after ~15-20 stops. More stops = more GPU work for negligible visual improvement.
- **Interpolating in HSL color space:** Creates pink banding artifacts. Stick with RGB (Direct2D default).
- **Forgetting to dispose brushes:** CanvasLinearGradientBrush is IDisposable. Use `using` statements or dispose in CreateResources cleanup.

## Don't Hand-Roll

Problems that look simple but have existing solutions:

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Easing functions | Custom quadratic formulas | Easing.Apply() from Easing.cs | Already implements all TransitionStyle cases |
| Rounded rectangle geometry | CanvasGeometry.CreateRoundedRectangle | FillRoundedRectangle() primitive | No geometry object needed, faster |
| Color interpolation | Manual RGB lerp | Existing interpolation in AnimationEngine | Handles HueColor conversion, tested |
| Gradient stop generation | Manual loop with hardcoded positions | Pattern 1 with Easing.Apply() | Matches transition styles exactly |

**Key insight:** Win2D primitives (FillRoundedRectangle) are optimized specifically to avoid geometry creation overhead. Use them over manual geometry when possible.

## Common Pitfalls

### Pitfall 1: Gradient Doesn't Match Keyframe Transition
**What goes wrong:** Gradient shows linear interpolation but keyframe uses EaseInOut, visual mismatch
**Why it happens:** Forgetting to apply Easing.Apply() when calculating gradient stop positions
**How to avoid:**
```csharp
// BAD: Linear gradient stops (ignores TransitionStyle)
for (int i = 0; i < numStops; i++)
{
    float t = i / (float)(numStops - 1);
    var color = Lerp(startColor, endColor, t); // Wrong
    stops[i] = new CanvasGradientStop { Position = t, Color = color };
}

// GOOD: Apply easing to match keyframe transition
for (int i = 0; i < numStops; i++)
{
    float t = i / (float)(numStops - 1);
    double easedT = Easing.Apply(t, endKeyframe.TransitionStyle); // Correct
    var color = Lerp(startColor, endColor, easedT);
    stops[i] = new CanvasGradientStop { Position = t, Color = color };
}
```
**Warning signs:** Gradient looks smooth but doesn't match light animation during playback

### Pitfall 2: Very Short Segments Cause Visual Artifacts
**What goes wrong:** Gradient segments < 5 pixels wide show pixelation or incorrect colors
**Why it happens:** Insufficient width for gradient to render cleanly at high zoom
**How to avoid:**
```csharp
private void DrawGradientSegment(/* ... */)
{
    double segmentWidth = (endKf.TimeSeconds - startKf.TimeSeconds) * ZoomLevel;

    // If segment too narrow, fall back to solid color (midpoint)
    if (segmentWidth < 5.0)
    {
        var midColor = InterpolateHueColor(startKf.Color, endKf.Color, 0.5);
        var solidBrush = new CanvasSolidColorBrush(ds, midColor);
        ds.FillRoundedRectangle(rect, cornerRadius, cornerRadius, solidBrush);
        return;
    }

    // Normal gradient rendering
    var gradientBrush = CreateSegmentGradient(/* ... */);
    ds.FillRoundedRectangle(rect, cornerRadius, cornerRadius, gradientBrush);
}
```
**Warning signs:** Odd colors or banding when zoomed in very far

### Pitfall 3: Brush Memory Leaks
**What goes wrong:** Memory usage grows over time, app becomes sluggish
**Why it happens:** CanvasLinearGradientBrush is IDisposable but not disposed after use
**How to avoid:**
```csharp
// If creating brushes per-frame without caching
private void DrawTrackGradient(CanvasDrawingSession ds, /* ... */)
{
    using var gradientBrush = CreateSegmentGradient(/* ... */);
    DrawGradientSegment(ds, gradientBrush, /* ... */);
    // Brush auto-disposed here
}

// If caching brushes (advanced)
private Dictionary<GradientKey, CanvasLinearGradientBrush> _gradientCache = new();

private void OnZoomChanged()
{
    // Dispose all cached brushes when zoom changes
    foreach (var brush in _gradientCache.Values)
        brush.Dispose();
    _gradientCache.Clear();
}
```
**Warning signs:** Increasing memory usage in Task Manager, GC activity spikes

### Pitfall 4: Corner Radius Doesn't Scale with Track Height
**What goes wrong:** Rounded corners look wrong when track height changes
**Why it happens:** Hardcoded corner radius instead of calculating from height
**How to avoid:**
```csharp
// BAD: Hardcoded radius
float cornerRadius = 25f; // Wrong if trackHeight != 50

// GOOD: Calculate from track height
float cornerRadius = (float)(trackHeight / 2); // Always pill-shaped
```
**Warning signs:** Corners don't look fully rounded or oval-shaped instead of pill

### Pitfall 5: Keyframes Hidden Behind Gradient
**What goes wrong:** Keyframe circles not visible, can't interact with them
**Why it happens:** Drawing keyframes before gradient strips (wrong layer order)
**How to avoid:**
```csharp
private void TimelineCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
{
    var ds = args.DrawingSession;

    // Layer 1: Background grid (bottom)
    DrawGrid(ds);

    // Layer 2: Track separators
    DrawTrackSeparators(ds);

    // Layer 3: Gradient strips (BEFORE keyframes)
    _gradientTrackRenderer.Draw(ds, ViewModel.Tracks);

    // Layer 4: Keyframes (ON TOP of gradients)
    _keyframeRenderer.Draw(ds, ViewModel.Tracks);

    // Layer 5: Playhead (top)
    _playheadRenderer.Draw(ds, ViewModel.PlayheadPosition);
}
```
**Warning signs:** Can't click keyframes, visually obscured

## Code Examples

Verified patterns from official sources and project code:

### Creating Gradient Stops with Easing
```csharp
// Source: Easing.cs (project), CanvasLinearGradientBrush docs
private CanvasGradientStop[] CreateEasedGradientStops(
    KeyframeViewModel startKf,
    KeyframeViewModel endKf,
    int numStops = 15)
{
    var stops = new CanvasGradientStop[numStops];

    for (int i = 0; i < numStops; i++)
    {
        // Linear position (0.0 to 1.0)
        float position = i / (float)(numStops - 1);

        // Apply easing function from endKf's transition style
        double easedT = Easing.Apply(position, endKf.TransitionStyle);

        // Interpolate hue color (brightness not shown in gradient)
        var interpolatedColor = InterpolateColor(
            startKf.Color,
            endKf.Color,
            easedT);

        stops[i] = new CanvasGradientStop
        {
            Position = position,
            Color = ToWindowsColor(interpolatedColor)
        };
    }

    return stops;
}

// Example: TransitionStyle.EaseInOut creates more stops at start/end
// Position: [0.0, 0.067, 0.133, 0.2, 0.267, 0.333, 0.4, 0.467, 0.533, 0.6, 0.667, 0.733, 0.8, 0.867, 0.933, 1.0]
// Eased:    [0.0, 0.009, 0.035, 0.08, 0.143, 0.222, 0.320, 0.437, 0.563, 0.680, 0.778, 0.857, 0.920, 0.965, 0.991, 1.0]
// Notice how eased values cluster near 0.0 and 1.0 (ease-in-out effect)
```

### Handling Instant Transitions
```csharp
// Source: Easing.cs TransitionStyle.Instant case
private CanvasLinearGradientBrush CreateInstantGradient(
    CanvasControl sender,
    KeyframeViewModel startKf,
    KeyframeViewModel endKf,
    Vector2 startPoint,
    Vector2 endPoint)
{
    // Instant transition: hard cut at 100% position
    var stops = new CanvasGradientStop[]
    {
        new() { Position = 0.0f, Color = ToWindowsColor(startKf.Color) },
        new() { Position = 0.99f, Color = ToWindowsColor(startKf.Color) },
        new() { Position = 1.0f, Color = ToWindowsColor(endKf.Color) }
    };

    return new CanvasLinearGradientBrush(sender, stops)
    {
        StartPoint = startPoint,
        EndPoint = endPoint
    };
}
```

### Full Track Gradient Rendering
```csharp
// Complete example with edge extension and segments
public void Draw(CanvasDrawingSession ds, ObservableCollection<TrackViewModel> tracks, double zoomLevel)
{
    const double trackHeight = 50;
    double currentY = 60; // Below ruler

    foreach (var track in tracks)
    {
        var keyframes = track.Keyframes.OrderBy(k => k.TimeSeconds).ToList();
        if (keyframes.Count == 0)
        {
            currentY += trackHeight;
            continue;
        }

        var firstKf = keyframes[0];
        var lastKf = keyframes[^1];

        // Edge extension: before first keyframe
        if (firstKf.TimeSeconds > 0)
        {
            DrawSolidSegment(ds, 0, currentY,
                firstKf.TimeSeconds * zoomLevel, trackHeight,
                ToWindowsColor(firstKf.Color));
        }

        // Gradient segments between keyframes
        for (int i = 0; i < keyframes.Count - 1; i++)
        {
            var startKf = keyframes[i];
            var endKf = keyframes[i + 1];

            double segmentStartX = startKf.TimeSeconds * zoomLevel;
            double segmentEndX = endKf.TimeSeconds * zoomLevel;
            double segmentWidth = segmentEndX - segmentStartX;

            // Handle very short segments
            if (segmentWidth < 5.0)
            {
                var midColor = InterpolateColor(startKf.Color, endKf.Color, 0.5);
                DrawSolidSegment(ds, segmentStartX, currentY, segmentWidth, trackHeight, midColor);
            }
            else
            {
                using var gradientBrush = CreateSegmentGradient(
                    ds.Device, startKf, endKf,
                    new Vector2((float)segmentStartX, 0),
                    new Vector2((float)segmentEndX, 0));

                DrawGradientSegment(ds, gradientBrush, segmentStartX, currentY, segmentWidth, trackHeight);
            }
        }

        // Edge extension: after last keyframe
        double durationSeconds = GetDurationSeconds();
        if (lastKf.TimeSeconds < durationSeconds)
        {
            double extendX = lastKf.TimeSeconds * zoomLevel;
            double extendWidth = (durationSeconds - lastKf.TimeSeconds) * zoomLevel;
            DrawSolidSegment(ds, extendX, currentY, extendWidth, trackHeight,
                ToWindowsColor(lastKf.Color));
        }

        currentY += trackHeight;
    }
}

private void DrawSolidSegment(CanvasDrawingSession ds, double x, double y, double width, double height, Color color)
{
    float cornerRadius = (float)(height / 2);
    var rect = new Rect(x, y, width, height);
    using var brush = new CanvasSolidColorBrush(ds, color);
    ds.FillRoundedRectangle(rect, cornerRadius, cornerRadius, brush);

    // Border
    var borderColor = Color.FromArgb(80, 0, 0, 0);
    ds.DrawRoundedRectangle(rect, cornerRadius, cornerRadius, borderColor, 1.0f);
}

private void DrawGradientSegment(CanvasDrawingSession ds, CanvasLinearGradientBrush brush, double x, double y, double width, double height)
{
    float cornerRadius = (float)(height / 2);
    var rect = new Rect(x, y, width, height);
    ds.FillRoundedRectangle(rect, cornerRadius, cornerRadius, brush);

    // Border
    var borderColor = Color.FromArgb(80, 0, 0, 0);
    ds.DrawRoundedRectangle(rect, cornerRadius, cornerRadius, borderColor, 1.0f);
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Discrete keyframe circles only | Gradient strips + keyframe markers | This phase | Visual context for color evolution |
| Linear RGB interpolation in CSS | Perceptually uniform (Oklab, OKLCH) | 2023-2024 (CSS spec) | Not applicable - Win2D uses sRGB |
| Many gradient stops (50+) | Optimized stop count (10-20) | 2020s (performance research) | Faster rendering, same visual quality |
| Manual rounded rect geometry | FillRoundedRectangle primitive | Win2D 1.0 (2015) | Simpler code, faster |

**Deprecated/outdated:**
- **HSL color space for gradients:** Creates banding artifacts (pink hue spikes). Use RGB.
- **CanvasGeometry for rounded rectangles:** Use FillRoundedRectangle primitive instead.
- **Gradient stops at every pixel:** Diminishing returns after ~15-20 stops.

## Open Questions

Things that couldn't be fully resolved:

1. **Optimal number of gradient stops per segment**
   - What we know: 15-20 stops sufficient for smooth visual (CSS gradient research), more stops = more GPU work
   - What's unclear: Exact cutoff where additional stops become invisible
   - Recommendation: Start with 15 stops, profile rendering, adjust if visual banding appears

2. **Brush caching vs per-frame recreation**
   - What we know: Creating gradient brushes is expensive (Direct2D docs), but Win2D is GPU-optimized
   - What's unclear: Whether per-frame creation is acceptable at 8 tracks × average 5 segments = 40 brushes/frame
   - Recommendation: Start without caching (simpler code), profile frame time, add cache only if > 16ms (60fps)

3. **Color space for gradient interpolation**
   - What we know: Win2D uses sRGB by default (Direct2D behavior), perceptually uniform spaces (Oklab) avoid banding
   - What's unclear: Whether Win2D exposes color space control for gradients (couldn't find in docs)
   - Recommendation: Use default sRGB, test for visible banding with extreme colors (red→green), add dithering noise if needed

4. **Border color for maximum contrast**
   - What we know: Subtle dark border recommended (Logic Pro style), semi-transparent black works on most colors
   - What's unclear: Exact opacity and whether white border needed on very dark gradients
   - Recommendation: Start with Color.FromArgb(80, 0, 0, 0), user test on dark scenes, adjust if invisible

## Sources

### Primary (HIGH confidence)
- [CanvasLinearGradientBrush Class](https://microsoft.github.io/Win2D/WinUI2/html/T_Microsoft_Graphics_Canvas_Brushes_CanvasLinearGradientBrush.htm) - Gradient brush API
- [CanvasGradientStop Structure](https://microsoft.github.io/Win2D/WinUI3/html/T_Microsoft_Graphics_Canvas_Brushes_CanvasGradientStop.htm) - Gradient stop fields
- [FillRoundedRectangle Method](https://microsoft.github.io/Win2D/WinUI3/html/M_Microsoft_Graphics_Canvas_CanvasDrawingSession_FillRoundedRectangle_9.htm) - Rounded rectangle drawing
- [CanvasCachedGeometry Class](https://microsoft.github.io/Win2D/WinUI3/html/T_Microsoft_Graphics_Canvas_Geometry_CanvasCachedGeometry.htm) - Performance optimization
- [Improving Direct2D Performance](https://learn.microsoft.com/en-us/windows/win32/direct2d/improving-direct2d-performance) - Brush reuse, resource caching
- Project Easing.cs - TransitionStyle formulas (EaseIn, EaseOut, EaseInOut, Instant)
- Project AnimationDefinition.cs - TransitionStyle enum definition

### Secondary (MEDIUM confidence)
- [Color Interpolation - ColorAide](https://facelessuser.github.io/coloraide/interpolation/) - sRGB vs linear RGB, easing functions
- [CSS Gradients Performance](https://tryhoverify.com/blog/i-wish-i-had-known-this-sooner-about-css-gradient-performance/) - Gradient stop optimization (verified principle applies to GPU gradients)
- [Pill Button Design](https://medium.com/design-bootcamp/building-a-consistent-corner-radius-system-in-ui-1f86eed56dd3) - Corner radius = height/2 for pill shape
- [CubicBezierEasingFunction](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.composition.cubicbeziereasingfunction?view=windows-app-sdk-1.7) - Windows composition easing (confirms Easing.cs formulas)

### Tertiary (LOW confidence)
- [Color Banding in Gradients](https://blog.frost.kiwi/GLSL-noise-and-radial-gradient/) - Dithering techniques (may not be needed)
- [Logic Pro Color Regions](https://support.apple.com/guide/logicpro/change-the-color-of-regions-lgcpf7c0db8c/mac) - Visual reference (no technical detail on rounded corners)
- WebSearch results on narrow gradient segments - General CSS guidance, not Win2D-specific

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - Win2D already in project, primitives documented
- Gradient rendering: HIGH - CanvasLinearGradientBrush official API, well-documented
- Easing integration: HIGH - Easing.cs already implements exact formulas needed
- Rounded pill shape: HIGH - FillRoundedRectangle documented, radius formula standard UI pattern
- Performance: MEDIUM - Brush reuse recommended in Direct2D docs, but unclear if needed at 8-track scale
- Color interpolation space: MEDIUM - Win2D uses sRGB default, but color space control not found in docs
- Border styling: MEDIUM - Semi-transparent black is common pattern, but exact opacity is aesthetic choice

**Research date:** 2026-01-21
**Valid until:** 60 days (stable Win2D API, rendering patterns unlikely to change)
