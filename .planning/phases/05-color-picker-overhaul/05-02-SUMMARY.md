---
phase: 05-color-picker-overhaul
plan: 02
subsystem: ui
tags: [hue-api, color-science, cie-xy, gamut, winui3]

# Dependency graph
requires:
  - phase: 05-color-picker-overhaul
    provides: HUE-COLORS.md research document with gamut algorithms
provides:
  - ColorGamut class with triangle math for gamut boundaries
  - Point-in-triangle testing using cross product method
  - Gamut clipping algorithm for out-of-gamut colors
  - Max saturation calculation per hue angle
affects: [05-03-color-picker-ui, light-detail, scene-builder]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - CIE xy color space math operations
    - Ray-triangle intersection for gamut boundaries
    - Gamut-aware color validation

key-files:
  created:
    - src/HueWindows.Core/Models/ColorGamut.cs
  modified: []

key-decisions:
  - "Use cross product sign method for point-in-triangle (simpler than barycentric)"
  - "Implement adaptive saturation via ray-triangle intersection from white point"
  - "Store gamuts A, B, C as static readonly instances for reuse"

patterns-established:
  - "ColorGamut instances are immutable value objects"
  - "All gamut operations return new coordinates, never mutate"

# Metrics
duration: 3min
completed: 2026-01-21
---

# Phase 05 Plan 02: ColorGamut Class Summary

**Complete gamut triangle math for Philips Hue color boundaries with point containment, clipping, and adaptive saturation**

## Performance

- **Duration:** 3 min
- **Started:** 2026-01-21T21:29:57Z
- **Completed:** 2026-01-21T21:33:04Z
- **Tasks:** 3
- **Files modified:** 1

## Accomplishments
- ColorGamut class with GamutA, GamutB, GamutC definitions from research
- Point-in-triangle testing using cross product sign method
- Gamut clipping to project out-of-gamut colors to nearest edge
- Adaptive saturation calculation per hue angle via ray-triangle intersection

## Task Commits

Each task was committed atomically:

1. **Task 1: Create ColorGamut class with gamut definitions** - `56fcba9` (feat)
2. **Task 2: Add point-in-triangle and clipping methods** - `42d650a` (feat)
3. **Task 3: Add max saturation per hue calculation** - `af97c93` (feat)

## Files Created/Modified
- `src/HueWindows.Core/Models/ColorGamut.cs` - Gamut triangle math for Hue color constraints (253 lines)
  - Contains() - Point-in-triangle test using cross products
  - Clip() - Projects out-of-gamut points to nearest edge
  - GetMaxSaturationForHue() - Calculates achievable saturation per hue angle
  - Helper methods: ClosestPointOnSegment, RaySegmentIntersection, HsvToXyDirection

## Decisions Made

**Cross product sign method for point-in-triangle:**
- Simpler than barycentric coordinates
- No division edge cases
- Well-documented algorithm from research

**Ray-triangle intersection for max saturation:**
- Enables adaptive saturation UI (limits saturation ring dynamically)
- More accurate than fixed saturation limits
- Matches Philips Hue's actual color capabilities

**Static gamut instances:**
- GamutA, GamutB, GamutC as readonly static fields
- Avoids repeated allocation
- Safe for concurrent access (immutable)

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None - all algorithms implemented directly from research document.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

**Ready for phase 05-03 (Color Picker UI):**
- ColorGamut class provides all needed gamut operations
- Can be used in GamutColorPicker control for:
  - Validating user color selections
  - Clipping out-of-gamut colors before sending to Hue API
  - Rendering adaptive saturation ring (dynamically sized per hue)

**Usage pattern for next phase:**
```csharp
// Get light's gamut (from HueBridgeService)
var gamut = ColorGamut.GamutC;

// Check if color is achievable
bool canDisplay = gamut.Contains(xy.X, xy.Y);

// Clip to gamut if needed
var (clippedX, clippedY) = gamut.Clip(xy.X, xy.Y);

// Limit saturation ring radius per hue
double maxSat = gamut.GetMaxSaturationForHue(hueDegrees);
```

**No blockers or concerns.**

---
*Phase: 05-color-picker-overhaul*
*Completed: 2026-01-21*
