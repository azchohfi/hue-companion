# Pitfalls Research: DAW Timeline UI

**Domain:** DAW-style timeline editors for animation
**Project:** Hue Windows Scene Builder
**Researched:** 2026-01-20
**Overall Confidence:** MEDIUM-HIGH

Mix of general canvas/timeline UI pitfalls (HIGH confidence from multiple authoritative sources) and DAW-specific insights (MEDIUM confidence, limited DAW-specific sources). Current Scene Builder implementation reviewed for context.

---

## Critical Pitfalls

These mistakes break usability or require rewrites. Address immediately.

### Pitfall 1: Full Canvas Redraw on Every Update

**What goes wrong:**
Calling `Canvas.Children.Clear()` and re-rendering everything on every frame kills performance. Current Scene Builder does this in `RenderTimeline()` which is called on zoom, duration change, keyframe edits, and during playback.

**Why it happens:**
Immediate-mode canvas rendering makes "clear everything and redraw" the simplest approach. Every canvas function call results in immediate drawing, and the entire canvas must be redrawn each frame.

**Consequences:**
- Stuttering animations at 60fps
- High CPU usage
- Poor battery life on laptops
- Unresponsive UI during complex scenes

**Warning signs:**
- Frame drops when timeline has >50 keyframes
- Zoom/pan feels sluggish
- Battery drains rapidly during editing

**Prevention:**
1. **Dirty region tracking** - Only redraw changed areas
2. **Layer separation** - Static elements (grid, ruler) on separate canvas from dynamic (playhead, keyframes)
3. **Optimized playhead** - Current implementation uses `UpdatePlayheadPosition()` for playhead-only updates (GOOD), but grid/ruler still re-render unnecessarily
4. **Viewport culling** - Don't render keyframes outside visible scroll region

**Current status in Scene Builder:**
PARTIALLY ADDRESSED - `UpdatePlayheadPosition()` optimizes playhead during playback, but zoom/keyframe edits still trigger full `RenderTimeline()`. Ruler re-renders even when only keyframes changed.

**Phase recommendation:** Address in polish phase - implement dirty flags and layer separation.

---

### Pitfall 2: Ruler Alignment Breaks on Window Resize

**What goes wrong:**
Time ruler uses absolute/fixed positioning that doesn't track with timeline canvas when window resizes. Labels become misaligned with grid lines.

**Why it happens:**
Canvas width calculated dynamically (`DurationSeconds * ZoomLevel`) but ruler positioning assumes fixed relationship. Center-aligned rulers especially problematic.

**Consequences:**
- Ruler labels at wrong positions relative to keyframes
- Grid lines don't match tick marks
- User loses trust in visual feedback
- Snap-to-grid snaps to wrong positions visually (even if logically correct)

**Warning signs:**
- Bug reports: "grid doesn't line up after resize"
- Ruler "drifts" during zoom operations
- Labels overlap or have gaps

**Prevention:**
1. **Bind ruler width to canvas width** - Use same calculation for both
2. **Left-align rulers** - Avoid center alignment which amplifies resize errors
3. **Shared coordinate system** - Ruler and canvas should reference same time-to-pixel conversion
4. **Resize listener** - Trigger ruler re-render on window resize events

**Current status in Scene Builder:**
PRESENT - `RenderTimeRuler()` sets `TimeRulerCanvas.Width = ViewModel.DurationSeconds * ViewModel.ZoomLevel` but doesn't listen for window resize. Ruler is in separate canvas from keyframes.

**Phase recommendation:** Fix immediately - add window resize handler that triggers `RenderTimeRuler()`.

---

### Pitfall 3: Zoom Loses User's Focus Point

**What goes wrong:**
Zooming centers on timeline origin (time=0) instead of where user is looking/cursor position. User has to scroll to find their work after every zoom.

**Why it happens:**
Default zoom implementations scale from origin. Calculating "zoom to cursor" requires tracking mouse position and adjusting scroll offset.

**Consequences:**
- Frustrating workflow - zoom in, scroll to find position, repeat
- Users avoid zooming, work at wrong granularity
- Mistakes from editing at wrong time position

**Warning signs:**
- User constantly scrolling after zoom
- Complaints about "timeline jumping around"
- Feature requests for "zoom to cursor"

**Prevention:**
```csharp
// Calculate time under cursor before zoom
var mouseX = point.Position.X;
var timeUnderMouse = mouseX / oldZoom;

// Apply zoom
ViewModel.ZoomLevel = newZoom;

// Adjust scroll to keep same time under cursor
var newMouseX = timeUnderMouse * newZoom;
var scrollDelta = newMouseX - mouseX;
scrollViewer.ChangeView(scrollViewer.HorizontalOffset + scrollDelta, null, null, false);
```

**Current status in Scene Builder:**
IMPLEMENTED - `KeyframeCanvas_PointerWheelChanged()` implements zoom-to-cursor correctly (lines 1076-1104).

**Phase recommendation:** No action needed - already working.

---

### Pitfall 4: Touch Targets Too Small (Accessibility)

**What goes wrong:**
Playhead handle (12px triangle), keyframes (16px circles), and interactive elements fail WCAG 2.5.5 accessibility standards. Users with tremors, touch input, or mobility issues can't interact.

**Why it happens:**
Designers optimize for visual density, not interaction. "Looks clean" prioritized over "works for everyone."

**Consequences:**
- Inaccessible to users with motor impairments
- Frustrating on touch devices (tablets, Surface)
- Accidental clicks on wrong elements
- Users avoid features they can't reliably interact with

**Warning signs:**
- Complaints about "can't click playhead"
- Touch device users report difficulty
- Accessibility audit failures
- Users request "bigger handles"

**Prevention:**
1. **Minimum 44x44px touch targets** (WCAG 2.5.5 Level AAA)
2. **Minimum 24x24px with spacing** (WCAG 2.5.8 Minimum)
3. **Invisible hit areas** - Visual can be small, interactive area larger (see current playhead hit area)
4. **Hover affordances** - Show expanded hit area on hover
5. **Padding around visuals** - Icon appears 24px but padding creates 48px target

**Current status in Scene Builder:**
PARTIALLY ADDRESSED:
- Playhead has 12px wide hit area (line 359) - BELOW standard
- Keyframes are 16px circles - BELOW standard
- No hover affordances
- No visible indication of touch target size

**Phase recommendation:** Address in polish phase:
- Increase playhead hit area to 44px
- Add hover state showing expanded hit area
- Consider larger keyframe circles or invisible padding rings

---

## Performance Pitfalls

These kill 60fps. Address to improve responsiveness.

### Pitfall 5: Timer Resolution Mismatch

**What goes wrong:**
`DispatcherTimer` at 16ms (60fps) doesn't guarantee 60fps updates. Timer drift accumulates, animations stutter.

**Why it happens:**
`DispatcherTimer` is UI-thread based, not real-time. Competing UI operations delay timer ticks. Actual interval varies 16-30ms.

**Consequences:**
- Playhead movement not smooth
- Animation preview feels stuttery
- Time display lags behind actual time

**Prevention:**
1. **Calculate delta time** - Measure actual elapsed time between frames (IMPLEMENTED in Scene Builder line 194)
2. **Use DateTime.Now** - Don't assume fixed 16ms intervals
3. **Frame skip on lag** - If delta >33ms, skip interpolation to catch up
4. **Consider `Storyboard`** - WinUI's native animation system may be smoother for simple cases

**Current status in Scene Builder:**
IMPLEMENTED - Playback timer correctly calculates `deltaSeconds = (now - _lastFrameTime).TotalSeconds` (lines 193-195).

**Phase recommendation:** No action needed - already using delta time.

---

### Pitfall 6: API Flooding During Playback

**What goes wrong:**
Sending light commands at 60fps overwhelms Hue bridge. Bridge drops commands, lights stutter or stop responding.

**Why it happens:**
Tight coupling between playback loop and hardware API. Every frame triggers network calls.

**Consequences:**
- Lights don't follow animation smoothly
- Bridge becomes unresponsive
- Other apps controlling lights experience lag
- Scene playback doesn't match editor preview

**Prevention:**
1. **Rate limiting** - Throttle light updates to bridge's capability (~10Hz for Hue)
2. **Command batching** - Group multiple light updates into single API call
3. **Local preview** - Render timeline visually at 60fps, update hardware at 10fps
4. **Delta detection** - Only send commands when interpolated value changes meaningfully

**Current status in Scene Builder:**
IMPLEMENTED - Rate limited to 100ms intervals (line 223): `if (msSinceLastUpdate >= LightUpdateIntervalMs)`.

**Phase recommendation:** No action needed - already throttled to ~10Hz.

---

### Pitfall 7: Live Preview on Every Keyframe Edit

**What goes wrong:**
Changing color/brightness in side panel triggers immediate light update. Dragging brightness slider sends 30+ commands per second to bridge during drag.

**Why it happens:**
Direct binding: `await ViewModel.UpdateLightForKeyframeAsync(ViewModel.SelectedKeyframe)` on every slider value change (line 1292).

**Consequences:**
- Bridge overwhelmed by rapid commands
- Slider movement feels laggy (waiting for API response)
- Other lights/apps experience latency
- Undo/redo stack polluted with intermediate values

**Prevention:**
1. **Debounce input** - Wait 150-300ms after user stops changing value before sending
2. **Visual-only preview** - Update keyframe circle color immediately, send to bridge after delay
3. **Throttle slider events** - Only process every Nth value change during drag
4. **Send on release** - Only update bridge when user releases slider/closes color picker

**Current status in Scene Builder:**
NOT IMPLEMENTED - Color picker and brightness slider send commands immediately on every change.

**Phase recommendation:** Fix in polish phase - add debouncing to `KeyframeColorPicker_ColorChanged` and `BrightnessSlider_ValueChanged`.

---

## Visual Pitfalls

Design mistakes that hurt usability.

### Pitfall 8: Color Picker Obscures Timeline

**What goes wrong:**
Large color picker overlay (often 300x300px+) blocks view of keyframes and timeline when editing. User can't see context of what they're changing.

**Why it happens:**
Default color picker controls designed for general UI, not spatial editors. Positioned inline or relative to parent, not viewport-aware.

**Consequences:**
- Can't see keyframe being edited
- Can't compare color to adjacent keyframes
- Must close picker to see result, reopen to adjust (slow iteration)
- Workflow: pick color, close picker, see result, reopen picker, adjust (repeat)

**Warning signs:**
- Users request "smaller color picker"
- Complaints about "can't see what I'm doing"
- Feature requests for "floating color picker"

**Prevention:**
1. **Compact color picker** - Use HSV gradient square (150x150) instead of wheel
2. **Popup positioning** - Position picker to side of timeline, not over it
3. **Transparent background** - Let timeline show through slightly
4. **Inline mini preview** - Show color swatch on keyframe, popup only for detailed editing
5. **Recent colors palette** - Allow quick selection without opening full picker

**Current status in Scene Builder:**
PRESENT - Color picker in side panel (SidePanel), but panel overlays timeline. No viewport-aware positioning.

**Phase recommendation:** Address in polish phase:
- Make side panel narrower (reduce picker size)
- Or move color picker to flyout/popup positioned intelligently
- Add recent colors palette for quick selection

---

### Pitfall 9: No Visual Feedback on Hover

**What goes wrong:**
No hover states on interactive elements (keyframes, playhead, grid lines). User doesn't know what's clickable or draggable.

**Why it happens:**
Canvas elements don't have built-in hover states like HTML/XAML controls. Must implement manually.

**Consequences:**
- User doesn't realize keyframes are clickable
- No indication that playhead is draggable
- Trial-and-error to discover interactions
- Features go unused because they're not discoverable

**Prevention:**
1. **Cursor changes** - Change to pointer/hand cursor on hover
2. **Visual highlight** - Brighten/outline hovered element
3. **Tooltip hints** - Show time/value on hover
4. **Size increase** - Slightly enlarge hovered element
5. **Animation** - Subtle pulse or glow on hover

**Current status in Scene Builder:**
NOT IMPLEMENTED - No hover states on keyframes or playhead. Canvas elements don't respond to hover.

**Phase recommendation:** Add in polish phase:
- `PointerEntered`/`PointerExited` handlers
- Visual feedback (glow, scale, cursor change)
- Tooltips showing keyframe time and color

---

### Pitfall 10: Snap Grid Not Visible Until Interaction

**What goes wrong:**
Snap-to-grid enabled by default, but grid lines only visible if you know to look. Users confused why keyframes "jump" to specific positions.

**Why it happens:**
Grid rendered at low opacity (25 alpha in Scene Builder) to avoid visual clutter. Design choice prioritizes clean look over discoverability.

**Consequences:**
- Users don't understand why dragging "snaps"
- Disable snap-to-grid because behavior feels broken
- Accidental keyframe placement at wrong time
- Requests to "fix the jumping"

**Prevention:**
1. **Toggle visibility** - Grid only shows when snap enabled
2. **Highlight on drag** - Brighten nearest snap line during keyframe drag
3. **Snap indicator** - Show small icon/badge when snap is active
4. **Tutorial hint** - First-time tooltip explaining snap-to-grid
5. **Dynamic opacity** - Increase grid opacity at higher zoom levels

**Current status in Scene Builder:**
PRESENT - Grid always rendered at 25 alpha regardless of snap state. No indication snap is enabled except toggle button.

**Phase recommendation:** Enhance in polish phase:
- Only show grid when snap enabled
- Highlight nearest snap line during drag
- Add snap indicator icon near cursor during drag

---

## Interaction Pitfalls

UX mistakes that frustrate users.

### Pitfall 11: Modifier Keys Not Discoverable

**What goes wrong:**
Ctrl-click to disable snap, Shift-click for multi-select, but no indication these shortcuts exist. Users resort to toggling snap button repeatedly.

**Why it happens:**
Keyboard shortcuts are powerful but invisible. No affordance in UI showing alternate interaction modes.

**Consequences:**
- Users toggle snap on/off repeatedly instead of holding Ctrl
- Single-select workflow (don't know about Shift-click)
- Copy/paste goes unused (Ctrl+C/V not obvious)
- Power users frustrated by slow discoverable workflow

**Prevention:**
1. **Keyboard hints in tooltips** - "Hold Ctrl to ignore snap"
2. **Visual feedback** - Show "SNAP OFF" indicator when Ctrl held
3. **Keyboard shortcuts panel** - Help overlay showing all shortcuts (press ? key)
4. **Context menu hints** - Right-click shows available modifiers
5. **Tutorial overlays** - First-time tips showing key combinations

**Current status in Scene Builder:**
NOT DOCUMENTED - Ctrl and Shift modifiers implemented but not surfaced in UI. No tooltips, no help panel.

**Phase recommendation:** Document in polish phase:
- Add keyboard shortcuts help panel
- Tooltips on controls mentioning modifiers
- Visual feedback when modifier detected

---

### Pitfall 12: Undo/Redo Not Granular Enough

**What goes wrong:**
Every keyframe property change creates undo entry. Adjusting color creates 20+ undo states as RGB sliders update. User expects "undo color change" not "undo last red value increment."

**Why it happens:**
Undo triggered on property change event, which fires continuously during slider drag or color picker interaction.

**Consequences:**
- Undo stack cluttered with micro-changes
- Must press Ctrl+Z 15 times to undo one color adjustment
- Undo/redo becomes useless for major edits
- Users avoid undo, manually recreate previous state

**Prevention:**
1. **Batch related changes** - Group all color picker changes into single undo entry
2. **Transaction boundaries** - Begin transaction on slider press, commit on release
3. **Debounced undo** - Only create undo entry after 500ms of no changes
4. **Semantic commands** - "Change keyframe color" not "Set R to 128, Set G to 64..."
5. **Command consolidation** - Consecutive similar commands merge into one

**Current status in Scene Builder:**
UNCLEAR - Undo/redo infrastructure exists (`TimelineCommandHistory`) but unclear if color picker/slider changes create undo entries. Code shows commands for add/delete but not property changes.

**Phase recommendation:** Verify in polish phase:
- Test undo behavior during color/brightness editing
- Add transaction support if needed
- Batch property changes into single command

---

### Pitfall 13: Multi-Select Without Visual Rectangle

**What goes wrong:**
Shift-click multi-select works, but no marquee/rectangle selection tool. Selecting 10 keyframes requires clicking each individually while holding Shift.

**Why it happens:**
Marquee selection requires drag interaction, collision detection, and visual feedback - more complex than click handlers.

**Consequences:**
- Slow workflow for bulk operations
- Users don't discover multi-select capability
- Copy/paste/delete limited to small selections
- Feature requests for "select all in range"

**Prevention:**
1. **Click-drag marquee** - Draw selection rectangle on canvas drag
2. **Visual feedback** - Show blue rectangle during drag
3. **Collision detection** - Select keyframes overlapping rectangle
4. **Ctrl+A select all** - (Already implemented in Scene Builder)
5. **Time range selection** - Click ruler to select all keyframes in time range

**Current status in Scene Builder:**
NOT IMPLEMENTED - Shift-click multi-select works (line 801), but no drag-to-select marquee. `SelectKeyframesInRect()` exists (line 1056) but no UI trigger.

**Phase recommendation:** Add in polish phase:
- Canvas drag to create selection rectangle
- Visual marquee box during drag
- Hook up existing `SelectKeyframesInRect()` method

---

### Pitfall 14: Scroll Position Lost on Timeline Rebuild

**What goes wrong:**
Changing duration, zoom, or room selection triggers full timeline rebuild. Scroll position resets to 0, user loses place.

**Why it happens:**
`RenderTimeline()` recreates canvas which resets `ScrollViewer.HorizontalOffset` to default.

**Consequences:**
- User scrolled to 45s editing keyframe, changes duration, jumps back to 0s
- Zoom in to see detail, scroll position lost, must scroll right again
- Frustrating during iterative editing

**Prevention:**
1. **Save/restore scroll** - Capture `ScrollViewer.HorizontalOffset` before rebuild, restore after
2. **Calculate equivalent position** - When zooming, compute new offset maintaining same time in view
3. **Preserve playhead visibility** - Scroll to keep playhead in view after changes
4. **Partial rebuild** - Don't rebuild entire timeline for minor changes

**Current status in Scene Builder:**
NOT IMPLEMENTED - `RenderTimeline()` has no scroll preservation. Zoom handler preserves cursor position (line 1103) but duration/room changes don't.

**Phase recommendation:** Fix in polish phase:
- Save scroll position before `RenderTimeline()`
- Restore proportional position after (accounting for zoom changes)

---

## Domain-Specific Pitfalls

Mistakes specific to timeline animation editors.

### Pitfall 15: Linear Interpolation for Color (Muddy Colors)

**What goes wrong:**
Interpolating RGB colors linearly produces muddy browns between vibrant colors. Blue to yellow passes through gray instead of green.

**Why it happens:**
RGB color space is not perceptually uniform. Midpoint in RGB != midpoint visually.

**Consequences:**
- Animations look dull, not vibrant
- Colors users didn't intend appear mid-transition
- Professional appearance suffers

**Prevention:**
Use HSV/HSL interpolation instead of RGB. Already implemented in Scene Builder (`ColorConverter.InterpolateHsv()` line 380).

**Current status in Scene Builder:**
SOLVED - HSV interpolation already used (lines 380-385, 591).

**Phase recommendation:** No action needed.

---

### Pitfall 16: No Easing Visualization

**What goes wrong:**
Transition style dropdown (Linear, EaseIn, EaseOut) has no visual preview. User doesn't understand difference between options.

**Why it happens:**
Easing curves abstract concept, hard to communicate in text.

**Consequences:**
- Users stick to default (EaseInOut) for everything
- Animations lack variety and impact
- Professional features go unused

**Prevention:**
1. **Curve preview** - Show small easing curve graph next to dropdown
2. **Live preview** - Animate icon/ball showing easing when selected
3. **Better names** - "Ease In (slow start)" instead of just "EaseIn"
4. **Visual icons** - Icons showing curve shape in dropdown

**Current status in Scene Builder:**
NOT IMPLEMENTED - Transition dropdown is text-only (TransitionComboBox in XAML).

**Phase recommendation:** Enhance in polish phase:
- Add curve icons to dropdown items
- Or add mini preview panel showing selected easing

---

### Pitfall 17: Event Track Frequency Slider Not Calibrated

**What goes wrong:**
Frequency slider 0.0-1.0 with labels "Rare/Medium/Frequent" doesn't communicate actual timing. What's "rare" - once per minute? Once per 10 seconds?

**Why it happens:**
Abstract 0-1 scale simpler to implement than concrete time values. Mapping to (min, max) intervals hidden from user.

**Consequences:**
- Trial and error to find desired frequency
- Users request "more control" over timing
- Can't recreate specific timing from memory

**Prevention:**
1. **Show actual intervals** - "Rare (8-15s)" instead of just "Rare"
2. **Time-based slider** - Slider controls "average interval in seconds" directly
3. **Preview mode** - Show visualization of event triggers on timeline
4. **Examples** - "Lightning every ~10 seconds" more meaningful than "Frequent"

**Current status in Scene Builder:**
PRESENT - Frequency slider 0-1 with text labels (line 1520). Interval mapping hidden in `GetInterval()` (lines 1283-1288).

**Phase recommendation:** Improve in polish phase:
- Show actual time ranges in labels: "Rare (8-15s), Medium (3-6s), Frequent (0.5-2s)"
- Or change to time-based input

---

## Prevention Strategies by Phase

### Phase 1: Core Functionality (Already Complete)
- [x] Zoom-to-cursor implementation
- [x] Delta-time playback calculations
- [x] Rate-limited bridge updates
- [x] HSV color interpolation
- [x] Optimized playhead updates during playback

### Phase 2: Polish and Accessibility (Recommended Next)

**High Priority:**
1. Window resize handler for ruler alignment
2. Debounce color picker and brightness slider
3. Increase touch target sizes (playhead 44px, keyframe padding)
4. Add hover states and cursor changes
5. Scroll position preservation on timeline rebuild

**Medium Priority:**
6. Grid visibility tied to snap toggle
7. Keyboard shortcuts documentation/tooltips
8. Snap indicator during drag
9. Compact/repositioned color picker

**Lower Priority:**
10. Marquee selection rectangle
11. Easing curve visualization
12. Event frequency with actual time ranges

### Phase 3: Performance Optimization (If Needed)

Only if performance issues observed with large scenes (>100 keyframes):

1. Dirty region tracking
2. Layer-based rendering (static vs dynamic)
3. Viewport culling
4. Undo batching for property changes

---

## Testing Checklist

Before claiming "polish complete," verify:

### Accessibility
- [ ] Playhead touch target >= 44px
- [ ] Keyframe touch target >= 24px or 44px with padding
- [ ] All interactive elements have hover states
- [ ] Keyboard shortcuts work without mouse
- [ ] Screen reader can announce timeline state (if applicable)

### Performance
- [ ] 60fps playback with 50+ keyframes
- [ ] No frame drops during zoom/pan
- [ ] Light updates throttled to ~10Hz
- [ ] No lag during color picker interaction

### Correctness
- [ ] Ruler aligned after window resize
- [ ] Scroll position preserved on rebuild
- [ ] Zoom centers on cursor position
- [ ] Snap grid visible when snap enabled

### Usability
- [ ] Color picker doesn't obscure timeline
- [ ] Modifier keys documented in tooltips
- [ ] Undo/redo at appropriate granularity
- [ ] Multi-select works with marquee

---

## Confidence Assessment

| Category | Confidence | Reasoning |
|----------|-----------|-----------|
| Canvas Performance | HIGH | Multiple authoritative sources (MDN, web.dev, AG Grid) |
| Touch Targets | HIGH | WCAG standards, W3C, NN/g documentation |
| Zoom/Pan | MEDIUM | Found positioning issues in various editors, principles apply |
| Color Picker UX | MEDIUM | Design pattern issues well-documented, positioning less so |
| DAW-Specific | MEDIUM-LOW | Limited DAW-specific sources, extrapolated from general timeline editors |

**Overall assessment:** HIGH confidence on performance/accessibility pitfalls (verified against standards), MEDIUM confidence on UX/interaction pitfalls (based on general UI patterns and codebase review).

---

## Sources

### Canvas Performance
- [Optimizing canvas - MDN Web Docs](https://developer.mozilla.org/en-US/docs/Web/API/Canvas_API/Tutorial/Optimizing_canvas)
- [HTML5 Canvas Performance Tips - GitHub Gist](https://gist.github.com/jaredwilli/5469626)
- [Optimising HTML5 Canvas Rendering - AG Grid](https://blog.ag-grid.com/optimising-html5-canvas-rendering-best-practices-and-techniques/)
- [Improving HTML5 Canvas Performance - web.dev](https://web.dev/canvas-performance/)
- [Canvas Layering Optimization - IBM Developer](https://developer.ibm.com/tutorials/wa-canvashtml5layering/)

### Accessibility & Touch Targets
- [WCAG 2.5.5: Target Size - W3C](https://www.w3.org/WAI/WCAG21/Understanding/target-size.html)
- [Looking at WCAG 2.5.5 for Better Target Sizes - CSS-Tricks](https://css-tricks.com/looking-at-wcag-2-5-5-for-better-target-sizes/)
- [Accessible Target Sizes Cheatsheet - Smashing Magazine](https://www.smashingmagazine.com/2023/04/accessible-tap-target-sizes-rage-taps-clicks/)
- [Touch Targets on Touchscreens - Nielsen Norman Group](https://www.nngroup.com/articles/touch-target-size/)

### Timeline UX
- [Evaluating Pan and Zoom Timelines and Sliders - ResearchGate](https://www.researchgate.net/publication/330600782_Evaluating_Pan_and_Zoom_Timelines_and_Sliders)
- [Timeline Zoom Issues - Adobe Community](https://community.adobe.com/t5/premiere-pro-discussions/p-alt-scroll-zoom-behaves-different-on-v24-4-0/m-p/14640605)

### Color Picker UX
- [Designing a Good Color Picker - Medium/Bootcamp](https://bootcamp.uxdesign.cc/designing-a-good-color-picker-4c08573dcb7b)
- [Color Picker UI Design Patterns - Mobbin](https://mobbin.com/glossary/color-picker)

### WinUI Performance
- [WinUI Performance Optimization - Microsoft Learn](https://learn.microsoft.com/en-us/windows/apps/performance/winui-perf)
- [Win2D Canvas Performance Issues - GitHub](https://github.com/microsoft/microsoft-ui-xaml/issues/7290)
- [Win2D CanvasControl Documentation](https://microsoft.github.io/Win2D/WinUI3/html/T_Microsoft_Graphics_Canvas_UI_Xaml_CanvasControl.htm)

### Codebase Analysis
- SceneBuilderPage.xaml.cs (reviewed lines 1-1806)
- SceneBuilderViewModel.cs (reviewed lines 1-1414)
- Current implementation patterns and existing optimizations
