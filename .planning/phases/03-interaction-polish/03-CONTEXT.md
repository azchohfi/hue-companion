# Phase 3: Interaction Polish - Context

**Gathered:** 2026-01-21
**Status:** Ready for planning

<domain>
## Phase Boundary

Timeline interactions feel polished with clear visual affordances and accessibility compliance. Includes hover states for keyframes and playhead, 44x44px minimum playhead hit area (WCAG 2.5.5), time ruler alignment on resize, and visual feedback indicating draggable elements.

</domain>

<decisions>
## Implementation Decisions

### Hover Feedback Style
- Keyframes show glow effect on hover (not brightness or scale)
- Glow color matches the keyframe's own color (cohesive, shows color more)
- Playhead uses thickness change on hover (2px → 4px), not glow (different treatment from keyframes)

### Playhead Visual Treatment
- Red playhead (classic DAW convention, high visibility)
- Triangular handle at top pointing down (Logic Pro style)
- Triangle is filled solid red (bold, easy to spot)
- On hover, only the line thickens — triangle handle stays same size

### Selection Behavior
- Single select only (no multi-select in this phase)
- Selected keyframe shows persistent glow (even when not hovered)
- Clicking empty canvas deselects current selection
- Clicking empty canvas also adds a keyframe (current behavior preserved), new keyframe becomes selected

### Cursor Vocabulary
- Empty track area: Default arrow cursor
- Keyframe hover: Hand/pointer cursor
- Keyframe dragging: Grabbing hand (closed fist)
- Playhead: Same as keyframes (pointer on hover, grabbing when dragging)

### Claude's Discretion
- Exact glow radius/spread for keyframe hover
- Transition duration for hover states
- Exact triangle dimensions for playhead handle
- How selection glow differs visually from hover glow (brightness level, radius)

</decisions>

<specifics>
## Specific Ideas

- Playhead should feel like Logic Pro — red with triangular handle
- Keyframe glow should match keyframe color for cohesive look
- Keep single-select simple — multi-select can be a future phase

</specifics>

<deferred>
## Deferred Ideas

- Multi-select with Shift+Click or drag rectangle — future phase
- Keyboard navigation for keyframes (arrow keys to move between) — future phase

</deferred>

---

*Phase: 03-interaction-polish*
*Context gathered: 2026-01-21*
