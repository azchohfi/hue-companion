# Features Research: DAW Timeline UI

**Domain:** Timeline editor for animated light scenes (DAW-style interface)
**Researched:** 2026-01-20
**Target aesthetic:** Ableton Live (minimal transport) + Logic Pro (polished track styling)

## Executive Summary

Professional DAW timeline interfaces share a consistent set of table-stakes features that users expect from muscle memory across applications. The Scene Builder already implements many core features (keyframe editing, snap-to-grid, playhead scrubbing, undo/redo, zoom). Polishing to "professional DAW feel" requires attention to: continuous visual feedback (gradient tracks showing color evolution), refined playhead affordances (scrub area, better visual feedback), track management polish (height adjustment, color coding), and keyboard-first workflow optimizations.

**Current state analysis:** Scene Builder has solid foundation with keyframes, tracks, event tracks, snap-to-grid with Ctrl bypass, space-to-play, undo/redo (Ctrl+Z/Y), zoom (Ctrl +/-), and multi-select (Shift+click). Missing DAW polish includes: continuous automation curves between keyframes, dedicated scrub area above ruler, loop region markers, track height adjustment, and visual density improvements.

---

## Table Stakes

Features users expect from any timeline editor. Missing these makes the interface feel incomplete or amateur.

| Feature | Why Expected | Complexity | Current Status | Notes |
|---------|--------------|------------|----------------|-------|
| **Playhead scrubbing** | Standard in all DAWs for precise navigation | Low | ✅ Implemented | Playhead is draggable with 12px hit area. Works well. |
| **Dedicated scrub area** | Click-anywhere-to-play without hitting timeline elements | Low | ⚠️ Partial | TimeRulerCanvas has click-to-jump. Needs dedicated scrub strip above ruler for better affordance. |
| **Space bar play/pause** | Universal muscle memory across all DAWs | Low | ✅ Implemented | Space toggles play/pause. Standard behavior. |
| **Zoom controls** | Essential for navigating timelines of any length | Medium | ✅ Implemented | Ctrl+wheel, Ctrl+/- keyboard shortcuts, slider. Zoom-to-cursor implemented. |
| **Snap to grid** | Prevents misaligned keyframes, critical for timing | Low | ✅ Implemented | Toggle with Ctrl-bypass. Adaptive intervals (2s→1s→0.5s→0.25s). |
| **Undo/redo** | Safety net for experimentation | Medium | ✅ Implemented | Ctrl+Z/Ctrl+Y with TimelineCommandHistory. Non-destructive. |
| **Visual loop region** | Shows what will repeat during playback | Low | ✅ Implemented | Blue tinted background + boundary line at duration end. |
| **Time ruler with labels** | Orientation for timeline position | Low | ✅ Implemented | Tick marks every 1s, labels every 5s. Standard pattern. |
| **Track separators** | Visual distinction between tracks | Low | ✅ Implemented | Horizontal lines between tracks. Clear separation. |
| **Keyframe selection** | Click to select, multi-select with modifiers | Medium | ✅ Implemented | Click selects, Shift+click multi-selects. Visual highlight. |
| **Keyframe dragging** | Move keyframes along timeline | Medium | ✅ Implemented | Drag with pointer, respects snap unless Ctrl held. |
| **Delete keyframes** | Remove unwanted keyframes | Low | ✅ Implemented | Right-click or Delete/Backspace key. |
| **Copy/paste keyframes** | Duplicate timing patterns | Medium | ✅ Implemented | Ctrl+C/V with clipboard storage. Preserves track association. |
| **Time display** | Current position vs total duration | Low | ✅ Implemented | mm:ss.ff format (minutes:seconds:centiseconds). |
| **Selection highlight** | Visual feedback for selected elements | Low | ✅ Implemented | Larger circles with colored stroke for selected keyframes. |
| **Grid lines** | Visual alignment aid when snap enabled | Low | ✅ Implemented | Vertical lines at snap intervals extending beyond duration. |

### Critical Missing Table Stakes

| Feature | Why Expected | Complexity | Priority | Notes |
|---------|--------------|------------|----------|-------|
| **Continuous automation display** | Users expect to SEE color gradient between keyframes, not just dots | Medium | HIGH | Currently only shows keyframe circles. Need gradient path connecting them showing color evolution. |
| **Track height adjustment** | Standard in all DAWs for focus or overview | Medium | MEDIUM | Fixed 50px track height. Users expect drag-to-resize or presets (Small/Medium/Large). |
| **Loop region handles** | Draggable start/end markers for loop boundaries | Low | MEDIUM | Currently loop is fixed 0→duration. Users expect draggable loop brace. |
| **Playhead position indicator on ruler** | Triangle/marker showing exact playhead position | Low | LOW | Implemented but could be more prominent. Currently small triangle. |

---

## Differentiators

Polish features that elevate from "functional" to "professional." Not expected but significantly improve experience.

| Feature | Value Proposition | Complexity | Current Status | Notes |
|---------|-------------------|------------|----------------|-------|
| **Color gradient automation curves** | Visualize color transitions between keyframes (unique to light scenes) | Medium | ❌ Missing | Draw bezier/linear paths with gradient fills showing color evolution. Major differentiator for light animation tool. |
| **Scrub area audio feedback** | Visual ripple when scrubbing (audio DAWs play audio, we could pulse lights) | Low | ❌ Missing | Brief pulse to affected lights when scrubbing playhead could help users "feel" the timeline. |
| **Zoom presets** | Fit-to-window, fit-selection, 1:1 scale quick access | Low | ⚠️ Partial | Manual zoom works. Missing keyboard shortcuts like Z (zoom selection), X (previous zoom). |
| **Marquee selection tool** | Drag rectangle to select multiple keyframes | Medium | ❌ Missing | Standard in Logic Pro. Hold M or Command to enable marquee drag for area selection. |
| **Track color coding** | Assign colors to tracks for visual organization | Low | ⚠️ Partial | Keyframes show light color. Track background could be tinted to light's color at low opacity. |
| **Minimap/overview** | Bird's-eye view of entire timeline with viewport indicator | Medium | ❌ Missing | Like Ableton's overview. Shows all tracks compressed with highlighted viewport box. |
| **Smart snap** | Snap to nearby keyframes, not just grid | Medium | ❌ Missing | When dragging near another keyframe, snap to its time even if grid is off. |
| **Keyframe velocity handles** | Bezier curve handles to control transition easing | High | ❌ Missing | TransitionStyle enum exists (Linear, EaseIn, EaseOut, EaseInOut) but no visual handles for custom curves. |
| **Event track visualization** | Show event triggers on timeline as they occur | Low | ✅ Implemented | Pulse animations when events fire. Good visual feedback. |
| **Multi-track editing** | Edit multiple tracks simultaneously | Medium | ⚠️ Partial | Can add keyframes to all tracks at once via button. Missing linked-selection for batch edits. |
| **Keyboard shortcuts overlay** | ? key shows shortcut cheatsheet | Low | ❌ Missing | Teaching tip or overlay showing all shortcuts. Common in pro tools. |
| **Non-destructive live preview** | Scrub shows live light updates without affecting saved state | Medium | ✅ Implemented | Live preview on selection, rate-limited updates during playback. |
| **Transport position memory** | Return to last playhead position on play | Low | ⚠️ Partial | Space bar always resumes from current position. Missing Shift+Space pattern (start from stopped position). |

### Recommended Differentiator Priorities

**Phase 1 (High impact, low complexity):**
1. Color gradient automation curves between keyframes
2. Track color coding with light color tints
3. Zoom presets (Z/X keyboard shortcuts)
4. Scrub area polish with better affordances

**Phase 2 (Medium impact):**
5. Marquee selection tool for multi-select
6. Track height adjustment
7. Loop region draggable handles
8. Smart snap to nearby keyframes

**Phase 3 (Polish):**
9. Keyboard shortcuts overlay
10. Minimap overview
11. Velocity/easing handles for custom curves

---

## Anti-Features

Common mistakes that hurt DAW timeline usability. Avoid these patterns.

| Anti-Feature | Why Avoid | What to Do Instead |
|--------------|-----------|-------------------|
| **Modal editing modes** | Requires mental state tracking (am I in select mode? draw mode?) | Use modifier keys for temporary modes. Ctrl = bypass snap, Shift = multi-select, Command = marquee. Default mode does the right thing. |
| **Invisible hit areas** | Frustrating when you can't click what you see | Make all interactive elements visually obvious or provide wider hit areas with cursor feedback. Playhead handle is good example (12px hit area). |
| **Destructive operations without undo** | Users won't experiment if they fear losing work | ALL edits go through undo system. Already implemented well. |
| **Inconsistent snap behavior** | Snap works differently for different operations | Snap should behave identically for keyframe creation, dragging, playhead, etc. Ctrl bypass should be universal. Currently good. |
| **Cluttered transport controls** | Too many buttons (record, metronome, MIDI, etc.) | Minimal transport: Play/Pause, Stop, Loop toggle. Scene Builder is good here. |
| **Fixed zoom origin** | Zooming always from timeline start is disorienting | Zoom to cursor position. Already implemented via Ctrl+Wheel. Keep this. |
| **Magnetic/auto-scroll during playback** | Jarring when viewport jumps to follow playhead | Let playhead leave viewport during playback unless user enables "follow" mode. Current behavior is OK (no auto-scroll). |
| **Tiny keyframe targets** | Hard to select/drag small dots | Current 16px circles (20px when selected) are good size. Don't make smaller. |
| **Buried edit operations** | Common actions require multiple clicks or menu diving | Keep common operations as direct manipulation (drag, right-click) or single-key (Space, Delete). Currently good. |
| **Inconsistent selection feedback** | Selected items don't look clearly different | Current implementation good: larger size, colored stroke, higher z-index. |
| **Auto-save without user control** | Overwrites intentional versions or causes confusion | Explicit Save button. Unsaved changes warning on exit. Good pattern. |
| **Non-obvious keyboard shortcuts** | Custom shortcuts that conflict with OS/app standards | Stick to DAW conventions: Space=play, Ctrl+Z=undo, Delete=delete, etc. Current shortcuts are conventional. |
| **Precision-only workflow** | Forcing pixel-perfect positioning for every operation | Snap-by-default with easy bypass (Ctrl). Smart defaults. Good balance. |

### Current Anti-Pattern Risks

**Moderate risk:**
- **No track height control** could feel limiting as projects grow. Users may want to collapse unimportant tracks.
- **Only circle keyframes** without interpolation curves makes color evolution invisible until playback.
- **Fixed track order** - no drag-to-reorder tracks yet.

**Low risk:**
- Event tracks are conceptually unfamiliar (not standard in DAWs), but visual design makes them discoverable.

---

## Reference DAW Patterns

Specific patterns from professional DAWs to emulate.

### Ableton Live - Minimal Transport

**What works:**
- Clean, icon-based transport controls (Play, Stop, Record)
- Arrangement View scrolls horizontally with minimal chrome
- Dual-ruler system (bars/beats + absolute time)
- Session mixer view integrates into Arrangement View (Live 12)
- Clip-based workflow with non-destructive looping

**Apply to Scene Builder:**
- ✅ Minimal transport (Play/Pause, Stop, no record needed)
- ✅ Horizontal scroll without vertical clutter
- ⚠️ Could add secondary ruler for minutes:seconds vs seconds-only
- ❌ Event tracks are Scene Builder's unique element (not in Ableton)

### Logic Pro - Polished Track Styling

**What works:**
- Track height presets: Reduced, Small, Medium, Large
- Rich color coding with consistent color scheme options
- Automation lanes toggle independently per track
- Marquee tool (Command-drag) for precise selection
- Time ruler with musical + absolute time
- Smart Tempo for adaptive timing

**Apply to Scene Builder:**
- ❌ Need track height adjustment (presets or drag-resize)
- ⚠️ Track color coding exists (keyframe colors) but track backgrounds could be tinted
- ⚠️ Automation display exists (keyframes) but needs continuous curves
- ❌ Marquee selection missing (would be useful for selecting time ranges)
- ✅ Time ruler works well (absolute time in seconds)

### Pro Tools - Edit & Timeline Selections

**What works:**
- Distinction between Edit Selection (what's affected) and Timeline Selection (what plays)
- Scrub area above timeline for click-to-play
- Zoom Toggle (Z key) for quick in/out
- Loop Recording over designated section
- Track height controls (Reduce/Small/Medium/Large/Jumbo)

**Apply to Scene Builder:**
- ❌ Edit vs Timeline selection not needed (simpler model works)
- ⚠️ Scrub area exists (TimeRulerCanvas) but could be more obvious
- ❌ Zoom Toggle (Z key) missing - would be useful
- ✅ Loop playback exists with visual region
- ❌ Track height controls needed

### Common Patterns Across DAWs

| Pattern | Ableton | Logic | Pro Tools | Scene Builder Status |
|---------|---------|-------|-----------|---------------------|
| Space = Play/Pause | ✓ | ✓ | ✓ | ✅ Implemented |
| Ctrl+Wheel = Zoom | ✓ | ✓ | ✓ | ✅ Implemented |
| Ctrl+Z/Y = Undo/Redo | ✓ | ✓ | ✓ | ✅ Implemented |
| Delete = Remove | ✓ | ✓ | ✓ | ✅ Implemented |
| Ctrl+C/V = Copy/Paste | ✓ | ✓ | ✓ | ✅ Implemented |
| Shift+Click = Multi-select | ✓ | ✓ | ✓ | ✅ Implemented |
| Snap with bypass | ✓ (toggle) | ✓ (N key) | ✓ (Cmd) | ✅ Implemented (Ctrl bypass) |
| Zoom to cursor | ✓ | ✓ | - | ✅ Implemented |
| Track height adjust | ✓ | ✓ | ✓ | ❌ Missing |
| Color coding | ✓ | ✓ | ✓ | ⚠️ Partial |
| Automation curves | ✓ | ✓ | ✓ | ❌ Missing (only keyframes) |
| Loop region | ✓ | ✓ | ✓ | ⚠️ Fixed (needs handles) |
| Scrub area | - | ✓ | ✓ | ⚠️ Needs polish |
| Marquee selection | - | ✓ (Cmd) | - | ❌ Missing |
| Keyboard shortcut overlay | - | - | - | ❌ Missing |

---

## Scene Builder Unique Requirements

Features specific to light animation that don't map directly to audio DAWs.

### Color-Specific Features

**Color gradient automation:**
- Between keyframes, show continuous gradient path representing color interpolation
- Use bezier curves or linear segments based on TransitionStyle
- Brightness affects gradient opacity/luminance
- Critical for understanding what lights will do before playback

**Live light preview:**
- ✅ Already implemented (updates lights during scrubbing/editing)
- Rate-limited to 10Hz to avoid flooding bridge
- Non-destructive (doesn't affect saved scene)
- Major advantage over audio DAWs (can't "hear" automation curves without playing)

### Event Tracks (Randomized Effects)

**Current implementation:**
- ✅ Event tracks with presets (Lightning, Sparkle, Candle)
- ✅ Frequency slider controlling trigger interval
- ✅ Visual pulses showing event fires
- ✅ Random target light selection

**Unique UX considerations:**
- Dashed pattern visually distinct from keyframe tracks
- No interpolation (events are discrete triggers)
- Preview mode shows random fires without affecting actual lights
- Consider adding: manual trigger button for testing

### Multi-Light Coordination

**Current approach:**
- One track per light in room
- Keyframes can be added across all tracks at playhead position
- Each light maintains independent color/brightness timeline

**Missing coordination features:**
- Link/unlink tracks for synchronized editing
- "Apply to all tracks" for keyframe edits
- Track grouping/folding (collapse similar lights)
- Color palette consistency (ensure multiple lights use harmonious colors)

---

## Feature Dependencies

How features relate to each other and build on prerequisites.

### Dependency Graph

```
Core Timeline (✅ Implemented)
├── Playhead & Scrubbing
│   ├── → Dedicated scrub area polish
│   └── → Playhead position indicator
├── Zoom System
│   ├── → Zoom presets (Z/X shortcuts)
│   └── → Minimap overview
├── Snap System
│   └── → Smart snap to keyframes
├── Undo/Redo
│   └── → All editing operations
└── Time Ruler
    └── → Secondary ruler (musical time)

Track Management (⚠️ Partial)
├── Track Height (MISSING)
│   ├── → Presets (Small/Medium/Large)
│   ├── → Drag-to-resize
│   └── → Fit-to-window
├── Track Colors (PARTIAL)
│   └── → Background tint from light color
└── Track Reordering (MISSING)

Keyframe System (✅ Implemented)
├── Basic CRUD
│   ├── → Continuous automation curves (MISSING)
│   │   └── → Color gradient display
│   └── → Velocity handles (MISSING)
├── Selection
│   ├── → Multi-select (✅)
│   └── → Marquee selection (MISSING)
└── Copy/Paste (✅)

Loop System (⚠️ Partial)
├── Visual loop region (✅)
└── Draggable loop handles (MISSING)

Event Tracks (✅ Implemented)
├── Preset patterns (✅)
├── Frequency control (✅)
└── Visual feedback (✅)
```

### Implementation Order for Missing Features

**Iteration 1: Visual Feedback (High impact)**
1. Continuous color gradient curves between keyframes
2. Track background color tinting
3. Scrub area polish

**Iteration 2: Track Management**
4. Track height adjustment (presets)
5. Loop region draggable handles
6. Track drag-to-reorder

**Iteration 3: Selection & Navigation**
7. Marquee selection tool
8. Zoom presets (Z/X keys)
9. Smart snap to keyframes

**Iteration 4: Advanced Polish**
10. Keyboard shortcuts overlay
11. Minimap overview
12. Velocity/easing handles

---

## MVP Recommendation

**Current Scene Builder is already MVP-viable.** It has all table-stakes features for a functional timeline editor.

### What's Good (Keep)

- ✅ Core timeline editing (keyframes, tracks, playhead)
- ✅ Keyboard shortcuts following DAW conventions
- ✅ Undo/redo with non-destructive editing
- ✅ Multi-select and copy/paste
- ✅ Live light preview
- ✅ Event tracks for randomized effects
- ✅ Zoom-to-cursor behavior
- ✅ Snap with Ctrl bypass

### Critical Gaps (Address First)

**Before "professional DAW feel" can be claimed:**
1. **Continuous automation curves** - Currently only dots, not curves showing color evolution
2. **Track height adjustment** - Fixed height feels limiting
3. **Scrub area affordances** - Needs visual distinction from ruler

### Post-MVP (Defer)

- Marquee selection (nice-to-have, not critical)
- Minimap overview (useful for long scenes only)
- Velocity handles (advanced feature)
- Secondary time ruler (added complexity)

---

## Confidence Assessment

**Overall confidence: HIGH**

### Research Quality by Area

| Area | Confidence | Sources | Notes |
|------|------------|---------|-------|
| DAW timeline patterns | HIGH | Ableton/Logic/Pro Tools official docs, multiple DAW comparison articles | Consistent patterns across all major DAWs |
| Automation visualization | HIGH | DAW automation documentation, visual feedback research | Standard practice well-documented |
| Keyboard shortcuts | HIGH | Official keyboard shortcut documentation | Industry conventions clear |
| Zoom controls | HIGH | DAW navigation guides | Universal patterns |
| Track management | HIGH | Official track height/color documentation | Common across DAWs |
| Scrub area design | MEDIUM | Pro Tools and Logic documentation | Less standardized than other features |
| Event tracks (unique) | MEDIUM | No direct DAW equivalent | Novel feature, based on Scene Builder's current implementation |
| Color gradient display | MEDIUM | Web search + logic inference | Unique to light animation, no direct DAW parallel |

### Knowledge Gaps

**Minor gaps (don't affect recommendations):**
- Exact bezier curve implementations for color interpolation (implementation detail)
- Specific gradient rendering performance in WinUI 3 Canvas (technical detail)
- Marquee selection UX variations across DAWs (minor feature)

**No critical gaps.** Recommendations are well-supported by research and current codebase analysis.

---

## Sources

**Ableton Live Documentation:**
- [Arrangement View — Ableton Reference Manual Version 12](https://www.ableton.com/en/manual/arrangement-view/) - Timeline features, navigation, automation
- [Live Concepts — Ableton Reference Manual Version 12](https://www.ableton.com/en/live-manual/12/) - Dual-view workflow, clip-based editing
- [Ableton Live: Session & Arrangement Views](https://www.soundonsound.com/techniques/ableton-live-session-arrangement-views) - View integration patterns

**Logic Pro Documentation:**
- [Key commands for Views Showing Time Ruler in Logic Pro for Mac](https://support.apple.com/guide/logicpro/views-showing-time-ruler-lgcp267f63a1/mac) - Time ruler patterns
- [6 Reasons to Use the Logic Marquee Tool](https://whylogicprorules.com/marquee-tool/) - Marquee selection UX
- [Apple Logic Pro 12 and Mainstage 4.0 arrive](https://synthanatomy.com/2026/01/apple-logic-pro-12.html) - Recent features

**Pro Tools & General DAW Research:**
- [Navigating Pro Tools With Ease](https://www.soundonsound.com/techniques/navigating-pro-tools-ease) - Zoom controls, track height
- [Pro Tools: Edit & Timeline Selections](https://www.soundonsound.com/techniques/pro-tools-edit-timeline-selections) - Selection models
- [Scrubbing and Seeking - Audacity Manual](https://manual.audacityteam.org/man/scrubbing_and_seeking.html) - Scrubbing implementation

**Timeline UX Research:**
- [Timeline Navigators: User Interface Design Patterns for Time](https://journals.sagepub.com/doi/10.1177/21695067231192451) - Academic research on timeline patterns
- [How to Create a Good Timeline UI Design](https://mockitt.wondershare.com/ui-ux-design/timeline-ui-design.html) - Timeline design principles

**Automation & Visual Feedback:**
- [Mastering Advanced DAW Automation Curves for Engineers](https://www.departuremusic.com/mastering-advanced-daw-automation-curves/) - Curve types and visual feedback
- [Creating Curved Automation In Ableton Live](https://livekeyboardist.com/curvedautomationlines/) - Bezier curve implementation
- [The Write Stuff](https://www.soundonsound.com/techniques/write-stuff) - Automation writing modes

**Color Coding & Organization:**
- [Colour Coding A Mix Session](https://solarheavystudios.com/colour-coding-a-mix-session/) - Color coding best practices
- [DAW track color scheme](https://jokertonecourse.com/blog/daw-track-color-scheme-part-3/) - Color organization patterns

**Undo/Redo & Non-Destructive Editing:**
- [Nondestructive Editing and Undo/Redo](https://s1manual.presonus.com/Content/Fundamentals_Topics/Nondestructive_Editing.htm) - Non-destructive patterns
- [Unleashing the Power of Undo and Redo in DaVinci Resolve](https://www.gurusoftware.com/unleashing-the-power-of-undo-and-redo-in-davinci-resolve/) - Undo stack architecture

**UX Best Practices:**
- [7 fundamental UX design principles in 2026](https://www.uxdesigninstitute.com/blog/ux-design-principles-2026/) - Modern UX principles
- [14 Common UX Design Mistakes & How to Fix Them](https://contentsquare.com/guides/ux-design/mistakes/) - Anti-patterns to avoid
