# Feature Gap Roadmap

A prioritized plan to bring Hue Companion to feature parity with the official Hue app. Organized by priority tier, with implementation details for each feature.

---

## Tier 1: High Value

### 1. Schedules & Automations

The official Hue app's automation system covers timers, wake/sleep routines, and time-based triggers. The CLIP v2 API exposes these via `smart_scene`, `behavior_script`, and `behavior_instance` resources.

#### 1.1 Automations Page & Navigation

- [ ] Add "Automations" nav item to sidebar (icon: `E916` clock)
- [ ] Create `AutomationsPage.xaml` — list view of all automations
- [ ] Create `AutomationsViewModel.cs` — load/manage automations
- [ ] Create `AutomationItemViewModel.cs` — individual automation state

#### 1.2 Models

- [ ] `AutomationModel` — id, name, type, enabled, schedule, actions
- [ ] `AutomationType` enum — Timer, WakeUp, GoToSleep, TimeOfDay, CustomSchedule
- [ ] `ScheduleModel` — time, recurrence (days of week), fade duration
- [ ] `AutomationAction` — target (room/zone/light), action (scene, on/off, brightness, color)

#### 1.3 Bridge Service Methods

- [ ] `GetAutomationsAsync()` — fetch behavior_instance resources
- [ ] `CreateAutomationAsync(AutomationModel)` — create behavior_instance
- [ ] `UpdateAutomationAsync(AutomationModel)` — update behavior_instance
- [ ] `DeleteAutomationAsync(Guid id)` — delete behavior_instance
- [ ] `SetAutomationEnabledAsync(Guid id, bool enabled)` — toggle on/off

#### 1.4 Wake-Up Routine

- [ ] Create `WakeUpRoutineEditor.xaml` — visual editor
- [ ] Room/zone selector
- [ ] Target scene or color temperature selector
- [ ] Wake-up time picker with day-of-week recurrence
- [ ] Fade-in duration slider (1–30 minutes)
- [ ] Preview: simulate the fade-in

#### 1.5 Go-to-Sleep Routine

- [ ] Create `SleepRoutineEditor.xaml` — visual editor
- [ ] Room/zone selector
- [ ] Starting scene/current state
- [ ] Fade-out duration slider (1–60 minutes)
- [ ] End action: lights off or dim to minimum
- [ ] Time picker with day-of-week recurrence

#### 1.6 Timer / Countdown

- [ ] Create `TimerEditor.xaml`
- [ ] Duration picker (minutes/hours)
- [ ] Action when timer ends: turn off, activate scene, set color
- [ ] Room/zone/light target selector
- [ ] Active timer display with countdown

#### 1.7 Time-of-Day Automations

- [ ] Create `ScheduleEditor.xaml`
- [ ] Time trigger: specific time, sunrise, sunset (with offset)
- [ ] Day-of-week recurrence checkboxes
- [ ] Action: activate scene, turn on/off, set brightness/color
- [ ] Multiple actions per automation

#### 1.8 Automation List UI

- [ ] Toggle switch per automation (enabled/disabled)
- [ ] Next trigger time display
- [ ] Group by type (wake-up, sleep, custom)
- [ ] Context menu: edit, duplicate, delete
- [ ] Empty state with "Create Automation" CTA

---

### 2. Room & Zone Management (CRUD)

Currently rooms and zones are read-only. Users need to manage these from the app.

#### 2.1 Bridge Service Methods

- [ ] `CreateRoomAsync(string name, RoomArchetype archetype)` — POST /resource/room
- [ ] `UpdateRoomAsync(Guid id, string name, RoomArchetype archetype)` — PUT /resource/room/{id}
- [ ] `DeleteRoomAsync(Guid id)` — DELETE /resource/room/{id}
- [ ] `AssignLightToRoomAsync(Guid roomId, Guid lightId)` — update room children
- [ ] `RemoveLightFromRoomAsync(Guid roomId, Guid lightId)` — update room children
- [ ] `CreateZoneAsync(string name, RoomArchetype archetype)` — POST /resource/zone
- [ ] `UpdateZoneAsync(Guid id, string name, RoomArchetype archetype)` — PUT /resource/zone/{id}
- [ ] `DeleteZoneAsync(Guid id)` — DELETE /resource/zone/{id}
- [ ] `GetUnassignedLightsAsync()` — lights not in any room

#### 2.2 Create Room/Zone Dialog

- [ ] `CreateRoomDialog.xaml` — ContentDialog
- [ ] Name text input with validation
- [ ] Archetype picker (grid of icons matching Hue archetypes)
- [ ] Light selector: checkboxes for unassigned lights
- [ ] Room vs Zone toggle

#### 2.3 Edit Room/Zone

- [ ] Edit button on RoomDetailPage header (settings gear icon)
- [ ] `EditRoomDialog.xaml` — pre-populated ContentDialog
- [ ] Rename, change archetype
- [ ] Add/remove lights with drag or checkbox UI
- [ ] Show unassigned lights available to add

#### 2.4 Delete Room/Zone

- [ ] Delete option in room context menu / edit dialog
- [ ] Confirmation dialog with warning about light assignment
- [ ] Navigate back to dashboard after deletion
- [ ] Update nav items after room/zone CRUD operations

#### 2.5 Navigation Updates

- [ ] "Add Room" button on DashboardPage
- [ ] "Add Zone" button on ZonesPage
- [ ] Refresh nav items after create/delete operations
- [ ] Update `MainWindow.xaml.cs` nav item list dynamically

---

### 3. Accessories & Sensors

The Hue ecosystem includes motion sensors, dimmer switches, tap dials, and wall switches. These use the `device`, `motion`, `button`, `relative_rotary`, `temperature`, and `light_level` CLIP v2 resources.

#### 3.1 Models

- [ ] `AccessoryModel` — id, name, type, battery level, firmware version
- [ ] `AccessoryType` enum — MotionSensor, DimmerSwitch, TapDial, WallSwitch, SmartPlug
- [ ] `MotionSensorConfig` — sensitivity, enabled, daylight detection
- [ ] `ButtonConfig` — button event type, action mapping
- [ ] `RotaryConfig` — rotation event, action mapping

#### 3.2 Bridge Service Methods

- [ ] `GetDevicesAsync()` — fetch all devices (lights + accessories)
- [ ] `GetAccessoriesAsync()` — filter to non-light devices
- [ ] `GetMotionSensorStateAsync(Guid id)` — motion, light level, temperature
- [ ] `SetMotionSensorEnabledAsync(Guid id, bool enabled)` — toggle sensor
- [ ] `SetMotionSensorSensitivityAsync(Guid id, int sensitivity)` — adjust sensitivity
- [ ] `GetButtonEventsAsync(Guid id)` — button press history
- [ ] `GetDeviceBatteryAsync(Guid id)` — battery percentage

#### 3.3 Accessories Page

- [ ] Add "Accessories" nav item to sidebar (icon: `E957`)
- [ ] Create `AccessoriesPage.xaml` — grid/list of accessories
- [ ] Create `AccessoriesViewModel.cs`
- [ ] Device cards showing: name, type icon, battery level, last activity
- [ ] Group by type (sensors, switches, dials)

#### 3.4 Motion Sensor Detail

- [ ] Create `MotionSensorDetailPage.xaml`
- [ ] Current state: motion detected, light level (lux), temperature
- [ ] Sensitivity slider
- [ ] Enable/disable toggle
- [ ] Linked behavior: what happens when motion detected
- [ ] Activity history/log

#### 3.5 Button/Switch Configuration

- [ ] Create `SwitchDetailPage.xaml`
- [ ] Visual button map (show physical button layout)
- [ ] Per-button action assignment:
  - Activate scene
  - Toggle room on/off
  - Adjust brightness
  - Cycle through scenes
- [ ] Battery level display
- [ ] Last press time

#### 3.6 Event Stream Integration

- [ ] Subscribe to motion events via SSE
- [ ] Subscribe to button press events via SSE
- [ ] Real-time state updates on detail pages
- [ ] Optional notification when motion detected

---

## Tier 2: Medium Value

### 4. Entertainment Areas & Sync

Entertainment features require the Hue Entertainment Streaming API (DTLS/UDP), which is significantly more complex than the REST API.

#### 4.1 Entertainment Area Management

- [ ] `GetEntertainmentAreasAsync()` — fetch entertainment configurations
- [ ] `CreateEntertainmentAreaAsync()` — create new area
- [ ] `DeleteEntertainmentAreaAsync()` — remove area
- [ ] `EntertainmentAreaPage.xaml` — 2D/3D light position editor
- [ ] Drag lights to position them in the entertainment space

#### 4.2 Screen Sync

- [ ] Screen capture service (Windows.Graphics.Capture API)
- [ ] Color sampling from screen regions
- [ ] Map screen regions to entertainment area lights
- [ ] DTLS streaming connection to bridge
- [ ] Latency-optimized color pipeline
- [ ] Multi-monitor support
- [ ] Configurable: capture region, intensity, color boost

#### 4.3 Music Sync

- [ ] Audio capture service (WASAPI loopback)
- [ ] Beat detection / frequency analysis
- [ ] Map audio features to light effects (bass → color, beat → flash)
- [ ] DTLS streaming to bridge
- [ ] Configurable: sensitivity, color palette, effect style

#### 4.4 Entertainment UI

- [ ] Add "Entertainment" nav item to sidebar
- [ ] `EntertainmentPage.xaml` — area list + sync controls
- [ ] Start/stop sync buttons
- [ ] Sync mode selector (screen, music, game)
- [ ] Performance metrics (latency, frame rate)

> **Note:** Entertainment streaming is a large undertaking requiring DTLS, UDP, and real-time processing. Consider this a stretch goal.

---

### 5. Power-on Behavior

Configure what lights do when physical power is restored.

#### 5.1 Bridge Service Methods

- [ ] `GetPowerOnBehaviorAsync(Guid lightId)` — read powerup preset
- [ ] `SetPowerOnBehaviorAsync(Guid lightId, PowerOnPreset preset)` — set behavior
- [ ] `PowerOnPreset` enum — LastState, DefaultColor, CustomColor, SafetyBright

#### 5.2 UI Integration

- [ ] Add "Power-on Behavior" section to LightDetailPage
- [ ] Preset selector: Last state, Default (warm white), Custom, Safety (bright)
- [ ] Custom: color picker + brightness slider for power-on state
- [ ] Preview of what the light will do
- [ ] Batch apply: set power-on behavior for all lights in a room

---

### 6. Light Configuration

Rename lights and manage their room assignment from the app.

#### 6.1 Bridge Service Methods

- [ ] `RenameLightAsync(Guid lightId, string newName)` — PUT /resource/light/{id}
- [ ] `GetLightCapabilitiesAsync(Guid lightId)` — color gamut, features
- [ ] `IdentifyLightAsync(Guid lightId)` — blink the light to identify it

#### 6.2 UI Integration

- [ ] Add edit button to LightDetailPage header
- [ ] `EditLightDialog.xaml` — rename, view capabilities
- [ ] "Identify" button — blinks the physical light
- [ ] Show light model info: product name, firmware version, capabilities
- [ ] Room assignment: move light to a different room
- [ ] Light capabilities display: color, color temperature ranges, max brightness

---

### 7. Gradient Light Support

Hue gradient lightstrips allow multiple color points along the strip.

#### 7.1 Models

- [ ] `GradientPoint` — position (0.0–1.0), color (HueColor)
- [ ] Extend `LightModel` with `SupportsGradient`, `GradientPointCount`
- [ ] `GradientMode` enum — InterpolatedPalette, InterpolatedPaletteMirrored, RandomPixelated

#### 7.2 Bridge Service Methods

- [ ] `SetGradientAsync(Guid lightId, List<GradientPoint> points)` — set gradient colors
- [ ] `GetGradientAsync(Guid lightId)` — read current gradient
- [ ] `SetGradientModeAsync(Guid lightId, GradientMode mode)` — set interpolation mode

#### 7.3 UI

- [ ] Detect gradient-capable lights in LightDetailPage
- [ ] Gradient editor: horizontal strip with draggable color stops
- [ ] Color picker per stop
- [ ] Add/remove gradient points
- [ ] Mode selector: smooth, mirrored, random
- [ ] Live preview as gradient is edited

---

## Tier 3: Lower Value

### 8. Firmware Updates

Bridge and light firmware update management.

#### 8.1 Bridge Service Methods

- [ ] `CheckForUpdatesAsync()` — query bridge for available updates
- [ ] `GetUpdateStatusAsync()` — current update state
- [ ] `StartBridgeUpdateAsync()` — initiate bridge firmware update
- [ ] `StartLightUpdateAsync(Guid lightId)` — initiate light firmware update
- [ ] `GetDeviceFirmwareVersionAsync(Guid deviceId)` — current firmware

#### 8.2 UI

- [ ] Update notification badge on Settings nav item
- [ ] "Updates" section in SettingsPage
- [ ] List of devices with available updates
- [ ] Update progress indicator
- [ ] Update history / changelog
- [ ] Auto-check on app launch (configurable)

---

### 9. Scene Scheduling

Connect scenes to the automation system.

- [ ] "Schedule" option in scene context menu
- [ ] Quick-schedule dialog: time, recurrence, fade duration
- [ ] Creates a time-based automation targeting the scene
- [ ] "Scheduled" badge on scene cards that have active schedules
- [ ] Link to automation from scene detail

> **Note:** Depends on Automations (1.x) being implemented first.

---

### 10. Backup & Restore

Export and import app configuration.

#### 10.1 App Settings Backup

- [ ] Export app settings to JSON file (pinned items, custom icons, scene assignments)
- [ ] Import app settings from JSON file
- [ ] File picker dialogs for save/load

#### 10.2 Bridge Configuration Backup

- [ ] Export bridge config: rooms, zones, scenes, automations, accessories
- [ ] Warning: bridge restore requires manual re-pairing
- [ ] JSON format with schema version for forward compatibility

#### 10.3 Scene Export/Import

- [ ] Export animated scenes as shareable JSON files
- [ ] Import animated scenes from file
- [ ] Duplicate detection on import

---

## Implementation Order

Recommended order based on user value and dependency chains:

| Phase | Feature | Depends On |
|-------|---------|------------|
| 1 | Room/Zone CRUD (2.x) | — |
| 2 | Light Configuration (6.x) | — |
| 3 | Power-on Behavior (5.x) | — |
| 4 | Accessories & Sensors (3.x) | — |
| 5 | Automations — Models & Service (1.1–1.3) | — |
| 6 | Automations — Wake/Sleep/Timer (1.4–1.6) | Phase 5 |
| 7 | Automations — Full scheduling (1.7–1.8) | Phase 5 |
| 8 | Scene Scheduling (9.x) | Phase 7 |
| 9 | Gradient Lights (7.x) | — |
| 10 | Firmware Updates (8.x) | — |
| 11 | Backup & Restore (10.x) | — |
| 12 | Entertainment & Sync (4.x) | — (large effort) |
