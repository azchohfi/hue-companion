# Remaining Scene Features - Design Document

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Complete the remaining features from GitHub issue #3 for animated scenes and scene management.

**Scope:** Save room as scene, static scene editing, scene assignment, thumbnails/previews, expanded library.

---

## 1. Save Current Room as Scene

**Location:** Room Detail Page header.

**Flow:**
1. User taps "Save as Scene" button
2. Dialog with name input (pre-filled: "Room Name - Custom")
3. Call `HueApi.Scene.CreateAsync()` with current light states
4. Scene appears in room's scene list

**Implementation:**
- Add `SaveRoomAsSceneAsync(RoomModel room, string sceneName)` to `HueBridgeService`
- Capture each light's color, brightness, on/off state
- Create scene via CLIP v2 API with `SceneType.Regular`

**UI:**
- Button in `RoomDetailPage.xaml` header: "Save as Scene" (floppy disk icon)
- `ContentDialog` for name input

---

## 2. Static Scene Editing

**Scope:** Edit user-created scenes, duplicate built-in scenes.

**Scene List:**
- User-created: "Edit" and "Delete" buttons
- Built-in: "Duplicate" button only
- Visual distinction between user vs built-in

**Edit Flow:**
1. Tap "Edit" → opens scene editor
2. Per-light controls: color picker, brightness, on/off
3. "Save" updates bridge, "Cancel" discards

**Duplicate Flow:**
1. Tap "Duplicate" → creates "Scene Name (Copy)"
2. Opens editor immediately

**Implementation:**
- `UpdateSceneAsync(sceneId, lightStates)` in `HueBridgeService`
- `DuplicateSceneAsync(sceneId, newName)` in `HueBridgeService`
- New `StaticSceneEditorPage.xaml` - grid of light cards with controls
- Reuse `ColorPickerFlyout`

---

## 3. Scene Assignment (Animated Scenes to Rooms)

**Concept:** Pin animated scenes to rooms for quick access.

**Storage:**
- `%LocalAppData%/HueCompanion/room-scene-assignments.json`
- Maps `roomId` → list of `animatedSceneId`s

**UI - Assigning:**
- After playing scene: "Pin to [Room Name]?"
- Or: Right-click → "Assign to Room..." → picker

**UI - Room Detail:**
- "Pinned Animations" section below static scenes
- Play/Remove buttons per scene

**Implementation:**
- New `RoomSceneAssignmentService` - load/save JSON
- Update `RoomDetailViewModel` to load assignments
- Update `ScenesPage` with assignment prompt

---

## 4. Scene Thumbnails & Animated Previews

**Color Swatches (Baseline):**
- Display `paletteColors` as 2-4 circles on cards
- Consistent placement

**Animated Mini-Preview (Experimental):**
- 80x40px canvas on scene cards
- 15fps keyframe animation
- Only animates when visible
- Limit 3-4 concurrent animations

**Performance:**
- CompositionAPI for hardware acceleration
- Visibility-aware ticking
- Settings toggle: "Animate scene previews"

**Card Layout:**
```
┌─────────────────────────┐
│ [Animated Preview]      │
│ Scene Name              │
│ Description...          │
│ ● ● ● ● (palette)       │
└─────────────────────────┘
```

**Implementation:**
- New `AnimatedPreviewControl.xaml`
- Takes `AnimatedSceneModel`, renders to canvas
- Pause when scrolling/off-screen

---

## 5. Expanded Scene Library

**New Scenes (~15 total):**

| Category | Scenes |
|----------|--------|
| Ambient | Lava Lamp, Fireflies |
| Seasonal | Christmas Twinkle, Halloween Spooky, Autumn Warmth |
| Nature | Sunrise, Sunset, Rainforest |
| Party | Disco, Date Night, Chill Lounge |
| Focus | Deep Work, Reading, Meditation |
| Entertainment | Movie Night, Gaming RGB |

**Process:**
1. Generate via `tools/scene-generator-prompt.md`
2. Save to `tools/generated/`
3. Test on hardware, tweak
4. Move to `src/HueCompanion/Assets/Scenes/`

**Naming:** `scene_<snake_case>.json`

---

## Out of Scope

- Animation persistence across app restart
- User-facing LLM generator
- Community scene sharing

---

## Implementation Order

1. Save room as scene (simplest, immediate value)
2. Static scene editing (builds on #1)
3. Expanded scene library (can run in parallel)
4. Scene assignment (depends on having more scenes)
5. Animated previews (polish, can be last)
