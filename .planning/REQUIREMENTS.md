# Requirements: Hue Companion

**Defined:** 2026-01-21
**Core Value:** The track representation must feel like a real DAW — continuous color gradients showing what lights will do over time, with precise time alignment that users can trust.

## v0.3 Requirements

Bug fixes and usability improvements for Scene Builder color picker and interactions.

### Color Picker Redesign

- [ ] **CPICK-01**: Color picker uses temperature + saturation model instead of full color wheel
- [ ] **CPICK-02**: All selectable colors are realizable on Philips Hue bulbs
- [ ] **CPICK-03**: RGB text input fields removed from color picker
- [ ] **CPICK-04**: Color picker circle reduced to ~50% of current size
- [ ] **CPICK-05**: Color picker popup can appear outside timeline area bounds

### Color Accuracy

- [ ] **COLOR-01**: Selected color on picker accurately reflects in gradient bar
- [ ] **COLOR-02**: Dragging keyframe preserves user-selected color (no corruption)

### Interaction Defaults

- [ ] **INTER-01**: Snap-to-grid disabled by default

## Future Requirements

Deferred from v0.2 to future milestones:

### Scene Builder v2

- **SCENE-01**: Track height adjustment (presets or drag-to-resize)
- **SCENE-02**: Track reordering via drag
- **SCENE-03**: Ableton-style minimal transport layout
- **SCENE-04**: Zoom presets via keyboard shortcuts (Z/X)
- **SCENE-05**: Marquee selection for multiple keyframes
- **SCENE-06**: Multi-keyframe move/delete

## Out of Scope

| Feature | Reason |
|---------|--------|
| Full HSV/HSL color wheel | Hue bulbs can't reproduce all colors; temperature + saturation is more appropriate |
| Audio integration | This is lights-only |
| Undo/redo system | Not addressing in this pass |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| CPICK-01 | TBD | Pending |
| CPICK-02 | TBD | Pending |
| CPICK-03 | TBD | Pending |
| CPICK-04 | TBD | Pending |
| CPICK-05 | TBD | Pending |
| COLOR-01 | TBD | Pending |
| COLOR-02 | TBD | Pending |
| INTER-01 | TBD | Pending |

**Coverage:**
- v0.3 requirements: 8 total
- Mapped to phases: 0
- Unmapped: 8 ⚠️

---
*Requirements defined: 2026-01-21*
*Last updated: 2026-01-21 after initial definition*
