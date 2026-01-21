# Roadmap: Hue Companion v0.3

## Overview

Scene Builder bug bash milestone. Two phases address color picker usability issues and interaction defaults discovered during v0.2 testing. Phase 5 delivers a redesigned color picker with temperature + saturation model ensuring all colors are realizable on Hue bulbs. Phase 6 changes snap-to-grid default for better first-use experience.

## Phases

### Phase 5: Color Picker Overhaul

**Goal:** Users select colors via hue + saturation wheel where every choice produces accurate, realizable results on physical Hue bulbs.

**Dependencies:** None (first phase of v0.3)

**Requirements:**
- CPICK-01: Color picker uses hue + saturation wheel clipped to Philips Hue gamut
- CPICK-02: All selectable colors are realizable on Philips Hue bulbs
- CPICK-03: RGB text input fields removed from color picker
- CPICK-04: Color picker circle reduced to ~50% of current size
- CPICK-05: Color picker popup can appear outside timeline area bounds
- COLOR-01: Selected color on picker accurately reflects in gradient bar
- COLOR-02: Dragging keyframe preserves user-selected color (no corruption)

**Success Criteria:**
1. User can select any hue on the color wheel with adjustable saturation
2. Every color selectable in the picker displays correctly on physical Hue bulb
3. Selected color in picker matches the gradient bar segment for that keyframe
4. Dragging a keyframe to a new time position preserves the exact color value
5. Color picker popup positions intelligently and can extend beyond timeline bounds

---

### Phase 6: Interaction Polish

**Goal:** Scene Builder interactions match user expectations on first use without configuration.

**Dependencies:** Phase 5 (ensures color system is stable before changing interaction defaults)

**Requirements:**
- INTER-01: Snap-to-grid disabled by default

**Success Criteria:**
1. New Scene Builder sessions start with snap-to-grid toggle in off state
2. User can still enable snap-to-grid manually when needed

---

## Progress

| Phase | Status | Requirements |
|-------|--------|--------------|
| Phase 5: Color Picker Overhaul | Pending | 7 |
| Phase 6: Interaction Polish | Pending | 1 |

**Total Requirements:** 8
**Mapped:** 8
**Coverage:** 100%

---
*Roadmap created: 2026-01-21*
*Milestone: v0.3 Scene Builder Bug Bash*
