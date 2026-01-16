# Scene Builder Design

## Overview

A DAW-style timeline editor for creating custom animated light scenes. Users can visually place keyframes on a timeline, edit colors and transitions, preview on real lights, and save to their scene library.

## Key Design Decisions

- **Keyframe animations only** (v1) - Event-based triggers deferred to future version
- **Looping by default** - Scenes loop seamlessly
- **Per-light tracks** - When created from a room, each light gets its own timeline track with deterministic color mapping
- **Per-room animations** - Multiple rooms can run different animations simultaneously
- **Room Detail integration** - Animations can be triggered from room pages, not just Scenes

---

## Page Layout

```
┌─────────────────────────────────────────────────────────────────────────┐
│  ← Back    Scene Builder                          [Save] [Save As...]   │
├─────────────────────────────────────────────────────────────────────────┤
│  Scene: [Untitled Scene________]    Duration: [16] sec    Room: [Kitchen ▼] │
├───────────────────────────────────────────────────┬─────────────────────┤
│                                                   │                     │
│  TIMELINE AREA                                    │  SIDE PANEL         │
│                                                   │  (appears on        │
│  Light names │ Keyframe tracks                    │   selection)        │
│  ────────────┼──────────────────────────────      │                     │
│  Floor Lamp  │ 🟠─────────🟡─────────🟠          │  ┌───────────────┐  │
│  Ceiling     │ 🔵─────────🔵─────────🟣          │  │ Color         │  │
│  Strip       │ 🟢─────────🟡─────────🟢          │  │ [  picker  ]  │  │
│              │                                    │  │               │  │
│  ────────────┴──────────────────────────────      │  │ Brightness    │  │
│              0s        8s        16s              │  │ ████░░ 80%    │  │
│                        ▲                          │  │               │  │
│                    playhead                       │  │ Transition    │  │
│                                                   │  │ [EaseOut ▼]   │  │
├───────────────────────────────────────────────────┴─────────────────────┤
│  [▶ Play]  [■ Stop]                 [+ Add Keyframe]    🔁 Loop: On     │
└─────────────────────────────────────────────────────────────────────────┘
```

**Key layout elements:**
- **Header**: Scene name, duration, target room selector
- **Timeline**: Light labels (150px) + scrollable canvas with keyframe tracks
- **Side panel**: ~280px, slides in when keyframe selected
- **Footer**: Transport controls and actions

---

## Data Model

### ViewModels

```csharp
public class SceneBuilderViewModel : ObservableObject
{
    public string SceneName { get; set; }
    public double DurationSeconds { get; set; }
    public RoomModel? TargetRoom { get; set; }
    public ObservableCollection<TrackViewModel> Tracks { get; }
    public KeyframeViewModel? SelectedKeyframe { get; set; }
    public double PlayheadPosition { get; set; }
    public bool IsPlaying { get; set; }

    public IRelayCommand PlayCommand { get; }
    public IRelayCommand StopCommand { get; }
    public IRelayCommand SaveCommand { get; }
    public IRelayCommand AddKeyframeCommand { get; }
}

public class TrackViewModel : ObservableObject
{
    public string LightId { get; set; }
    public string DisplayName { get; set; }  // "Floor Lamp" or "Color 1"
    public ObservableCollection<KeyframeViewModel> Keyframes { get; }
}

public class KeyframeViewModel : ObservableObject
{
    public double TimeSeconds { get; set; }
    public HueColor Color { get; set; }
    public double Brightness { get; set; }
    public TransitionStyle Transition { get; set; }
}
```

### Serialization

Scenes serialize to the existing `AnimatedSceneModel` JSON format:

```json
{
  "id": "user_my_sunset",
  "name": "My Sunset",
  "category": "Custom",
  "isBuiltIn": false,
  "defaultTargeting": "room",
  "targetId": "guid-of-source-room",
  "animations": [
    {
      "id": "track_floor_lamp",
      "name": "Floor Lamp",
      "type": "keyframe",
      "targetLightIndices": [0],
      "keyframes": [...],
      "durationSeconds": 16,
      "repeatMode": "loop"
    }
  ]
}
```

---

## Timeline Rendering

Built with `Canvas` inside `ScrollViewer`:

```
┌─────────────────────────────────────────────────────────────────┐
│ ScrollViewer (horizontal scroll for long durations)             │
│ ┌─────────────────────────────────────────────────────────────┐ │
│ │ Grid: [Light Labels Column] [Canvas for tracks/keyframes]   │ │
│ │                                                             │ │
│ │  150px fixed    │  Flexible width based on zoom             │ │
│ │  ──────────────────────────────────────────────────────     │ │
│ │  "Floor Lamp"   │  ●────────────●────────────●              │ │
│ │  "Ceiling"      │  ●────────────●────────────●              │ │
│ │  "Strip"        │  ●────────────●────────────●              │ │
│ └─────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
```

**Rendering:**
- Track rows: ~50px height
- Keyframes: Colored circles at `(timeSeconds / duration) * trackWidth`
- Connections: Gradient lines between keyframes
- Time ruler: Top bar with 1s tick marks
- Playhead: Animated vertical line

**Interactions:**
- Click empty track → Add keyframe at position
- Click keyframe → Select, show side panel
- Drag keyframe → Reposition in time
- Right-click → Context menu (delete, duplicate)

**Zoom:** 20-200 pixels/second, default 50px/sec

---

## Side Panel Editor

Appears when keyframe selected (~280px wide, slides from right):

```
┌─────────────────────────────┐
│  Keyframe @ 8.0s        ✕   │
├─────────────────────────────┤
│                             │
│  Color                      │
│  ┌───────────────────────┐  │
│  │     ColorPicker       │  │
│  │     (compact)         │  │
│  └───────────────────────┘  │
│                             │
│  Brightness                 │
│  ████████████░░░░  80%      │
│                             │
│  Transition                 │
│  ┌─────────────────────▼─┐  │
│  │ Ease In-Out           │  │
│  └───────────────────────┘  │
│                             │
│  ─────────────────────────  │
│                             │
│  [Duplicate]  [Delete]      │
│                             │
└─────────────────────────────┘
```

**Transition options:** Linear, Ease In, Ease Out, Ease In-Out, Instant

---

## Playback

**Flow:**
1. Validate room selected with lights
2. Start playback loop:
   - Animate playhead across timeline
   - Interpolate state for each track at playhead position
   - Send commands to bridge (~100ms intervals)
3. On loop end: Reset playhead to 0, continue
4. Stop: Cancel loop, optionally restore original states

**Rate limiting:** Target 10 updates/sec, batch track updates, skip tiny deltas

---

## Per-Room Animation Support

Multiple rooms can run different animations simultaneously.

**AnimationService changes:**

```csharp
public class AnimationService : IAnimationService
{
    private readonly ConcurrentDictionary<Guid, RunningAnimation> _runningAnimations;

    Task<Result> StartSceneAsync(string sceneId, Guid roomId);
    Task StopSceneInRoomAsync(Guid roomId);
    Task StopAllScenesAsync();
    bool IsAnimationRunning(Guid roomId);
    AnimatedSceneModel? GetRunningScene(Guid roomId);

    event EventHandler<RoomAnimationChangedEventArgs> RoomAnimationChanged;
}
```

---

## Room Detail Integration

Add "Animations" section to RoomDetailPage:

```
┌─────────────────────────────────────────────────┐
│  Living Room                            [On/Off]│
│  5 lights · 75%                                 │
├─────────────────────────────────────────────────┤
│  Scenes                                         │
│  ┌─────────┐ ┌─────────┐ ┌─────────┐           │
│  │ Relax   │ │ Focus   │ │ Bright  │           │
│  └─────────┘ └─────────┘ └─────────┘           │
│                                                 │
│  Animations                    [Browse All →]   │
│  ┌─────────────────────────────────────────┐   │
│  │ ▶ Ocean Waves          [Stop]           │   │
│  └─────────────────────────────────────────┘   │
│  ┌─────────┐ ┌─────────┐ ┌─────────┐           │
│  │ 🔥 Fire │ │ 🌊 Ocean│ │ 🌌 Cosmic│          │
│  └─────────┘ └─────────┘ └─────────┘           │
└─────────────────────────────────────────────────┘
```

---

## Save & Load

**Locations:**
- Built-in: `Assets/Scenes/*.json` (read-only)
- User: `%LocalAppData%/HueWindows/Scenes/*.json`

**Entry points:**
1. "Create New" from Scenes page → Blank scene, pick room
2. "Edit" on existing user scene → Load and populate
3. "Save current room as scene" → Snapshot as first keyframe

---

## Implementation Phases

### Phase 4a: Scaffold & Basic Timeline (4 commits)
1. Create `SceneBuilderPage.xaml` with layout, navigation from Scenes page
2. Add `SceneBuilderViewModel` with properties, track/keyframe models
3. Render static timeline with tracks and keyframe circles
4. Wire up room selector, duration input

### Phase 4b: Keyframe Editing (4 commits)
5. Click keyframe → Select and show side panel
6. Side panel: Color picker, brightness slider, transition dropdown
7. Click track → Add new keyframe at position
8. Delete keyframe (button + Delete key)

### Phase 4c: Drag & Timeline Polish (4 commits)
9. Drag keyframes to reposition in time
10. Time ruler with tick marks
11. Zoom slider
12. Playhead visualization

### Phase 4d: Per-Room Animation Service (3 commits)
13. Refactor `AnimationService` for concurrent per-room animations
14. Add `RoomAnimationChanged` events
15. Update `ScenesPage` to use new service

### Phase 4e: Playback in Builder (3 commits)
16. Play/Stop with animated playhead
17. Send interpolated states to bridge
18. Loop back to start

### Phase 4f: Save/Load (3 commits)
19. Save to user scenes folder
20. Load existing user scene for editing
21. "Create from current room" entry point

### Phase 4g: Room Detail Integration (2 commits)
22. Add "Animations" section to `RoomDetailPage`
23. Now Playing card with Stop, quick-pick grid

---

## Future Considerations

- Event-based triggers (lightning, sparkles)
- Brightness automation lanes
- Snap to grid
- Undo/redo
- Import/export JSON
- BPM/tempo mode for music sync
