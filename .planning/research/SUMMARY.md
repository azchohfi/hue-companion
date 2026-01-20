# Project Research Summary

**Project:** Hue Companion Scene Builder Polish
**Domain:** DAW-style timeline editor for animated light scenes
**Researched:** 2026-01-20
**Confidence:** HIGH

## Executive Summary

The Scene Builder currently uses XAML Canvas with shape primitives for timeline rendering, which is fundamentally incompatible with achieving 60fps DAW-style polish. Professional timeline editors (Logic Pro, Ableton Live) require continuous visual feedback through gradient color strips, component-based rendering for hover states, and GPU-accelerated drawing to maintain smooth performance. The current monolithic `RenderTimeline()` approach recreates all canvas children on every update, creating a performance ceiling around 30fps with 100+ elements.

**Recommended approach:** Migrate timeline rendering to Win2D CanvasControl (already in project dependencies) while keeping XAML for UI chrome. Win2D provides GPU-accelerated Direct2D rendering with proper caching and invalidation, enabling gradient color strips between keyframes and 60fps performance. The migration is straightforward (8-16 hours estimated) because the component architecture is already logically separated in the codebase - it just needs physical separation into layered canvases with independent renderers.

**Key risk:** Touch target accessibility violations. The playhead handle (12px) and keyframes (16px circles) fall below WCAG 2.5.5 standards (44x44px minimum). This blocks users with motor impairments and makes touch interaction frustrating. Mitigation: expand invisible hit areas to 44px while keeping visual size small, add hover affordances to show expanded targets.

## Key Findings

### Recommended Stack

**Migrate from XAML Canvas to Win2D for timeline rendering.** The current approach of clearing and recreating XAML shape primitives (`Children.Clear()` followed by adding Lines, Rectangles, Ellipses) cannot achieve 60fps with the continuous gradient color strips required for professional DAW aesthetics. Win2D is already installed (Microsoft.Graphics.Win2D 1.3.0) and provides GPU-accelerated Direct2D rendering with geometry realizations and brush reuse patterns.

**Core technologies:**
- **Win2D CanvasControl**: GPU-accelerated timeline layer — enables gradient color strips, 60fps performance, geometry caching
- **CanvasLinearGradientBrush**: Track color visualization — creates Logic Pro-style gradient fills showing color evolution between keyframes
- **CanvasGeometryRealization**: Repeated shape optimization — caches rounded rectangle tessellations for 184-438% rendering improvement
- **Hybrid XAML + Win2D**: XAML for UI chrome, Win2D for timeline — keeps TextBox/ComboBox/Slider accessibility while optimizing drawing performance

**Migration path:** Extract rendering to component classes (TimeRulerRenderer, TrackLanesRenderer, KeyframeLayerRenderer, PlayheadRenderer), replace XAML Canvas with layered Win2D CanvasControl instances, implement resource creation/reuse pattern. Estimated effort: 8-16 hours across three phases (proof-of-concept, full migration, polish).

### Expected Features

Scene Builder already implements most table-stakes timeline features (playhead scrubbing, space-to-play, snap-to-grid, undo/redo, zoom, multi-select). The polish pass focuses on closing three critical gaps that prevent "professional DAW feel."

**Must have (table stakes already implemented):**
- Playhead scrubbing, space bar play/pause, zoom controls — standard muscle memory
- Snap to grid with Ctrl bypass — prevents misaligned keyframes
- Undo/redo with TimelineCommandHistory — safety net for experimentation
- Keyframe selection, dragging, copy/paste — core editing workflow
- Time ruler, track separators, visual loop region — orientation and structure

**Critical missing table stakes:**
- **Continuous automation display** — users expect to SEE color gradient between keyframes, not just dots. Currently only shows keyframe circles.
- **Track height adjustment** — standard in all DAWs for focus/overview. Fixed 50px feels limiting.
- **Loop region handles** — draggable start/end markers instead of fixed 0-to-duration.

**Should have (differentiators - high impact):**
- **Color gradient automation curves** — visualize color transitions between keyframes with gradient fills (unique to light scenes, requires Win2D)
- **Track color coding** — tint track backgrounds to light's color at low opacity for at-a-glance identification
- **Zoom presets** — Z/X keyboard shortcuts for fit-selection/previous-zoom
- **Scrub area polish** — better affordances above ruler for click-to-seek

**Defer (v2+):**
- Marquee selection tool — nice-to-have for multi-select but Shift+click works
- Minimap overview — useful only for very long scenes (5+ minutes)
- Velocity/easing handles — advanced feature, TransitionStyle enum already functional

### Architecture Approach

**Separate timeline into component layers with clear z-order and hit testing boundaries.** The current monolithic `RenderTimeline()` method renders all elements (ruler, grid, tracks, keyframes, playhead) to a single canvas in one pass. This works but prevents adding hover states, per-component optimizations, and gradient color strips without entangling logic.

**Recommended layer structure:**
1. **TrackBackgroundCanvas (Z=1)** — track lane backgrounds with gradient color strips showing color evolution, track separators, event track backgrounds
2. **GridCanvas (Z=2)** — snap grid lines, loop region highlight (non-interactive, `IsHitTestVisible=false`)
3. **KeyframeCanvas (Z=3)** — keyframe circles with selection highlights, event track patterns, temporary pulse animations
4. **PlayheadCanvas (Z=4)** — playhead line, draggable handle, wide invisible scrub hit area

**Major components:**
1. **TimeRulerRenderer** — time scale ticks, labels, snap markers on dedicated TimeRulerCanvas; click to seek playhead
2. **TrackLanesRenderer** — gradient color strips per track, separators, event backgrounds; enables hover brightening and color-coded organization
3. **KeyframeLayerRenderer** — keyframe circles with hover/selection states, drag-to-move, right-click context menus; manages component-local transient state (hoveredKeyframe, isDragging)
4. **PlayheadRenderer** — playhead line with handle and 44px hit area; scrubbing affordances with cursor changes

**Component separation benefits:** Each renderer can invalidate independently (dirty flagging), hover states don't require full re-render, gradient strips can be added to TrackLanes without touching keyframe logic, clear hit testing priority (playhead > keyframes > tracks > ruler).

### Critical Pitfalls

Research identified 17 pitfalls across performance, accessibility, visual, and interaction categories. Top 5 by severity and phase relevance:

1. **Full Canvas Redraw on Every Update** — calling `Canvas.Children.Clear()` and recreating all elements kills 60fps. Current code does this in `RenderTimeline()` on zoom/keyframe edits. Prevention: dirty region tracking, layer separation, optimized playhead updates (partially implemented for playback). Address in polish phase with Win2D migration.

2. **Touch Targets Too Small (Accessibility)** — playhead handle (12px) and keyframes (16px) violate WCAG 2.5.5 (44x44px minimum). Blocks users with motor impairments, frustrating on touch devices. Prevention: expand invisible hit areas to 44px, add hover affordances showing target size. Address immediately in polish phase.

3. **Ruler Alignment Breaks on Window Resize** — ruler uses dynamic width but doesn't listen for window resize events. Labels become misaligned with grid lines. Prevention: add window resize handler triggering `RenderTimeRuler()`. Fix immediately (low-hanging fruit).

4. **Live Preview on Every Keyframe Edit** — changing color/brightness triggers immediate bridge updates, flooding API during slider drag (30+ commands/sec). Prevention: debounce input (150-300ms), visual-only preview until drag release. Fix in polish phase.

5. **No Visual Feedback on Hover** — no hover states on keyframes, playhead, or interactive elements. Users don't know what's clickable/draggable. Prevention: `PointerEntered`/`PointerExited` handlers, visual highlights, cursor changes, tooltips. Requires component-local state. Add in polish phase after layer separation.

**Secondary pitfalls** (address in polish phase): snap grid not visible until interaction (toggle visibility with snap state), modifier keys not discoverable (add keyboard shortcuts panel), color picker obscures timeline (reposition or make narrower), scroll position lost on timeline rebuild (save/restore offset).

## Implications for Roadmap

Based on research, the polish pass should be structured as three incremental phases building on existing functionality. All table-stakes features are implemented; polish focuses on performance, accessibility, and visual feedback.

### Phase 1: Foundation - Win2D Migration & Component Separation

**Rationale:** Win2D migration is a prerequisite for gradient color strips (major differentiator) and enables all subsequent polish features (hover states, layer-specific rendering). Blocking work that unblocks everything else.

**Delivers:**
- Timeline rendering on Win2D CanvasControl with GPU acceleration
- Component renderer classes (TimeRulerRenderer, TrackLanesRenderer, KeyframeLayerRenderer, PlayheadRenderer)
- Layered canvas architecture (4 canvases with explicit z-order)
- 60fps performance baseline verified with profiling

**Addresses (from FEATURES.md):**
- Foundation for continuous automation display (gradient curves)
- Enables hover state infrastructure
- Performance optimization for large scenes

**Avoids (from PITFALLS.md):**
- Full canvas redraw on every update (Win2D invalidation pattern)
- Ruler alignment breaks (component-specific re-render)

**Research flag:** SKIP RESEARCH - well-documented Win2D patterns, proof-of-concept validates approach.

**Estimated effort:** 8-16 hours (proof-of-concept 2-4h, full migration 4-8h, polish 2-4h)

---

### Phase 2: Visual Polish - Gradients, Hover States, Accessibility

**Rationale:** With Win2D foundation in place, add the visual polish features that create "professional DAW feel." Gradient color strips are the signature differentiator for light animation tools. Hover states and touch targets address accessibility gaps.

**Delivers:**
- Gradient color strips showing color evolution between keyframes (Logic Pro aesthetic)
- Hover states on all interactive elements (keyframes brighten, playhead shows resize cursor)
- Touch target expansion (44x44px playhead hit area, keyframe padding)
- Track color coding (background tints at low opacity)
- Grid visibility tied to snap state (only show when snap enabled)

**Addresses (from FEATURES.md):**
- Continuous automation display (CRITICAL MISSING TABLE STAKES)
- Color gradient automation curves (HIGH-IMPACT DIFFERENTIATOR)
- Track color coding (differentiator)
- Scrub area polish (differentiator)

**Avoids (from PITFALLS.md):**
- Touch targets too small (expand to 44px with hover affordances)
- No visual feedback on hover (component-local hover state)
- Snap grid not visible until interaction (toggle with snap state)

**Research flag:** SKIP RESEARCH - gradient rendering patterns documented in Win2D, hover state is standard UX pattern.

**Estimated effort:** 6-10 hours (gradients 3-4h, hover states 2-3h, accessibility 1-2h, color coding 1h)

---

### Phase 3: Interaction Refinement - Debouncing, Keyboard Shortcuts, Polish

**Rationale:** Address remaining UX papercuts and performance optimizations. These are lower priority than visual polish but complete the professional experience.

**Delivers:**
- Debounced color picker and brightness slider (150ms delay before bridge update)
- Window resize handler for ruler alignment
- Scroll position preservation on timeline rebuild
- Keyboard shortcuts documentation panel (? key shows overlay)
- Modifier key visual feedback (show "SNAP OFF" when Ctrl held)
- Event track frequency labels with actual time ranges

**Addresses (from FEATURES.md):**
- Keyboard shortcuts overlay (differentiator)
- Better snap affordances

**Avoids (from PITFALLS.md):**
- Live preview on every keyframe edit (debouncing)
- Ruler alignment breaks on resize (window handler)
- Scroll position lost on rebuild (save/restore)
- Modifier keys not discoverable (documentation panel)

**Research flag:** SKIP RESEARCH - standard interaction patterns, implementation details known.

**Estimated effort:** 4-6 hours (debouncing 1h, resize handler 0.5h, scroll preservation 1h, keyboard panel 1-2h, event labels 0.5h)

---

### Phase Ordering Rationale

- **Phase 1 first** because Win2D migration is a prerequisite for gradient color strips (can't achieve with XAML shapes) and enables efficient hover state rendering (component-local invalidation). Blocking work.

- **Phase 2 second** because it delivers the highest-impact differentiators (gradient curves, track color coding) and closes critical accessibility gaps (touch targets). These are the features that create "professional DAW feel."

- **Phase 3 last** because it addresses polish details that improve but don't define the experience. Debouncing and window resize are important but not user-facing differentiators.

**Dependency chain:** Phase 1 (Win2D + component separation) → Phase 2 (gradients require Win2D, hover requires components) → Phase 3 (refinement on top of working system).

**Deferred features:** Marquee selection, minimap overview, velocity handles, track height adjustment, loop region handles. Not essential for "professional feel" - can be added in v2 if usage data shows demand.

### Research Flags

All three phases can proceed without additional research:

**Phases with standard patterns (skip research-phase):**
- **Phase 1:** Win2D migration well-documented with official Microsoft docs, CanvasControl tutorials, proven patterns
- **Phase 2:** Gradient rendering and hover states are standard UI patterns with established implementations
- **Phase 3:** Debouncing, keyboard shortcuts, scroll preservation are standard interaction patterns

**High confidence:** Stack research (HIGH), features research (HIGH), architecture research (MEDIUM-HIGH), pitfalls research (MEDIUM-HIGH for performance/accessibility, MEDIUM for DAW-specific).

**No blocking unknowns.** Implementation can proceed directly from research findings.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | Win2D already in project, official Microsoft docs, multiple tutorials, proven Direct2D wrapper |
| Features | HIGH | Consistent patterns across Ableton/Logic/Pro Tools, current Scene Builder analyzed, table-stakes features identified |
| Architecture | MEDIUM-HIGH | Component separation well-documented in DAW research, Win2D layer patterns established, some WinUI specifics need validation |
| Pitfalls | MEDIUM-HIGH | Performance/accessibility from authoritative sources (WCAG, MDN, web.dev), DAW-specific from codebase analysis and general timeline research |

**Overall confidence:** HIGH

Research findings are well-supported by authoritative sources (Microsoft Win2D docs, WCAG standards, official DAW documentation, performance optimization guides). The current Scene Builder codebase was analyzed to identify existing patterns and gaps. Recommendations are concrete and implementable.

### Gaps to Address

Minor implementation details to validate during Phase 1 (not blocking):

- **Exact Win2D rendering performance on high-DPI displays** — research indicates DpiScale tuning may be needed on 4K displays. Validate with profiling, cap DPI to 96 if fill-rate bound.

- **WinUI hit testing behavior with layered CanvasControl instances** — layer-based hit testing strategy is sound (Z-order priority), but exact pointer event routing needs validation during implementation. Fall back to Tag-based approach if layering doesn't work as expected.

- **Gradient stop calculation for sparse keyframes** — research shows CanvasLinearGradientBrush requires normalized positions (0.0-1.0). Need to handle edge cases (single keyframe, keyframes at start/end only) during Phase 2 implementation.

**Mitigation:** All gaps are implementation details that can be resolved during coding. No conceptual unknowns that would block progress or require architecture changes.

## Sources

### Primary (HIGH confidence)

**Win2D & Performance:**
- [CanvasControl Class - Win2D WinUI3 Docs](https://microsoft.github.io/Win2D/WinUI3/html/T_Microsoft_Graphics_Canvas_UI_Xaml_CanvasControl.htm)
- [CanvasLinearGradientBrush Class - Win2D Docs](https://microsoft.github.io/Win2D/WinUI2/html/T_Microsoft_Graphics_Canvas_Brushes_CanvasLinearGradientBrush.htm)
- [Improving Direct2D Performance - Microsoft Learn](https://learn.microsoft.com/en-us/windows/win32/direct2d/improving-direct2d-performance)
- [DPI and DIPs - Win2D WinUI3](https://microsoft.github.io/Win2D/WinUI3/html/DPI.htm)
- [Optimizing canvas - MDN Web Docs](https://developer.mozilla.org/en-us/docs/Web/API/Canvas_API/Tutorial/Optimizing_canvas)

**DAW Features & Patterns:**
- [Arrangement View — Ableton Reference Manual Version 12](https://www.ableton.com/en/manual/arrangement-view/)
- [Key commands for Views Showing Time Ruler in Logic Pro for Mac](https://support.apple.com/guide/logicpro/views-showing-time-ruler-lgcp267f63a1/mac)
- [Universal Audio Timeline Display Documentation](https://help.uaudio.com/hc/en-us/articles/360041441712-Working-in-the-Timeline-Display)
- [Audacity Timeline Manual](https://manual.audacityteam.org/man/timeline.html)

**Accessibility:**
- [WCAG 2.5.5: Target Size - W3C](https://www.w3.org/WAI/WCAG21/Understanding/target-size.html)
- [Looking at WCAG 2.5.5 for Better Target Sizes - CSS-Tricks](https://css-tricks.com/looking-at-wcag-2-5-5-for-better-target-sizes/)
- [Touch Targets on Touchscreens - Nielsen Norman Group](https://www.nngroup.com/articles/touch-target-size/)

### Secondary (MEDIUM confidence)

**Performance Optimization:**
- [HTML5 Canvas Performance Tips - GitHub Gist](https://gist.github.com/jaredwilli/5469626)
- [Optimising HTML5 Canvas Rendering - AG Grid](https://blog.ag-grid.com/optimising-html5-canvas-rendering-best-practices-and-techniques/)
- [Canvas Layering Optimization - IBM Developer](https://developer.ibm.com/tutorials/wa-canvashtml5layering/)

**Timeline Architecture:**
- [GitHub: animation-timeline-control](https://github.com/ievgennaida/animation-timeline-control) - TypeScript canvas-based timeline with area virtualization
- [W3C Canvas Hit Testing](https://www.w3.org/wiki/Canvas_hit_testing)
- [Canvas Z-Index Best Practices (2025)](https://stlplaces.com/blog/how-to-set-z-index-of-canvas-elements)

**UX Patterns:**
- [6 Reasons to Use the Logic Marquee Tool](https://whylogicprorules.com/marquee-tool/)
- [Mastering Advanced DAW Automation Curves for Engineers](https://www.departuremusic.com/mastering-advanced-daw-automation-curves/)
- [Timeline Navigators: User Interface Design Patterns for Time](https://journals.sagepub.com/doi/10.1177/21695067231192451)

### Tertiary (LOW confidence, informational)

- [DaVinci Resolve Render Caching - Creative Video Tips](https://creativevideotips.com/tutorials/davinci-resolve-render-cache-essentials) - Timeline caching patterns
- [Logic vs. Ableton: DAW Comparison - LANDR Blog](https://blog.landr.com/logic-vs-ableton/) - General DAW comparison

---
*Research completed: 2026-01-20*
*Ready for roadmap: yes*
