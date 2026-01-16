# Hue SDK Capabilities & Development Roadmap

## Current State

**SDK Version:** HueApi 1.5.1 + HueApi.ColorConverters 1.5.0
**Latest Available:** HueApi 3.1.2 (significant API changes)

### Currently Implemented
- Bridge discovery, pairing, credential storage
- Room/Zone browsing and control (on/off, brightness)
- Individual light control (on/off, brightness, color, temperature)
- Scene discovery and activation with palette previews
- Real-time event streaming
- Custom dashboard with pinnable items
- Theme settings
- Delightful animations (page transitions, scene pulse, staggered entrance, connected animations)
- Color picker with scale/fade flyout animation
- Color continuity during page transitions

### User Hardware
- Gradient lightstrips
- Motion/temperature sensors
- Dimmer switches and buttons

---

## Near-Term: UI Polish

### Light Card Color Controls
- Add color SplitButton to light cards on room detail page
- Same pattern as room header color button
- Only show for lights that support color

### Animation Polish

| Feature | Description |
|---------|-------------|
| **Brightness Drag Feedback** | Card scales 1.02x during drag-to-brightness gesture with visual indicator |
| **Composition Implicit Animations** | Elements auto-animate property changes (Translation, Scale, Opacity) |

---

## Feature Roadmap

### Phase 1: Foundation (SDK Upgrade)
**Goal:** Modernize SDK and establish patterns for new features

| Task | Description |
|------|-------------|
| Upgrade to HueApi 3.1.2 | New fluent API pattern, .NET 8+ support |
| Migrate service layer | Update all API calls to `_hueApi.Light.GetAllAsync()` pattern |
| Verify existing features | Full regression testing |

**API Migration Example:**
```csharp
// Old (1.5.1)                      // New (3.1.2)
_hueApi.GetLightsAsync()       →    _hueApi.Light.GetAllAsync()
_hueApi.UpdateLightAsync()     →    _hueApi.Light.UpdateAsync()
_hueApi.GetRoomsAsync()        →    _hueApi.Room.GetAllAsync()
_hueApi.RecallSceneAsync()     →    _hueApi.Scene.RecallAsync()
```

---

### Phase 2: Light Effects & Gradients
**Goal:** Enhanced light control for capable bulbs

| Feature | Description | Files |
|---------|-------------|-------|
| **Light Effects** | Candle, fireplace, prism, loop, sparkle | `LightModel.cs`, `LightDetailPage.xaml` |
| **Effect Picker UI** | Segmented control for effect selection | `LightDetailViewModel.cs` |
| **Gradient Support** | Multi-point color control for gradient lights | New `GradientEditor` control |
| **Smart Scenes** | Indicator for dynamic/time-based scenes | `RoomDetailPage.xaml` |

**New Service Methods:**
- `SetLightEffectAsync(Guid lightId, LightEffect effect)`
- `SetLightGradientAsync(Guid lightId, GradientPoint[] points)`

---

### Phase 3: Sensors & Accessories
**Goal:** Visibility into all Hue devices

| Feature | Description | Files |
|---------|-------------|-------|
| **Sensors Page** | View motion, light level, temperature sensors | New `SensorsPage.xaml` |
| **Battery Status** | Power levels for wireless devices | Badge on device cards |
| **Button/Remote Status** | Dimmer switch, smart button last events | New `AccessoriesPage.xaml` |
| **Event Stream Extensions** | Parse sensor and button events | `HueBridgeService.cs` |

**New Models:**
- `MotionSensorModel` - motion detected, sensitivity, battery
- `LightLevelSensorModel` - lux value, dark threshold
- `TemperatureSensorModel` - temperature in celsius
- `ButtonModel` - last event type, timestamp, battery

---

### Phase 4: Home Editing
**Goal:** Full configuration management (not just control)

| Feature | Description | Complexity |
|---------|-------------|------------|
| **Create/Edit Rooms** | Add rooms, rename, assign lights | Medium |
| **Create/Edit Zones** | Custom cross-room light groupings | Medium |
| **Create/Edit Scenes** | Design scenes with color picker | High |
| **Reorder/Organize** | Custom ordering, hide unused items | Low |

**New Service Methods:**
- `CreateRoomAsync(string name, RoomArchetype archetype, Guid[] lightIds)`
- `UpdateRoomAsync(Guid roomId, string name, Guid[] lightIds)`
- `DeleteRoomAsync(Guid roomId)`
- `CreateZoneAsync(string name, Guid[] lightIds)`
- `CreateSceneAsync(Guid roomId, string name, SceneLightState[] states)`
- `UpdateSceneAsync(Guid sceneId, SceneLightState[] states)`

**New UI:**
- Room/Zone editor dialog
- Scene designer with live preview
- Drag-and-drop light assignment

---

### Phase 5: Future (Lower Priority)
| Feature | Description | Status |
|---------|-------------|--------|
| Entertainment Streaming | Screen/music sync | Maybe later |
| Automation Rules | View/manage behaviors | Future |
| Schedules | Time-based automation | Future |

---

## Key Files

| File | Purpose |
|------|---------|
| `Core/HueWindows.Core.csproj` | Package references (SDK upgrade) |
| `Core/Services/HueBridgeService.cs` | All API interactions |
| `Core/Services/Interfaces/IHueBridgeService.cs` | Service contract |
| `Core/Models/LightModel.cs` | Light properties (effects, gradient) |
| `Core/Models/SensorModel.cs` | New sensor models |
| `Views/LightDetailPage.xaml` | Effect picker, gradient editor |
| `Views/SensorsPage.xaml` | New sensors view |
| `Views/RoomEditorPage.xaml` | New room/zone editor |
| `Views/SceneEditorPage.xaml` | New scene designer |

---

## Verification Strategy
- Build and run after each phase
- Test existing functionality (regression)
- Use `mcp__hue-screenshots__capture_screenshot` for UI validation
- Test with real bridge and varied hardware

---

## Sources
- [HueApi NuGet 3.1.2](https://www.nuget.org/packages/HueApi/)
- [Q42.HueApi GitHub](https://github.com/michielpost/Q42.HueApi)
- [HueApi.Entertainment](https://www.nuget.org/packages/HueApi.Entertainment)
- [OpenHAB Hue Binding](https://www.openhab.org/addons/bindings/hue/doc/readme_v2.html)
