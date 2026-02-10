---
phase: 05-color-picker-overhaul
plan: 03
subsystem: ui
tags: [winui3, win2d, color-picker, gamut, cie-xy, hue-api, canvas-rendering]

# Dependency graph
requires:
  - phase: 05-color-picker-overhaul
    provides: ColorGamut class with gamut triangle math and saturation limits
provides:
  - GamutColorPicker UserControl with Win2D rendering
  - Hue ring showing full 360-degree color selection
  - Saturation area with gamut-aware graying
  - Real-time color output as HueColor (CIE xy)
  - Brightness slider integration
affects: [05-04-scene-builder-integration, light-detail, scene-builder]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - Win2D CanvasControl for custom UI rendering
    - CanvasGeometry arc rendering for color wheels
    - HSV to CIE xy color space conversion
    - Gamut-aware color selection with visual feedback

key-files:
  created:
    - src/HueCompanion/Controls/GamutColorPicker.xaml
    - src/HueCompanion/Controls/GamutColorPicker.xaml.cs
  modified: []

key-decisions:
  - "Use HSV internally for color wheel display, convert to HueColor (xy) for output"
  - "Cache max saturation values per hue angle (360 values) for performance"
  - "Gray out-of-gamut regions with 100 alpha to show unavailable colors"
  - "Render hue ring with 60 segments and saturation with 8 steps for smooth appearance"

patterns-established:
  - "Win2D cleanup via Unloaded event + RemoveFromVisualTree()"
  - "Pointer drag handling with capture for smooth color selection"
  - "DependencyProperty for data binding integration"

# Metrics
duration: 4min
completed: 2026-01-21
---

# Phase 05 Plan 03: GamutColorPicker Control Summary

**Custom Win2D color picker with gamut-aware hue ring and adaptive saturation limiting**

## Performance

- **Duration:** 4 min
- **Started:** 2026-01-21T21:37:56Z
- **Completed:** 2026-01-21T21:42:13Z
- **Tasks:** 3
- **Files modified:** 2

## Accomplishments
- GamutColorPicker UserControl with Win2D CanvasControl rendering
- Full 360-degree hue ring with 60 segments for smooth gradients
- Saturation area with gamut-aware graying (8 concentric steps)
- Pointer drag handling for both hue ring and saturation selection
- Real-time ColorChanged event with HueColor (CIE xy) output
- Max saturation caching for performance (360 pre-calculated values)
- Win2D resource cleanup via Unloaded event handler

## Task Commits

Each task was committed atomically:

1. **Task 1: Create GamutColorPicker XAML structure** - `5a2305a` (feat)
2. **Task 2: Implement GamutColorPicker code-behind with rendering** - `41f8175` (feat)
3. **Task 3: Add Unloaded cleanup for Win2D resources** - `de3f2a1` (feat)

## Files Created/Modified
- `src/HueCompanion/Controls/GamutColorPicker.xaml` - UserControl with CanvasControl and brightness slider (37 lines)
- `src/HueCompanion/Controls/GamutColorPicker.xaml.cs` - Win2D rendering, input handling, color conversion (357 lines)
  - DrawHueRing() - Renders 60-segment color wheel
  - DrawSaturationArea() - Renders adaptive saturation area with gamut graying
  - DrawSelectionIndicator() - Shows current color selection
  - HandlePointerInput() - Drag handling for hue and saturation
  - GetMaxSaturation() - Cached gamut limit lookup
  - HsvToColor() / RgbToHsv() - Color space conversion helpers

## Decisions Made

**Use HSV for internal display:**
- Hue ring is naturally HSV-based (360-degree circular representation)
- Convert to HueColor (CIE xy) only for output to maintain API consistency
- Allows smooth color wheel rendering without complex xy-to-visual mapping

**Cache max saturation per hue:**
- Pre-calculate all 360 values in constructor
- Avoids expensive gamut calculations during drag operations
- Trade memory (360 doubles = 2.8KB) for responsiveness

**Gray out-of-gamut regions:**
- Alpha 100 overlay shows unavailable colors visually
- Users can see full hue spectrum but understand gamut limits
- Prevents selecting impossible colors without hiding the color space

**60 hue segments, 8 saturation steps:**
- Balance between smooth appearance and rendering performance
- Arc geometry generation is fast enough for 60 segments
- 8 saturation steps provide clear visual gradient without banding

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Added Microsoft.UI using statement for Colors class**
- **Found during:** Task 2 (Build verification)
- **Issue:** Build failed with "Colors does not exist in the current context" errors
- **Fix:** Added `using Microsoft.UI;` to access Colors.White, Colors.Black, Colors.Transparent
- **Files modified:** src/HueCompanion/Controls/GamutColorPicker.xaml.cs
- **Verification:** Build succeeded with no errors
- **Committed in:** 41f8175 (Task 2 commit, fixed before committing)

---

**Total deviations:** 1 auto-fixed (1 blocking)
**Impact on plan:** Essential fix for compilation. No scope change.

## Issues Encountered

**Dev instance file lock:**
- Build initially failed due to running dev instance locking HueCompanion.Core.dll
- Killed process with taskkill, rebuild succeeded
- Expected behavior for Windows locked file access

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

**Ready for phase 05-04 (Scene Builder Integration):**
- GamutColorPicker can be dropped into SceneBuilderPage to replace WinUI ColorPicker
- Control exposes:
  - `SelectedColor` DependencyProperty (HueColor, two-way bindable)
  - `Gamut` DependencyProperty (default: GamutC, configurable per light)
  - `ColorChanged` event for real-time updates
  - `Brightness` property (0-1) for separate brightness control
- Size: 160x200 pixels (50% smaller than original ~300x400 ColorPicker)
- No RGB input fields (matches CPICK-03 requirement)

**Integration pattern:**
```xaml
<controls:GamutColorPicker
    SelectedColor="{x:Bind ViewModel.SelectedColor, Mode=TwoWay}"
    Gamut="{x:Bind ViewModel.CurrentGamut}"
    ColorChanged="ColorPicker_ColorChanged"/>
```

**No blockers or concerns.**

---
*Phase: 05-color-picker-overhaul*
*Completed: 2026-01-21*
