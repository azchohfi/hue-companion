# Phase 1: Win2D Foundation - Context

**Gathered:** 2026-01-20
**Status:** Ready for planning

<domain>
## Phase Boundary

Migrate Scene Builder timeline rendering from XAML Canvas to Win2D CanvasControl for GPU acceleration. Establish layered architecture with separate render passes for ruler, tracks, keyframes, and playhead. Ensure correct display on high-DPI screens.

This is rendering infrastructure — no new features, just better foundation for polish features in later phases.

</domain>

<decisions>
## Implementation Decisions

### Claude's Discretion

User expressed confidence in Claude's technical judgment for this infrastructure phase. The following are explicitly left to Claude's discretion:

- **Layer separation strategy** — How to divide rendering responsibilities between layers, which layers invalidate independently
- **Migration approach** — Incremental port vs clean rewrite, how to preserve existing interaction logic
- **Component architecture** — Renderer class structure, coupling to ViewModel, resource management patterns
- **Performance measurement** — How to verify 60fps target, frame budget allocation, profiling approach

### Target Constraints

- 60fps rendering during playback (non-negotiable)
- Must maintain all existing functionality (keyframe click/drag, playhead scrub, zoom, snap)
- Win2D is already in dependencies — use it

</decisions>

<specifics>
## Specific Ideas

No specific requirements — open to standard Win2D patterns. Research indicated:
- Use CanvasControl.Draw event for rendering
- CreateResources event for resource caching
- Geometry realizations for repeated shapes
- Separate invalidation for playhead during playback

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

</deferred>

---

*Phase: 01-win2d-foundation*
*Context gathered: 2026-01-20*
