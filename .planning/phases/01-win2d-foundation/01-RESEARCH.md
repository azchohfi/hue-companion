# Phase 1: Win2D Foundation - Research

**Researched:** 2026-01-20
**Domain:** Win2D GPU-accelerated rendering for WinUI 3
**Confidence:** HIGH

## Summary

Win2D is a mature Windows Runtime API providing immediate mode 2D graphics rendering with GPU acceleration via Direct2D. For the Scene Builder timeline migration, the standard pattern is clear: replace XAML Canvas with CanvasControl, move rendering from RenderTimeline() to the Draw event, cache static geometry using CanvasCachedGeometry, and leverage CanvasRenderTarget for layer separation.

Current implementation uses XAML Canvas with manual shape management (Ellipse, Line, Polygon elements added/removed). This retained mode approach creates ~1500+ objects for a typical 8-track, 30-second timeline, forcing WinUI to manage layout, hit testing, and invalidation for each element individually. Win2D's immediate mode rendering draws directly to GPU, eliminating per-element overhead.

**Key findings:**
- Win2D 1.3.0 is already in project dependencies (Microsoft.Graphics.Win2D)
- CanvasControl provides XAML integration with GPU-accelerated rendering
- DPI scaling is automatic when using DIPs (device-independent pixels)
- Hit testing uses CanvasGeometry.FillContainsPoint() instead of XAML's visual tree
- Layer separation achieved via multiple CanvasRenderTarget instances or render order within single Draw event

**Primary recommendation:** Use single CanvasControl with render order layering (ruler → grid → tracks → keyframes → playhead), caching static geometry in CreateResources, and partial invalidation via manual dirty regions.

## Standard Stack

The established tools for Win2D rendering in WinUI 3:

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Microsoft.Graphics.Win2D | 1.3.0 | GPU-accelerated 2D rendering | Official Microsoft package, wraps Direct2D |
| System.Numerics | Built-in | Vector2, Matrix3x2 for transforms | Required by Win2D APIs |
| Microsoft.UI.Xaml | 1.8.x | XAML hosting for CanvasControl | WinUI 3 integration |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| CanvasControl | 1.3.0 | XAML-integrated render surface | Primary rendering (always) |
| CanvasRenderTarget | 1.3.0 | Offscreen render targets | Layer caching, static content |
| CanvasCachedGeometry | 1.3.0 | GPU-optimized geometry | Repeated shapes (grid, ruler) |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| CanvasControl | CanvasAnimatedControl | Provides 60fps game loop but limited WinUI3 support, overkill for timeline |
| Single CanvasControl | Multiple CanvasControl instances | Each control has device overhead, complicates layering |
| CanvasRenderTarget layers | CanvasCommandList | Command lists can't be partially updated, less flexible |

**Installation:**
Already installed. No additional packages needed.

## Architecture Patterns

### Recommended Project Structure
```
Views/
├── SceneBuilderPage.xaml           # XAML with <CanvasControl>
├── SceneBuilderPage.xaml.cs        # Event wiring, interaction logic
└── Rendering/
    ├── TimelineRenderer.cs         # Orchestrates Draw event
    ├── TimeRulerRenderer.cs        # Draws ruler ticks and labels
    ├── TrackLanesRenderer.cs       # Draws track separators and backgrounds
    ├── KeyframeLayerRenderer.cs    # Draws keyframe circles
    ├── PlayheadRenderer.cs         # Draws playhead line and handle
    └── RenderCache.cs              # Manages CanvasCachedGeometry lifetime
```

### Pattern 1: Single CanvasControl with Layered Rendering
**What:** One CanvasControl, render layers by draw order in single Draw event
**When to use:** Standard approach, provides simplest invalidation control
**Example:**
```csharp
// SceneBuilderPage.xaml
<canvas:CanvasControl x:Name="TimelineCanvas"
                      Draw="TimelineCanvas_Draw"
                      CreateResources="TimelineCanvas_CreateResources"
                      PointerPressed="TimelineCanvas_PointerPressed"/>

// SceneBuilderPage.xaml.cs
private void TimelineCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
{
    var ds = args.DrawingSession;

    // Layer 1: Background and grid (static, cached)
    if (_cachedGrid != null)
        ds.DrawCachedGeometry(_cachedGrid, Colors.Gray);

    // Layer 2: Track separators
    _trackRenderer.Draw(ds, ViewModel.Tracks);

    // Layer 3: Keyframes
    _keyframeRenderer.Draw(ds, ViewModel.Tracks, ViewModel.ZoomLevel);

    // Layer 4: Playhead (drawn last = on top)
    _playheadRenderer.Draw(ds, ViewModel.PlayheadPosition, ViewModel.ZoomLevel);
}

private void TimelineCanvas_CreateResources(CanvasControl sender, object args)
{
    // Cache static grid geometry
    var gridGeometry = CreateGridGeometry(sender);
    _cachedGrid = CanvasCachedGeometry.CreateStroke(gridGeometry, 1.0f);
}
```

### Pattern 2: Offscreen Render Targets for Static Layers
**What:** Render static content (ruler, grid) to CanvasRenderTarget once, draw as image
**When to use:** When layer content doesn't change between invalidations
**Example:**
```csharp
private CanvasRenderTarget _rulerLayer;

private void TimelineCanvas_CreateResources(CanvasControl sender, object args)
{
    // Create offscreen target matching control DPI
    _rulerLayer = new CanvasRenderTarget(sender, rulerWidth, rulerHeight);

    using (var ds = _rulerLayer.CreateDrawingSession())
    {
        ds.Clear(Colors.Transparent);
        _rulerRenderer.DrawToTarget(ds, ViewModel.DurationSeconds);
    }
}

private void TimelineCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
{
    // Draw pre-rendered ruler as image (fast)
    args.DrawingSession.DrawImage(_rulerLayer, 0, 0);

    // Draw dynamic content
    _keyframeRenderer.Draw(args.DrawingSession, ViewModel.Tracks);
}
```
**Source:** [Offscreen drawing - Microsoft Learn](https://learn.microsoft.com/en-us/windows/apps/develop/win2d/offscreen-drawing)

### Pattern 3: Optimized Playhead Updates
**What:** During playback, update only playhead without full redraw
**When to use:** To achieve 60fps during animation playback
**Example:**
```csharp
private bool _isPlayheadOnlyUpdate = false;
private double _lastPlayheadX = 0;

private void PlaybackTimer_Tick(object sender, object e)
{
    ViewModel.PlayheadPosition += deltaTime;

    // Mark as playhead-only update (no full timeline redraw)
    _isPlayheadOnlyUpdate = true;
    TimelineCanvas.Invalidate();
}

private void TimelineCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
{
    if (_isPlayheadOnlyUpdate)
    {
        // Clear old playhead region only
        var clearRect = new Rect(_lastPlayheadX - 10, 0, 20, sender.Size.Height);
        using (var layer = args.DrawingSession.CreateLayer(1.0f, clearRect))
        {
            args.DrawingSession.Clear(Colors.Transparent);
        }

        // Draw new playhead
        _playheadRenderer.Draw(args.DrawingSession, ViewModel.PlayheadPosition);
        _lastPlayheadX = ViewModel.PlayheadPosition * ViewModel.ZoomLevel;

        _isPlayheadOnlyUpdate = false;
    }
    else
    {
        // Full redraw
        DrawAllLayers(args.DrawingSession);
    }
}
```
**Note:** This pattern is complex and may not be necessary—benchmark first. Win2D is fast enough that full redraws at 60fps may suffice.

### Pattern 4: Hit Testing with Geometry
**What:** Use CanvasGeometry.FillContainsPoint() for pointer interaction
**When to use:** To detect clicks on keyframes, playhead, ruler
**Example:**
```csharp
private Dictionary<KeyframeViewModel, CanvasGeometry> _keyframeGeometries = new();

private void TimelineCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
{
    var point = e.GetCurrentPoint(TimelineCanvas).Position;
    var clickPoint = new Vector2((float)point.X, (float)point.Y);

    // Check if click hit a keyframe
    foreach (var (keyframe, geometry) in _keyframeGeometries)
    {
        if (geometry.FillContainsPoint(clickPoint))
        {
            HandleKeyframeClick(keyframe, e);
            e.Handled = true;
            return;
        }
    }

    // Check if click hit playhead
    if (_playheadGeometry?.FillContainsPoint(clickPoint) == true)
    {
        StartPlayheadDrag();
        e.Handled = true;
    }
}

private void CreateKeyframeGeometry(CanvasControl sender, KeyframeViewModel kf)
{
    var x = kf.TimeSeconds * ViewModel.ZoomLevel;
    var y = GetTrackY(kf);

    // Create circle geometry for hit testing
    var geometry = CanvasGeometry.CreateCircle(sender, new Vector2(x, y), 8);
    _keyframeGeometries[kf] = geometry;
}
```
**Source:** [CanvasGeometry.FillContainsPoint - Win2D](https://microsoft.github.io/Win2D/WinUI3/html/M_Microsoft_Graphics_Canvas_Geometry_CanvasGeometry_FillContainsPoint_1.htm)

### Anti-Patterns to Avoid
- **Creating CanvasCachedGeometry every frame:** Defeats caching purpose, slower than standard geometry. Cache in CreateResources instead.
- **Opening multiple DrawingSessions per Draw event:** Each session has overhead. Use single session from args.DrawingSession.
- **Storing XAML UIElements alongside Win2D:** Mixing retained and immediate mode creates sync complexity. Choose one approach.
- **Ignoring CreateResources DpiChanged reason:** Device loss and DPI changes require resource recreation. Check args.Reason.

## Don't Hand-Roll

Problems that look simple but have existing solutions:

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Circle drawing | Custom Bezier curves | CanvasGeometry.CreateCircle() | GPU-optimized, handles DPI |
| Line anti-aliasing | Manual pixel interpolation | DrawLine with AntialiasMode.PerPrimitive | Hardware-accelerated |
| Text measurement | Character width tables | CanvasTextLayout.LayoutBounds | Handles fonts, DPI, ligatures |
| Geometry hit testing | Bounding box approximation | CanvasGeometry.FillContainsPoint() | Exact, handles transforms |
| DPI conversion | Manual 96 DPI calculation | ConvertDipsToPixels/ConvertPixelsToDips | Handles system scaling changes |
| Color space conversion | RGB to XY formulas | HueApi.ColorConverters (already in project) | Handles Hue-specific gamut |

**Key insight:** Win2D wraps Direct2D, which has 15+ years of optimization. Custom implementations are almost certainly slower.

## Common Pitfalls

### Pitfall 1: Memory Leaks with CanvasControl
**What goes wrong:** Reference count cycles prevent CanvasControl from being garbage collected
**Why it happens:** CanvasControl holds strong references to event handlers, handlers capture `this`
**How to avoid:**
```csharp
// In page Unloaded event
private void SceneBuilderPage_Unloaded(object sender, RoutedEventArgs e)
{
    TimelineCanvas.RemoveFromVisualTree();
    TimelineCanvas = null;
}
```
**Warning signs:** Memory usage grows after navigating away from page and back multiple times
**Source:** [CanvasControl Class - Win2D](https://microsoft.github.io/Win2D/WinUI3/html/T_Microsoft_Graphics_Canvas_UI_Xaml_CanvasControl.htm)

### Pitfall 2: Forgetting to Clear CanvasRenderTarget
**What goes wrong:** Previous frame's content shows through on offscreen render targets
**Why it happens:** Unlike XAML controls, CanvasRenderTarget doesn't auto-clear
**How to avoid:**
```csharp
using (var ds = renderTarget.CreateDrawingSession())
{
    ds.Clear(Colors.Transparent); // ALWAYS clear first
    // ... drawing operations
}
```
**Warning signs:** Visual artifacts, old content not erasing
**Source:** [Offscreen drawing - Microsoft Learn](https://learn.microsoft.com/en-us/windows/apps/develop/win2d/offscreen-drawing)

### Pitfall 3: Creating Geometries on Every Frame
**What goes wrong:** Performance tanks, can't hit 60fps target
**Why it happens:** CanvasGeometry.Create*() allocates GPU resources, creating thousands per frame overwhelms driver
**How to avoid:**
```csharp
// BAD: Creating geometry every frame
private void Draw(CanvasDrawingSession ds)
{
    foreach (var kf in keyframes)
    {
        var circle = CanvasGeometry.CreateCircle(device, pos, radius); // SLOW
        ds.FillGeometry(circle, color);
    }
}

// GOOD: Cache geometry or use primitives directly
private void Draw(CanvasDrawingSession ds)
{
    foreach (var kf in keyframes)
    {
        ds.FillCircle(pos, radius, color); // Fast primitive
    }
}

// BEST: Cache if drawing same shape repeatedly
private CanvasCachedGeometry _circleTemplate;
private void CreateResources(CanvasControl sender)
{
    var circle = CanvasGeometry.CreateCircle(sender, Vector2.Zero, radius);
    _circleTemplate = CanvasCachedGeometry.CreateFill(circle);
}
```
**Warning signs:** Low frame rate, high CPU usage in CreateCircle/CreateEllipse calls
**Source:** [CanvasCachedGeometry - Win2D](https://microsoft.github.io/Win2D/WinUI3/html/T_Microsoft_Graphics_Canvas_Geometry_CanvasCachedGeometry.htm)

### Pitfall 4: Ignoring DPI Changes During CreateResources
**What goes wrong:** Cached geometry renders at wrong scale after moving window between monitors
**Why it happens:** CreateResources fires with DpiChanged reason when display changes, old cached resources have wrong DPI
**How to avoid:**
```csharp
private void TimelineCanvas_CreateResources(CanvasControl sender,
    CanvasCreateResourcesEventArgs args)
{
    // Check if this is a DPI change
    if (args.Reason == CanvasCreateResourcesReason.DpiChanged)
    {
        // Dispose old cached resources
        _cachedGrid?.Dispose();
        _rulerLayer?.Dispose();
    }

    // Recreate resources at new DPI
    CreateCachedResources(sender);
}
```
**Warning signs:** Blurry rendering after moving between high-DPI and standard displays
**Source:** [DPI and DIPs - Win2D](https://microsoft.github.io/Win2D/WinUI3/html/DPI.htm)

### Pitfall 5: Coordinate System Confusion
**What goes wrong:** Click detection off by several pixels, elements misaligned
**Why it happens:** Mixing pixels and DIPs, forgetting ScrollViewer offset
**How to avoid:**
```csharp
private void TimelineCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
{
    // GetCurrentPoint returns DIPs automatically
    var point = e.GetCurrentPoint(TimelineCanvas).Position;

    // If canvas is inside ScrollViewer, adjust for scroll
    var scrollOffset = GetScrollOffset(); // Account for horizontal scroll
    var canvasX = point.X + scrollOffset;

    // Convert to timeline time (DIPs to seconds)
    var timeSeconds = canvasX / ViewModel.ZoomLevel;
}
```
**Warning signs:** Hit testing works at zoom 100% but breaks at other zoom levels

## Code Examples

Verified patterns from official sources:

### Basic CanvasControl Setup
```csharp
// Source: https://microsoft.github.io/Win2D/WinUI3/html/QuickStart.htm
// XAML
<Page xmlns:canvas="using:Microsoft.Graphics.Canvas.UI.Xaml">
    <canvas:CanvasControl x:Name="TimelineCanvas"
                          Draw="TimelineCanvas_Draw"
                          CreateResources="TimelineCanvas_CreateResources"/>
</Page>

// Code-behind
private void TimelineCanvas_CreateResources(CanvasControl sender, object args)
{
    // Initialize resources that depend on device
    _cachedResources = CreateGeometryCaches(sender);
}

private void TimelineCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
{
    var ds = args.DrawingSession;

    // Example: Draw background
    ds.Clear(Colors.Black);

    // Example: Draw primitives
    ds.DrawLine(0, 0, 100, 100, Colors.White, 2);
    ds.FillCircle(50, 50, 20, Colors.Red);
}
```

### Creating Cached Geometry for Repeated Shapes
```csharp
// Source: https://microsoft.github.io/Win2D/WinUI3/html/T_Microsoft_Graphics_Canvas_Geometry_CanvasCachedGeometry.htm
private CanvasCachedGeometry _gridLinesCache;

private void TimelineCanvas_CreateResources(CanvasControl sender, object args)
{
    // Create geometry for grid lines
    var builder = new CanvasPathBuilder(sender);

    var zoom = ViewModel.ZoomLevel;
    var duration = ViewModel.DurationSeconds;
    var height = sender.Size.Height;

    // Vertical lines every second
    for (double t = 0; t <= duration; t += 1.0)
    {
        var x = (float)(t * zoom);
        builder.BeginFigure(new Vector2(x, 0));
        builder.AddLine(new Vector2(x, (float)height));
        builder.EndFigure(CanvasFigureLoop.Open);
    }

    var geometry = CanvasGeometry.CreatePath(builder);
    _gridLinesCache = CanvasCachedGeometry.CreateStroke(geometry, 1.0f);
}

private void TimelineCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
{
    // Draw cached grid (very fast)
    args.DrawingSession.DrawCachedGeometry(_gridLinesCache, Colors.Gray);
}
```

### Hit Testing with FillContainsPoint
```csharp
// Source: https://microsoft.github.io/Win2D/WinUI3/html/M_Microsoft_Graphics_Canvas_Geometry_CanvasGeometry_FillContainsPoint_1.htm
private CanvasGeometry CreatePlayheadHitArea(CanvasControl sender)
{
    var x = (float)(ViewModel.PlayheadPosition * ViewModel.ZoomLevel);
    var height = (float)sender.Size.Height;

    // Create hit area (wider than visual for easier dragging)
    var rect = new Rect(x - 6, 0, 12, height);
    return CanvasGeometry.CreateRectangle(sender, rect);
}

private void TimelineCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
{
    var point = e.GetCurrentPoint(TimelineCanvas).Position;
    var clickPoint = new Vector2((float)point.X, (float)point.Y);

    // Rebuild geometry at current playhead position
    var hitArea = CreatePlayheadHitArea(TimelineCanvas);

    if (hitArea.FillContainsPoint(clickPoint))
    {
        // Start playhead drag
        _isDraggingPlayhead = true;
        TimelineCanvas.CapturePointer(e.Pointer);
        e.Handled = true;
    }
}
```

### DPI-Aware Resource Creation
```csharp
// Source: https://microsoft.github.io/Win2D/WinUI3/html/DPI.htm
private CanvasRenderTarget _rulerLayer;

private void TimelineCanvas_CreateResources(CanvasControl sender,
    CanvasCreateResourcesEventArgs args)
{
    // Dispose old resources on DPI change
    if (args.Reason == CanvasCreateResourcesReason.DpiChanged)
    {
        _rulerLayer?.Dispose();
    }

    // Create render target matching control's DPI
    // Using (sender, width, height) overload inherits sender's DPI
    _rulerLayer = new CanvasRenderTarget(sender,
        (float)sender.Size.Width,
        40); // Height in DIPs

    RenderRulerToTarget(_rulerLayer);
}

private void RenderRulerToTarget(CanvasRenderTarget target)
{
    using (var ds = target.CreateDrawingSession())
    {
        ds.Clear(Colors.Transparent);

        // Draw ruler ticks - coordinates in DIPs
        for (int i = 0; i <= ViewModel.DurationSeconds; i++)
        {
            var x = i * ViewModel.ZoomLevel;
            var tickHeight = (i % 5 == 0) ? 12 : 6;
            ds.DrawLine(x, 40 - tickHeight, x, 40, Colors.White);
        }
    }
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| XAML Canvas with UIElement shapes | Win2D CanvasControl immediate mode | 2015 (Win2D 1.0) | 10-100x faster for dense scenes |
| Manual DPI scaling | Automatic DIP handling | 2015 (Win2D 1.0) | High-DPI displays work correctly |
| Software rendering fallback | GPU-only via Direct2D | 2015 (Win2D 1.0) | Consistent performance |
| CanvasAnimatedControl | CanvasControl with manual timer | 2021 (WinUI 3 limited support) | CanvasAnimatedControl has partial WinUI3 support |

**Deprecated/outdated:**
- **CanvasAnimatedControl for WinUI 3:** Partial support only, use CanvasControl with DispatcherTimer instead
- **Windows.UI.Xaml.Shapes.* for high-density graphics:** Use Win2D for 1000+ shapes
- **Manual DPI conversion formulas:** Use ConvertDipsToPixels/ConvertPixelsToDips methods

**Source:** [Win2D for WinUI3 (Work in progress)](https://microsoft.github.io/Win2D/WinUI3/html/Introduction.htm)

## Open Questions

Things that couldn't be fully resolved:

1. **Optimal invalidation strategy for partial updates**
   - What we know: Can mark canvas dirty regions manually, CanvasVirtualControl has RegionsInvalidated event
   - What's unclear: Whether manual dirty tracking adds complexity without measurable benefit for timeline scenario
   - Recommendation: Start with full canvas invalidation on every change, profile to verify 60fps, optimize only if necessary

2. **Geometry disposal timing during zoom changes**
   - What we know: CanvasGeometry and CanvasCachedGeometry are IDisposable, should be disposed when no longer needed
   - What's unclear: Whether to dispose immediately on zoom or cache multiple zoom levels
   - Recommendation: Dispose cached geometry on zoom change, recreate in CreateResources with DpiChanged reason

3. **Hit testing performance with 1000+ geometries**
   - What we know: FillContainsPoint is GPU-accelerated, very fast
   - What's unclear: Whether spatial partitioning (quad-tree) is needed for thousands of keyframes
   - Recommendation: Start with linear search through visible keyframes only, optimize if profiling shows hit testing bottleneck

## Sources

### Primary (HIGH confidence)
- [Win2D for WinUI3 Documentation](https://microsoft.github.io/Win2D/WinUI3/html/Introduction.htm) - Official API reference
- [CanvasControl Class](https://microsoft.github.io/Win2D/WinUI3/html/T_Microsoft_Graphics_Canvas_UI_Xaml_CanvasControl.htm) - Core rendering control
- [DPI and DIPs](https://microsoft.github.io/Win2D/WinUI3/html/DPI.htm) - DPI scaling behavior
- [CanvasCachedGeometry](https://microsoft.github.io/Win2D/WinUI3/html/T_Microsoft_Graphics_Canvas_Geometry_CanvasCachedGeometry.htm) - Performance optimization
- [CanvasGeometry.FillContainsPoint](https://microsoft.github.io/Win2D/WinUI3/html/M_Microsoft_Graphics_Canvas_Geometry_CanvasGeometry_FillContainsPoint_1.htm) - Hit testing
- [Offscreen drawing - Microsoft Learn](https://learn.microsoft.com/en-us/windows/apps/develop/win2d/offscreen-drawing) - CanvasRenderTarget patterns

### Secondary (MEDIUM confidence)
- [GitHub - microsoft/Win2D](https://github.com/microsoft/Win2D) - Issue discussions verified against official docs
- [Win2D Roadmap for Windows App SDK - Issue #1150](https://github.com/microsoft/WindowsAppSDK/issues/1150) - WinUI 3 support status
- [Canvas pointer input - Issue #423](https://github.com/Microsoft/Win2D/issues/423) - Pointer event handling patterns

### Tertiary (LOW confidence)
- Web search results about layering strategies - not Win2D-specific, general canvas patterns

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - Official Microsoft package, mature API, already in project
- Architecture: HIGH - Patterns verified with official docs and working WinUI 3 examples
- Pitfalls: HIGH - Documented in official sources or verified through GitHub issues
- Hit testing: HIGH - FillContainsPoint documented with examples
- DPI scaling: HIGH - Automatic DIP handling verified in official docs
- Layering strategy: MEDIUM - Multiple valid approaches, choice depends on profiling
- Partial invalidation optimization: LOW - Advanced technique, unclear if needed for this scenario

**Research date:** 2026-01-20
**Valid until:** 60 days (stable mature API, limited WinUI 3 evolution expected)
