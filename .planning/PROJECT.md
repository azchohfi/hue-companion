# Scene Builder DAW Polish

## What This Is

A focused polish pass on the Hue Companion Scene Builder — the DAW-style timeline editor for creating animated light scenes. The goal is to elevate it from functional to polished, taking inspiration from professional DAWs like Ableton Live and Logic Pro.

## Core Value

The track representation must feel like a real DAW — continuous color gradients showing what lights will do over time, with precise time alignment that users can trust.

## Requirements

### Validated

Existing Scene Builder capabilities (working today):

- ✓ Timeline with tracks per light — existing
- ✓ Keyframe creation via click, deletion via right-click — existing
- ✓ Keyframe drag repositioning — existing
- ✓ Color picker for keyframe editing — existing
- ✓ Event tracks for random effects (lightning, sparkle, flicker) — existing
- ✓ Snap-to-grid with zoom-adaptive intervals — existing
- ✓ 60fps playback with rate-limited light updates — existing
- ✓ Scene save/load to JSON — existing

### Active

Track representation improvements:

- [ ] Fix time ruler alignment bug (center-aligned, misaligns on window resize)
- [ ] Continuous gradient color bars per track showing interpolated color over time
- [ ] Keyframe markers overlaid on gradient strips
- [ ] Logic Pro aesthetic — rounded edges, clean borders on gradient strips

Bottom controls improvements:

- [ ] Compact icon toggles for snap/loop (accessible on/off states)
- [ ] Better button sizing and reflow behavior
- [ ] Ableton-inspired minimal transport layout

Color picker improvements:

- [ ] Smaller color picker popup
- [ ] Better positioning to avoid obscuring target keyframe

Playhead improvements:

- [ ] Larger drag hit area for playhead
- [ ] Visual affordance indicating draggability (hover state, cursor change)

### Out of Scope

- Audio integration or waveform display — this is lights-only
- Multi-track selection/editing — keep single-track focus for now
- Undo/redo system — not addressing in this pass
- New animation types or event presets — polish existing, don't expand

## Context

**Existing codebase:** WinUI 3 app with MVVM pattern using CommunityToolkit.Mvvm. Scene Builder is in `Views/SceneBuilderPage.xaml` with `Core/ViewModels/SceneBuilderViewModel.cs`.

**Graphics:** Uses Microsoft.Graphics.Win2D for timeline rendering. Canvas is manually drawn via `RenderTimeline()` method.

**Current rendering approach:** Keyframes drawn as colored circles on canvas. Time ruler rendered with tick marks. Playhead is a vertical line.

**Reference DAWs:**
- Ableton Live — minimal transport controls, clean aesthetic
- Logic Pro — rounded edges, polished borders on track lanes

## Constraints

- **Platform:** WinUI 3 / Windows App SDK — must use available controls and Win2D
- **Performance:** 60fps timeline rendering must be maintained during playback
- **Accessibility:** Icon toggles need proper accessible states (not just visual)

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Gradient strips behind existing keyframe markers | User prefers current keyframe circles, gradient adds context without replacing them | — Pending |
| Logic Pro aesthetic for borders | Rounded edges and clean borders match desired polish level | — Pending |
| Ableton-style icon toggles | Compact, recognizable, good reflow behavior | — Pending |

---
*Last updated: 2026-01-20 after initialization*
