# Requirements: Scene Builder DAW Polish

**Defined:** 2026-01-20
**Core Value:** The track representation must feel like a real DAW — continuous color gradients showing what lights will do over time, with precise time alignment that users can trust.

## v1 Requirements

Requirements for this polish milestone. Each maps to roadmap phases.

### Rendering Foundation

- [x] **REND-01**: Timeline renders via Win2D CanvasControl for GPU acceleration
- [x] **REND-02**: Canvas uses layered architecture (ruler, tracks, keyframes, playhead as separate layers)
- [x] **REND-03**: Rendering scales correctly on high-DPI displays

### Track Visuals

- [x] **TRAK-01**: Each track displays continuous gradient color strip showing interpolated color over time
- [x] **TRAK-02**: Gradient strips have rounded edges and clean borders (Logic Pro aesthetic)
- [x] **TRAK-03**: Tracks and keyframes show hover states on mouse over

### Playhead & Time Ruler

- [x] **PLAY-01**: Time ruler aligns correctly with keyframes when window is resized
- [x] **PLAY-02**: Playhead has 44px minimum drag hit area for accessibility
- [x] **PLAY-03**: Playhead shows visual affordance (cursor change, hover highlight) indicating draggability

### Controls

- [x] **CTRL-01**: Snap and loop use compact icon toggles with accessible on/off states
- [x] **CTRL-02**: Brightness and frequency sliders are debounced to prevent API flooding
- [x] **CTRL-03**: Keyboard shortcuts are documented in UI (tooltips or help)

### Color Picker

- [x] **PICK-01**: Color picker popup is smaller than current implementation
- [x] **PICK-02**: Color picker positions to avoid obscuring target keyframe
- [x] **PICK-03**: Color picker updates are debounced to prevent API spam

## v2 Requirements

Deferred to future milestone. Tracked but not in current roadmap.

### Track Management

- **TRAK-04**: Track height can be adjusted (presets or drag-to-resize)
- **TRAK-05**: Tracks can be reordered via drag

### Controls

- **CTRL-04**: Transport bar redesigned with Ableton-style minimal layout
- **CTRL-05**: Zoom presets via keyboard shortcuts (Z/X)

### Selection

- **SELC-01**: Marquee selection for multiple keyframes
- **SELC-02**: Multi-keyframe move/delete

## Out of Scope

Explicitly excluded. Documented to prevent scope creep.

| Feature | Reason |
|---------|--------|
| Audio waveforms | Light animation only, no audio integration |
| Undo/redo system | Not addressing in this polish pass |
| New animation types | Polish existing, don't expand functionality |
| Virtual scrolling | Scenes won't exceed track/duration limits requiring it |

## Traceability

Which phases cover which requirements. Updated during roadmap creation.

| Requirement | Phase | Status |
|-------------|-------|--------|
| REND-01 | Phase 1 | Complete |
| REND-02 | Phase 1 | Complete |
| REND-03 | Phase 1 | Complete |
| TRAK-01 | Phase 2 | Complete |
| TRAK-02 | Phase 2 | Complete |
| TRAK-03 | Phase 3 | Complete |
| PLAY-01 | Phase 3 | Complete |
| PLAY-02 | Phase 3 | Complete |
| PLAY-03 | Phase 3 | Complete |
| CTRL-01 | Phase 4 | Complete |
| CTRL-02 | Phase 4 | Complete |
| CTRL-03 | Phase 4 | Complete |
| PICK-01 | Phase 4 | Complete |
| PICK-02 | Phase 4 | Complete |
| PICK-03 | Phase 4 | Complete |

**Coverage:**
- v1 requirements: 15 total
- Mapped to phases: 15
- Unmapped: 0

---
*Requirements defined: 2026-01-20*
*Last updated: 2026-01-20 after roadmap creation*
