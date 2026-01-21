# Phase 2: Gradient Track Visuals - Context

**Gathered:** 2026-01-20
**Status:** Ready for planning

<domain>
## Phase Boundary

Display continuous color gradient strips on each track showing how light colors evolve between keyframes. The gradient is a true visual representation of what the light will actually look like at any given time. Keyframes remain as markers overlaid on the gradient.

</domain>

<decisions>
## Implementation Decisions

### Gradient Appearance
- Color interpolation should match keyframe transition style (ease-in-out, linear, etc.) — not just linear RGB blend
- Rounded pill shape for gradient strip edges (Logic Pro style)
- Subtle dark border (1px) around gradient strip
- Full track height (50px) — gradient fills entire track vertically
- Fully opaque colors, no transparency

### Layer Relationship
- Keyframes render on top of gradient (clearly visible)
- Keyframes get white or dark outline for contrast against any gradient color
- Selected keyframes keep current behavior (larger + highlight border)

### Segment Handling
- Before first keyframe: extend first keyframe's color backward to 0s
- After last keyframe: extend last keyframe's color to end of duration
- Gradient shows hue only — brightness not reflected in gradient visualization
- Same-color adjacent keyframes show solid block (no artificial variation)

### Visual Reference
- Logic Pro aesthetic: rounded region shapes, color vibrancy, overall polish
- Gradient should accurately represent the actual color of the light at any given time
- Pill shape with rounding at 0s and duration end (full timeline width)

### Claude's Discretion
- Exact corner radius for pill shape
- Border color (dark enough to contrast but not harsh)
- How to handle very short segments (< few pixels wide)
- Performance optimizations for gradient rendering

</decisions>

<specifics>
## Specific Ideas

- Logic Pro's colored region bars are the reference — pill-shaped, vibrant, polished
- The gradient is functional, not decorative: it shows what the light will actually do
- Keyframe transition styles (ease-in-out, linear) should shape how colors blend in the gradient

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

</deferred>

---

*Phase: 02-gradient-track-visuals*
*Context gathered: 2026-01-20*
