# Architecture Research: DAW Timeline UI

**Domain:** Timeline-based animation editor (DAW-style)
**Researched:** 2026-01-20
**Confidence:** MEDIUM-HIGH

## Executive Summary

Research into DAW timeline architecture patterns reveals that professional timeline editors separate concerns into distinct components (ruler, tracks, playhead, transport controls) with clear render layer ordering and hit testing strategies. The current Scene Builder implementation uses a monolithic rendering approach where all elements are drawn in a single `RenderTimeline()` method. This works but creates challenges for adding polish features (gradient strips, better playhead affordances, hover states) without introducing complexity.

**Key finding:** Component separation improves maintainability and enables incremental rendering optimizations. Timeline editors benefit from treating the ruler, track lanes, keyframes, playhead, and transport as separate logical and visual components with clear z-order and interaction boundaries.

## Current Implementation Analysis

### Monolithic Rendering Pattern

The current `SceneBuilderPage.xaml.cs` uses a single canvas with all rendering in one method:

```
RenderTimeline() executes in order:
1. Clear canvas
2. Render time ruler ticks → TimeRulerCanvas
3. Render track separators → KeyframeCanvas
4. Render loop region background → KeyframeCanvas
5. Render grid lines → KeyframeCanvas
6. Render keyframes (for each track) → KeyframeCanvas
7. Render event tracks → KeyframeCanvas
8. Render playhead (hit area, line, handle) → KeyframeCanvas
```

**Strengths:**
- Simple to understand and debug
- Predictable render order
- Low overhead for current feature set

**Limitations:**
- Full re-render on any state change (except playhead during playback)
- Difficult to add per-track gradients or polish without entangling logic
- Hit testing relies on manual z-order management via Tags
- No hover state infrastructure
- Hard to add component-specific animations

### Current Z-Order

Render order determines z-index (later = on top):

```
Layer 1 (bottom): Loop region background rectangle
Layer 2: Grid lines
Layer 3: Track separator lines
Layer 4: Event track backgrounds
Layer 5: Event track pattern indicators
Layer 6: Keyframe circles
Layer 7: Playhead (hit area, visible line, handle)
Layer 8 (top): Temporary pulse effects (event triggers)
```

This works because WinUI Canvas renders children in order, but there's no explicit layering system.

## Recommended Architecture

### Component Structure

Break timeline into logical components with clear responsibilities:

| Component | Responsibility | Render Target | Interaction |
|-----------|---------------|---------------|-------------|
| **TimeRuler** | Time scale ticks, labels, snap markers, ruler playhead marker | `TimeRulerCanvas` (separate) | Click to seek playhead |
| **TrackLanes** | Track backgrounds, separators, gradient color strips | `TrackBackgroundCanvas` (bottom layer) | Click to select track |
| **GridOverlay** | Snap grid lines, loop region highlight | `GridCanvas` (middle layer) | Non-interactive |
| **KeyframeLayer** | Keyframe circles, transition curves | `KeyframeCanvas` (top-middle layer) | Drag, click, right-click keyframes |
| **PlayheadComponent** | Playhead line, handle, scrubbing hit area | `PlayheadCanvas` (top layer) | Drag to scrub |
| **TransportControls** | Play, pause, stop, loop, zoom controls | Standard XAML controls (separate Grid) | Standard button/slider interaction |
| **InspectorPanel** | Keyframe properties, event track properties | Flyout side panel (separate Grid) | Form input |

**Why separate components:**
- Each component can render independently when its state changes
- Clear ownership of interaction areas (no ambiguous hit testing)
- Easier to add polish to one component without touching others
- Components can have their own visual state (hover, selected, dragging)

### Render Layer Ordering

Use multiple overlapping Canvas elements with explicit z-index via `Canvas.ZIndex` attached property:

```
┌─────────────────────────────────────┐
│ TimeRulerCanvas (Z=0, top border)   │  ← Click to seek
├─────────────────────────────────────┤
│ Scrollable Timeline Area:           │
│  ┌──────────────────────────────┐  │
│  │ TrackBackgroundCanvas (Z=1)  │  │  ← Gradients, separators
│  │ GridCanvas (Z=2)             │  │  ← Snap lines, loop region
│  │ KeyframeCanvas (Z=3)         │  │  ← Keyframes, event pulses
│  │ PlayheadCanvas (Z=4)         │  │  ← Playhead line + handle
│  └──────────────────────────────┘  │
└─────────────────────────────────────┘
```

**Layer 1 - TrackBackgroundCanvas (Z=1):**
- Track lane backgrounds (subtle color tint per track)
- Horizontal gradient color strips showing current track color
- Track separator lines
- Event track backgrounds

**Layer 2 - GridCanvas (Z=2):**
- Vertical snap grid lines
- Loop region highlight rectangle
- Non-interactive visual guides

**Layer 3 - KeyframeCanvas (Z=3):**
- Keyframe circles (colored, with selection highlights)
- Event track pattern indicators
- Temporary event pulse animations

**Layer 4 - PlayheadCanvas (Z=4):**
- Wide invisible hit area for scrubbing
- Visible playhead line (thin, red)
- Draggable playhead handle (triangle at top)

**Rationale:** Separating layers by interaction type (background, guides, interactive elements, playhead) enables clean hit testing and incremental updates.

### Component Boundaries

**TimeRuler Component:**
```
Inputs:
- DurationSeconds
- ZoomLevel
- PlayheadPosition
- SnapInterval (for grid ticks)

Outputs:
- Clicked(timeSeconds) event

Render:
- Tick marks at intervals (1s, 5s depending on zoom)
- Time labels (e.g., "0s", "5s", "10s")
- Playhead marker triangle
```

**TrackLanes Component:**
```
Inputs:
- Tracks[] (light tracks)
- EventTracks[]
- DurationSeconds
- ZoomLevel

Outputs:
- TrackClicked(trackIndex) event

Render:
- Per-track background rectangles (with gradient color strips)
- Horizontal separator lines
- Event track backgrounds (warm tint)
```

**GridOverlay Component:**
```
Inputs:
- DurationSeconds
- ZoomLevel
- SnapInterval
- IsSnapEnabled
- LoopRegion (start, end)

Outputs:
- None (non-interactive)

Render:
- Vertical grid lines at snap intervals
- Loop region highlight rectangle
```

**KeyframeLayer Component:**
```
Inputs:
- Tracks[] with Keyframes[]
- EventTracks[]
- SelectedKeyframes[]
- ZoomLevel

Outputs:
- KeyframeClicked(keyframe, track, isShiftHeld)
- KeyframeDragged(keyframe, newTimeSeconds)
- KeyframeRightClicked(keyframe, track)
- EmptySpaceClicked(trackIndex, timeSeconds)

Render:
- Keyframe circles (size, color, stroke based on selection)
- Event track pattern indicators (dashed lines)
- Event pulse animations (temporary)
```

**PlayheadComponent:**
```
Inputs:
- PlayheadPosition
- ZoomLevel
- CanvasHeight

Outputs:
- PlayheadDragged(newTimeSeconds)

Render:
- Invisible wide hit area (12px, transparent)
- Visible playhead line (2px, red)
- Draggable handle (triangle, 16px wide)
```

**Why these boundaries:**
- Each component has single responsibility
- Clear input/output contract
- Components can be tested independently
- Easy to add features to one component (e.g., gradient to TrackLanes)

## Hit Testing Strategy

### Current Approach (Tag-Based)

Current implementation stores metadata in `FrameworkElement.Tag`:
```csharp
circle.Tag = (keyframe, track);
circle.PointerPressed += Keyframe_PointerPressed;
```

Hit testing is implicit - WinUI routes events to the topmost hittable element.

**Problem:** No hover state, no visual feedback before click, no multi-select rectangle.

### Recommended Approach (Layer-Based with Explicit Priority)

Use layered canvases where each layer handles its own hit testing:

**Layer 4 (PlayheadCanvas) - Highest priority:**
```
if (element is in PlayheadCanvas && pointer over hit area):
    Start playhead drag
    e.Handled = true  // Prevent lower layers from seeing event
```

**Layer 3 (KeyframeCanvas) - Interactive elements:**
```
if (element is Keyframe circle):
    Show hover state (brighten stroke)
    On click: Select keyframe, start drag
    On right-click: Show context menu
    e.Handled = true
else if (empty space in track lane):
    On click: Create new keyframe at position
```

**Layer 2 (GridCanvas) - Non-interactive:**
```
IsHitTestVisible = false  // Events pass through
```

**Layer 1 (TrackBackgroundCanvas) - Lowest priority:**
```
if (element is track background):
    On click: Select track (deselect keyframes)
    e.Handled = true
```

**Priority order:** Playhead > Keyframes > Track backgrounds > Ruler

### Hover State Infrastructure

Add hover detection for polish:

```csharp
// In KeyframeLayer component
private KeyframeViewModel? _hoveredKeyframe;

private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
{
    var point = e.GetCurrentPoint(KeyframeCanvas);
    var newHovered = HitTestKeyframe(point.Position);

    if (newHovered != _hoveredKeyframe)
    {
        if (_hoveredKeyframe != null)
            RenderKeyframe(_hoveredKeyframe, isHovered: false);

        _hoveredKeyframe = newHovered;

        if (_hoveredKeyframe != null)
            RenderKeyframe(_hoveredKeyframe, isHovered: true);
    }
}

private void RenderKeyframe(KeyframeViewModel kf, bool isHovered)
{
    var strokeThickness = isHovered ? 3 : 2;
    var glowEffect = isHovered ? AddGlow() : null;
    // ... render with hover state
}
```

**Benefits:** Immediate visual feedback, better discoverability of interactive elements.

## State Management

### Current Approach

All state lives in `SceneBuilderViewModel`:
- Tracks[]
- EventTracks[]
- SelectedKeyframe
- SelectedEventTrack
- PlayheadPosition
- ZoomLevel
- IsPlaying
- etc.

Page code-behind (`SceneBuilderPage.xaml.cs`) reads state and renders directly.

**Problem:** No separation between timeline state and rendering state (hover, drag, temporary animations).

### Recommended Approach

**ViewModel state (persisted/shared):**
- Tracks[] with Keyframes[]
- EventTracks[]
- PlayheadPosition
- DurationSeconds
- ZoomLevel
- SelectedKeyframes[] (multi-select)

**Component-local state (transient):**
- TrackLanes: hoveredTrackIndex
- KeyframeLayer: hoveredKeyframe, isDragging, dragStartPosition
- PlayheadComponent: isDragging
- GridOverlay: none (stateless)

**Why separate:**
- ViewModel state is serializable (save/load scenes)
- Component state is ephemeral (doesn't need to persist)
- Easier to reason about what needs to save vs what's just UI polish

### Selection State

Support multi-select with explicit collection:

```csharp
// In ViewModel
public ObservableCollection<KeyframeViewModel> SelectedKeyframes { get; }

// Selection logic
public void ToggleKeyframeSelection(KeyframeViewModel kf, bool isMultiSelect)
{
    if (isMultiSelect)
    {
        if (SelectedKeyframes.Contains(kf))
            SelectedKeyframes.Remove(kf);
        else
            SelectedKeyframes.Add(kf);
    }
    else
    {
        SelectedKeyframes.Clear();
        SelectedKeyframes.Add(kf);
    }
}
```

**Render selected keyframes with highlight:**
```csharp
var isSelected = ViewModel.SelectedKeyframes.Contains(keyframe);
var strokeColor = isSelected ? SelectionColor : WhiteColor;
var strokeThickness = isSelected ? 3 : 2;
```

## Build Order and Dependencies

### Phase 1: Extract Components (Foundation)

**Goal:** Separate rendering logic without changing functionality.

1. **Create component classes** (not full controls, just logic separation):
   - `TimeRulerRenderer.cs` - Render ruler to TimeRulerCanvas
   - `TrackLanesRenderer.cs` - Render track backgrounds to TrackBackgroundCanvas
   - `GridOverlayRenderer.cs` - Render grid to GridCanvas
   - `KeyframeLayerRenderer.cs` - Render keyframes to KeyframeCanvas
   - `PlayheadRenderer.cs` - Render playhead to PlayheadCanvas

2. **Update XAML** to have layered canvases:
   ```xaml
   <Grid>
       <Canvas x:Name="TrackBackgroundCanvas" Canvas.ZIndex="1" />
       <Canvas x:Name="GridCanvas" Canvas.ZIndex="2" IsHitTestVisible="False" />
       <Canvas x:Name="KeyframeCanvas" Canvas.ZIndex="3" />
       <Canvas x:Name="PlayheadCanvas" Canvas.ZIndex="4" />
   </Grid>
   ```

3. **Refactor `RenderTimeline()`** to delegate:
   ```csharp
   private void RenderTimeline()
   {
       _rulerRenderer.Render(TimeRulerCanvas, ViewModel);
       _trackLanesRenderer.Render(TrackBackgroundCanvas, ViewModel);
       _gridRenderer.Render(GridCanvas, ViewModel);
       _keyframeRenderer.Render(KeyframeCanvas, ViewModel);
       _playheadRenderer.Render(PlayheadCanvas, ViewModel);
   }
   ```

**Dependency:** None (refactoring only)

### Phase 2: Add Per-Component State (Polish Infrastructure)

**Goal:** Enable hover states and component-specific polish.

1. **Add hover detection to KeyframeLayerRenderer:**
   - Track `_hoveredKeyframe`
   - Subscribe to PointerMoved on KeyframeCanvas
   - Re-render only hovered/unhovered keyframes

2. **Add hover detection to TrackLanesRenderer:**
   - Track `_hoveredTrackIndex`
   - Brighten track background on hover

3. **Add playhead scrubbing affordance:**
   - Show cursor change on playhead hover
   - Highlight playhead handle on hover

**Dependency:** Phase 1 complete (component separation exists)

### Phase 3: Incremental Rendering Optimization (Performance)

**Goal:** Avoid full re-render on every change.

1. **Implement dirty flagging:**
   ```csharp
   private bool _rulerDirty = true;
   private bool _tracksDirty = true;
   private bool _keyframesDirty = true;

   private void RenderTimeline()
   {
       if (_rulerDirty) { _rulerRenderer.Render(...); _rulerDirty = false; }
       if (_tracksDirty) { _trackLanesRenderer.Render(...); _tracksDirty = false; }
       if (_keyframesDirty) { _keyframeRenderer.Render(...); _keyframesDirty = false; }
       // Playhead always renders (cheap)
       _playheadRenderer.Render(...);
   }
   ```

2. **Mark dirty on state changes:**
   - Zoom changed → ruler, tracks, grid, keyframes dirty
   - Keyframe added/moved → keyframes dirty
   - Track added → tracks dirty, keyframes dirty

**Dependency:** Phase 2 complete (polish features working)

### Phase 4: Add Gradient Color Strips (Visual Polish)

**Goal:** Show per-track color gradient backgrounds.

1. **In TrackLanesRenderer:**
   ```csharp
   foreach (var track in viewModel.Tracks)
   {
       var avgColor = CalculateAverageColorFromKeyframes(track);
       var gradient = CreateHorizontalGradient(avgColor, opacity: 0.15);
       var rect = new Rectangle
       {
           Fill = gradient,
           Width = durationWidth,
           Height = trackHeight
       };
       Canvas.SetTop(rect, trackIndex * trackHeight);
       canvas.Children.Add(rect);
   }
   ```

**Dependency:** Phase 1 complete (TrackLanesRenderer exists)

## Rendering Performance Optimization

### Current Performance Characteristics

**Full re-render on every change:**
- User drags keyframe → RenderTimeline() called 60fps → all elements redrawn
- User changes zoom → RenderTimeline() called → all elements redrawn
- Playback running → UpdatePlayheadPosition() (optimized) updates only playhead elements

**Optimization already implemented:**
- Playhead position updates during playback use cached element references
- Light updates rate-limited to 10Hz

### Virtual Scrolling (Future Consideration)

Not needed for current scale (typical scenes: 10-20 tracks, 30-120 seconds, dozens of keyframes).

**When to implement:**
- Scenes exceed 50 tracks
- Duration exceeds 5 minutes
- Keyframes exceed 500

**Approach if needed:**
- Render only visible time range: `[scrollOffset - buffer, scrollOffset + viewportWidth + buffer]`
- Track which keyframes/elements are in viewport
- Only render visible elements
- Update visible set on scroll

**Reference:** The animation-timeline-control TypeScript project implements "area virtualization - only visible canvas area is rendered" for large timelines.

### RequestAnimationFrame for Smooth Updates

Current approach uses DispatcherTimer (16ms interval). This works but isn't frame-sync'd.

**Recommended for drag operations:**
```csharp
private void StartDragLoop()
{
    void DragFrame(object sender, object e)
    {
        if (!_isDragging) return;

        // Update dragged element position
        UpdateDraggedKeyframePosition();

        // Request next frame
        CompositionTarget.Rendering += DragFrame;
    }

    CompositionTarget.Rendering += DragFrame;
}
```

**Benefits:** Smoother drag, no tearing, frame-synchronized with display.

## Architecture Patterns from DAW Research

### Professional DAW Patterns

From research into DAW timelines (Audacity, Universal Audio, research papers):

**Time Ruler:**
- Displays time scale (bars/beats, minutes/seconds, or timecode)
- Switches ruler modes based on context
- Grid subdivisions for precise placement
- Clicking ruler seeks playhead

**Playhead:**
- Vertical line through all tracks
- Syncs across views (timeline, mini-timeline)
- Draggable from ruler or timeline
- "Current" time block styling under playhead

**Tracks:**
- Horizontal lanes for each audio/light source
- Visual separation (borders or alternating background colors)
- Track-specific controls (mute, solo, volume)

**Grid:**
- Divides timeline into regular units (beats, frames, seconds)
- Snap-to-grid for placement accuracy
- Toggle on/off, configurable intervals

### Applying to Scene Builder

**Already implemented well:**
- Time ruler with ticks and labels
- Playhead dragging and seeking
- Snap-to-grid with visual grid lines
- Track separation

**Opportunities for improvement:**
- Per-track gradient color strips (shows track color at a glance)
- Better playhead affordances (hover state, cursor change)
- Track selection highlighting (brighten selected track)
- Keyframe hover state (preview before clicking)

## Component Interaction Patterns

### Event Flow for Common Operations

**User drags keyframe:**
```
1. User clicks keyframe circle (KeyframeCanvas)
2. KeyframeLayerRenderer.OnPointerPressed
   → Check if shift held (multi-select)
   → Update ViewModel.SelectedKeyframes
   → CapturePointer
   → Subscribe to PointerMoved, PointerReleased
3. User moves pointer
4. KeyframeLayerRenderer.OnPointerMoved
   → Calculate new time from pointer X
   → Apply snap if enabled
   → Update keyframe.TimeSeconds in ViewModel
   → Re-sort track keyframes
   → Mark _keyframesDirty = true
   → Call RenderTimeline() (or just render keyframes)
5. User releases pointer
6. KeyframeLayerRenderer.OnPointerReleased
   → ReleasePointerCapture
   → Unsubscribe from events
   → Final render with selection highlight
```

**User clicks ruler to seek:**
```
1. User clicks TimeRulerCanvas
2. TimeRulerRenderer.OnPointerPressed
   → Calculate time from pointer X
   → Apply snap if enabled
   → Update ViewModel.PlayheadPosition
   → Fire PlayheadSeek event (or just update ViewModel)
3. SceneBuilderPage observes PlayheadPosition change
4. RenderTimeline() called
5. PlayheadRenderer updates playhead position
6. TimeRulerRenderer updates ruler playhead marker
```

**User hovers over keyframe:**
```
1. User moves pointer over KeyframeCanvas
2. KeyframeLayerRenderer.OnPointerMoved
   → Hit test pointer position against keyframe circles
   → Find keyframe under pointer (within radius threshold)
3. If hoveredKeyframe changed:
   → Clear previous hover (render normally)
   → Set new hover (render with highlight)
   → Only re-render affected keyframes (not full timeline)
```

### Cross-Component Communication

**Option 1: Direct ViewModel Updates (Current approach)**
- Components read from ViewModel
- Components write to ViewModel
- ViewModel fires PropertyChanged
- Page observes ViewModel changes, calls RenderTimeline()

**Option 2: Event-Based (Decoupled)**
- Components fire events (e.g., KeyframeClicked, PlayheadDragged)
- Page subscribes to component events
- Page updates ViewModel
- Page calls RenderTimeline()

**Recommendation:** Stick with Option 1 (direct ViewModel updates) for simplicity. Components are already tightly coupled to the page, not reusable controls.

## Anti-Patterns to Avoid

### Premature Custom Control Extraction

**Temptation:** Extract TimeRuler, TrackLanes, etc. as full UserControls or TemplatedControls.

**Why avoid:**
- Overhead of XAML parsing, control lifecycle
- Difficult to share state (ViewModel) across controls
- Adds complexity without clear benefit for single-use components

**When appropriate:**
- If timeline needs to be reused in multiple pages
- If timeline needs to be packaged as library

**For Scene Builder polish:** Renderer classes (not controls) are sufficient.

### Layering Too Many Canvases

**Temptation:** Create a canvas per track, per keyframe type, etc.

**Why avoid:**
- Each canvas has memory overhead
- Hit testing gets complex with many overlapping canvases
- Performance degrades with too many UIElements

**Recommended:** 4-5 canvases total (background, grid, keyframes, playhead). Render multiple logical components to same canvas where appropriate.

### Full Re-Render on Every Pointer Event

**Temptation:** Call RenderTimeline() in PointerMoved handler during drag.

**Why avoid:**
- 60+ RenderTimeline() calls per second
- Clears and recreates all canvas children
- Causes visual stutter, high CPU

**Mitigation:**
- Use cached element references for dragged elements
- Only update position properties, don't recreate elements
- Re-render only affected components (dirty flagging)

## Confidence Assessment

| Topic | Confidence | Reasoning |
|-------|------------|-----------|
| Component Separation | HIGH | Established pattern in DAW UIs, confirmed by research and existing code analysis |
| Render Layer Ordering | HIGH | WinUI Canvas.ZIndex well-documented, pattern clear from research |
| Hit Testing Strategy | MEDIUM | Layer-based approach is standard, but WinUI-specific details require experimentation |
| State Management | MEDIUM | ViewModel separation is clear, but component-local state patterns need validation |
| Performance Optimization | MEDIUM | Dirty flagging is proven, but virtual scrolling thresholds are estimates |
| Build Order | HIGH | Dependencies are clear, phased approach aligns with milestone goals |

**Overall confidence: MEDIUM-HIGH**

Research findings are well-supported by DAW architecture patterns, Win2D documentation, and analysis of existing code. Some WinUI-specific implementation details (exact hit testing behavior, performance characteristics) will require validation during implementation, but the high-level architecture is sound.

## Implementation Recommendations

### For Milestone: Polish Existing Scene Builder

**Priority 1 - Component Separation (Foundation):**
1. Create renderer classes for each logical component
2. Update XAML to use layered canvases with explicit z-index
3. Refactor RenderTimeline() to delegate to renderers
4. Verify functionality unchanged

**Priority 2 - Visual Polish (Low-Hanging Fruit):**
1. Add hover states to keyframes (brighten stroke on hover)
2. Add cursor change on playhead (resize cursor when hovering)
3. Add gradient color strips to track lanes
4. Improve playhead handle visibility (larger, drop shadow)

**Priority 3 - Incremental Rendering (Performance):**
1. Implement dirty flagging for components
2. Optimize drag operations (update positions, not full re-render)
3. Profile performance with typical scenes (20 tracks, 60s duration)

**Defer to Future:**
- Virtual scrolling (not needed for current scale)
- Custom control extraction (unnecessary complexity)
- Multi-timeline views (out of scope)

### Success Criteria

**Component separation successful when:**
- Each renderer class has single responsibility
- Adding a feature to one component doesn't require touching others
- Hit testing is predictable (clear priority order)

**Visual polish successful when:**
- Hover states provide immediate feedback
- Playhead is more discoverable and easier to grab
- Track lanes show color at a glance (gradients)

**Performance acceptable when:**
- 60fps maintained during playback with 20 tracks
- Drag operations feel smooth (no stutter)
- Zoom operations complete within 100ms

## Sources

**DAW Timeline Patterns:**
- [Universal Audio Timeline Display Documentation](https://help.uaudio.com/hc/en-us/articles/360041441712-Working-in-the-Timeline-Display)
- [Audacity Timeline Manual](https://manual.audacityteam.org/man/timeline.html)
- [Fedora DAW Common Elements Wiki](https://fedoraproject.org/wiki/User:Crantila/FSC/Recording/DAW_Common_Elements)
- [Cakewalk DAW UI Paradigms Discussion](https://discuss.cakewalk.com/topic/78676-where-did-the-standard-timelinepiano-roll-daw-ui-paradigms-come-from/)

**Canvas Rendering & Hit Testing:**
- [W3C Canvas Hit Testing](https://www.w3.org/wiki/Canvas_hit_testing)
- [Canvas Z-Index Best Practices (2025)](https://stlplaces.com/blog/how-to-set-z-index-of-canvas-elements)
- [Microsoft Learn: Canvas.ZIndex](https://learn.microsoft.com/en-us/uwp/api/windows.ui.xaml.controls.canvas.zindex?view=winrt-26100)

**Performance Optimization:**
- [Canvas Virtual Scrolling Optimization](https://dev.to/lalitkhu/rendering-massive-tables-at-lightning-speed-virtualization-with-virtual-scrolling-2dpp)
- [MDN Canvas Optimization Guide](https://developer.mozilla.org/en-US/docs/Web/API/Canvas_API/Tutorial/Optimizing_canvas)
- [HTML5 Canvas Performance Tips](https://gist.github.com/jaredwilli/5469626)

**WinUI Custom Controls:**
- [Creating Custom Controls in WinUI (2025)](https://albertakhmetov.com/posts/2025/creating-a-custom-control-in-winui/)
- [Microsoft Learn: XAML Templated Controls](https://learn.microsoft.com/en-us/windows/apps/winui/winui3/xaml-templated-controls-csharp-winui-3)
- [Win2D CanvasControl Documentation](https://microsoft.github.io/Win2D/WinUI3/html/T_Microsoft_Graphics_Canvas_UI_Xaml_CanvasControl.htm)

**Timeline Component Architectures:**
- [GitHub: animation-timeline-control](https://github.com/ievgennaida/animation-timeline-control) - TypeScript canvas-based timeline with area virtualization
- [React Timeline Editor](https://github.com/xzdarcy/react-timeline-editor) - Component-based timeline architecture
- [Remotion Timeline Builder](https://www.remotion.dev/docs/building-a-timeline) - Track-based timeline architecture

---

*Research completed: 2026-01-20*
*Target: Scene Builder polish milestone*
