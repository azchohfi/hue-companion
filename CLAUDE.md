# CLAUDE.md - Project Context

## Build Commands

```bash
# Development workflow (preferred)
.\hue dev              # Build and launch dev version
.\hue dev --restart    # Kill existing dev, rebuild, launch
.\hue dev --no-launch  # Build only, don't launch

# Stable builds (for side-by-side comparison)
.\hue cut              # Cut new stable from current code
.\hue cut --message "reason"  # Cut with note
.\hue stable           # Launch existing stable build

# Process management
.\hue list             # Show running instances (dev/stable)
.\hue kill dev         # Kill dev instances only
.\hue kill stable      # Kill stable instances only

# Direct dotnet commands (if needed)
dotnet build src/HueCompanion/HueCompanion.csproj -p:Platform=x64
dotnet build src/HueCompanion/HueCompanion.csproj -p:Platform=x64 -p:DevBuild=true
```

Dev builds display "Hue Companion (Dev)" in title bar with git hash version.
Stable builds display "Hue Companion" with date-based version.
Both can run simultaneously (separate MSIX identities).

## Architecture

**WinUI 3 + MVVM** - Standard MVVM pattern using CommunityToolkit.Mvvm.

```
src/
├── HueCompanion/              # WinUI 3 App (views, controls, styles)
├── HueCompanion.Core/         # Business logic (models, viewmodels, services)
├── HueCompanion.Mcp/          # MCP server (stdio + HTTP transport)
└── HueCompanion.Tests/        # Unit tests
```

## Key Files

| File | Purpose |
|------|---------|
| `Views/DashboardPage.xaml` | Main home page with room/zone cards |
| `Views/RoomDetailPage.xaml` | Room detail with scenes and lights |
| `Views/LightDetailPage.xaml` | Individual light control (color/temp) |
| `Views/SceneBuilderPage.xaml` | DAW-style timeline editor for animated scenes |
| `Views/ScenesPage.xaml` | Scene library with built-in and user scenes |
| `Controls/RoomCard.xaml` | Widget card for rooms/zones on dashboard |
| `Controls/LightCard.xaml` | Light row card with toggle and color indicator |
| `Styles/AppStyles.xaml` | Global styles and theme resource overrides |
| `Converters/BoolToOpacityConverter.cs` | XAML converters including gradient converters |
| `Constants/AppConstants.cs` | Animation durations, color values |
| `Core/Services/HueBridgeService.cs` | Hue API communication layer |
| `Core/Services/AnimationEngine.cs` | Runs animated scenes on lights |
| `Core/Services/AnimationService.cs` | Manages scene library and playback |
| `Core/Services/SceneStorageService.cs` | Saves/loads user scenes to JSON |
| `Core/ViewModels/DashboardViewModel.cs` | Dashboard data and commands |
| `Core/ViewModels/RoomDetailViewModel.cs` | Room/zone detail with lights and scenes |
| `Core/ViewModels/LightDetailViewModel.cs` | Individual light control state |
| `Core/ViewModels/SceneBuilderViewModel.cs` | Scene Builder state and keyframe editing |
| `Core/Models/AnimationDefinition.cs` | Animation types, keyframes, event patterns |
| `Core/Models/AnimatedSceneModel.cs` | Complete scene with multiple animations |
| `Mcp/Program.cs` | MCP server entry point (stdio + HTTP modes) |
| `Mcp/Tools/LightTools.cs` | MCP tools for light/room control |
| `Mcp/Tools/SceneTools.cs` | MCP tools for scene management |
| `Mcp/Tools/AnimationTools.cs` | MCP tools for animated scenes |
| `Mcp/Services/ColorParser.cs` | Parses hex/RGB/named colors to HueColor |
| `Mcp/Services/FuzzyMatcher.cs` | Fuzzy name matching for lights and rooms |
| `Services/McpServerManager.cs` | Manages MCP server process lifecycle |

## Styling Approach

Uses **WinUI 3 lightweight styling** - override theme resources rather than re-templating controls.

```xaml
<!-- Global theme resource override in AppStyles.xaml -->
<SolidColorBrush x:Key="ToggleSwitchFillOff" Color="#30FFFFFF"/>

<!-- Slider thumb size overrides -->
<x:Double x:Key="SliderHorizontalThumbWidth">22</x:Double>
<x:Double x:Key="SliderHorizontalThumbHeight">22</x:Double>
```

Key theme resources used:
- `ToggleSwitchFillOn/Off` - Track fill colors
- `ToggleSwitchKnobFillOn/Off` - Knob colors
- `SliderHorizontalThumbWidth/Height` - Larger touch targets
- `CardBackgroundFillColorDefaultBrush` - Card backgrounds

### GradientToggleSwitchStyle

Custom toggle style used on RoomDetailPage and LightDetailPage headers. Uses `Background` property for colored fill when on.

**Important:** This style requires explicit `Tapped` handler to toggle - the built-in toggle events don't fire reliably:
```xaml
<ToggleSwitch Style="{StaticResource GradientToggleSwitchStyle}"
              IsOn="{x:Bind ViewModel.IsOn, Mode=TwoWay}"
              Tapped="Toggle_Tapped"/>
```

## Detail Page Design Pattern

RoomDetailPage and LightDetailPage share a consistent design:

1. **Card-style header** with dark gradient background (`#1A1A1E` to `#1E1E22`)
2. **Colored border** that animates based on light/room color (cross-fade between two borders)
3. **Icon** that animates color when on/off state changes
4. **Brightness card** below header with same styling
5. **Debounced sliders** (150ms) to prevent API spam during drag

### Border Cross-Fade Animation
Two overlapping borders alternate to create smooth color transitions:
```csharp
var newBorder = _useFirstBorder ? Border1 : Border2;
var oldBorder = _useFirstBorder ? Border2 : Border1;
newBorder.BorderBrush = new SolidColorBrush(newColor);
AnimateBorderOpacity(newBorder, 1.0);
AnimateBorderOpacity(oldBorder, 0.0);
_useFirstBorder = !_useFirstBorder;
```

### Slider Debouncing
All sliders use DispatcherTimer debouncing to send commands only after user stops dragging:
```csharp
_debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
_debounceTimer.Tick += (s, e) => {
    _debounceTimer.Stop();
    ViewModel.Command.Execute(_pendingValue);
};
// On value change: stop timer, start timer
```

## Scene Cards

Scene cards display palette colors with gradient backgrounds:
- `HexToGradientBackgroundConverter` - Creates subtle gradient from scene color
- `HexToGradientBorderConverter` - Creates border brush for active indicator
- Color swatches (up to 4 ellipses) show scene palette

## Navigation

- `MainWindow.xaml` hosts NavigationView with rooms/zones in nav items
- Rooms and zones both use `RoomDetailPage` (zones are just grouped lights)
- Navigation parameters use `NavigationTag` record with Id and Type
- `NavigationHelper.cs` manages page navigation
- Deep linking supported via command-line args

## Hue API

Uses **HueApi** NuGet package (CLIP v2 API).

```csharp
// Key service methods in HueBridgeService.cs
GetRoomsAsync()           // Returns rooms with lights
GetZonesAsync()           // Returns zones with lights
GetScenesForRoomAsync()   // Returns scenes for a room
GetScenesForZoneAsync()   // Returns scenes for a zone
SetLightOnAsync()         // Toggle individual light
SetRoomOnAsync()          // Toggle entire room
SetLightColorAsync()      // Set light color (HueColor)
SetLightTemperatureAsync() // Set color temperature (mirek)
```

## Data Flow

1. `DashboardViewModel.LoadRoomsAsync()` fetches rooms and zones
2. Creates `RoomCardViewModel` for each
3. `RoomCard` control binds to viewmodel
4. Toggle/brightness changes call commands on viewmodel
5. ViewModel calls `HueBridgeService` to update Hue bridge
6. Event streaming updates UI in real-time

## Animation Constants

Defined in `Constants/AppConstants.cs`:
- `StandardDurationMs` = 300ms - Default animation duration
- `InactiveIconAlpha` = 128 - Icon opacity when light is off

## Scene Builder

DAW-style timeline editor for creating animated light scenes.

### Architecture

```
SceneBuilderPage.xaml.cs          # UI rendering, input handling
    └── SceneBuilderViewModel     # State, keyframes, event tracks
            ├── TrackViewModel         # One per light
            │   └── KeyframeViewModel  # Color/brightness at time
            └── EventTrackViewModel    # Random effects (lightning, etc.)
```

### Key Concepts

**Tracks**: One `TrackViewModel` per light in the selected room. Each track has a collection of `KeyframeViewModel` objects sorted by time.

**Keyframes**: Define color, brightness, and transition style at a specific time. Click canvas to add, drag to move, right-click to delete.

**Event Tracks**: `EventTrackViewModel` for random effects. Uses `EventPreset` enum (LightningFlash, Sparkle, CandleFlicker) with frequency slider.

**Snap to Grid**: Zoom-adaptive intervals (2s → 1s → 0.5s → 0.25s). Hold Ctrl to temporarily disable.

### Timeline Rendering

```csharp
// RenderTimeline() in SceneBuilderPage.xaml.cs
1. Clear canvas
2. Render time ruler ticks
3. Render track separators
4. Render snap grid lines (if enabled)
5. Render keyframes as colored circles
6. Render event tracks with dashed pattern
7. Render playhead line
```

### Playback

- 60fps timer updates `PlayheadPosition`
- `UpdatePlayheadPosition()` moves playhead visuals efficiently
- Light updates rate-limited to 10Hz via `_lastLightUpdateTime`
- Event triggers checked each frame, visual pulses shown when fired

### Save Format

Scenes save as `AnimatedSceneModel` JSON in `%LOCALAPPDATA%/HueCompanion/Scenes/`:

```json
{
  "Id": "user_abc123",
  "Name": "My Scene",
  "Animations": [
    {
      "Type": "Keyframe",
      "Keyframes": [...]
    },
    {
      "Type": "Event",
      "EventPattern": { "Triggers": [...], "MinIntervalSeconds": 3 }
    }
  ]
}
```

### Adding New Event Presets

1. Add enum value to `EventPreset` in `SceneBuilderViewModel.cs`
2. Add trigger states in `GetPresetTriggers()` method
3. Add display name in `OnPresetChanged()` partial method
4. Add icon glyph in `IconGlyph` property
5. Add menu item in `SceneBuilderPage.xaml` (Add Event flyout)
6. Add click handler in `SceneBuilderPage.xaml.cs`

### Keyboard Shortcuts

- **Space** - Play/Pause
- **Ctrl+Click** - Add keyframe without snap
- **Ctrl+Drag** - Move keyframe without snap

## MCP Server

Model Context Protocol server that exposes Hue light control to AI assistants (Claude Desktop, Claude Code, VS Code Copilot).

### Architecture

```
HueCompanion.Mcp/
├── Program.cs                    # Entry point: --stdio (for AI clients) or HTTP (localhost:5680)
├── Tools/
│   ├── LightTools.cs             # hue_list_rooms, hue_get_light, hue_set_light, hue_set_room, hue_turn_off_all
│   ├── SceneTools.cs             # hue_list_scenes, hue_activate_scene, hue_create_scene, hue_delete_scene
│   ├── AnimationTools.cs         # hue_create_animated_scene, hue_play_animation, hue_stop_animation, hue_edit_scene
│   └── BridgeTools.cs            # hue_list_bridges
└── Services/
    ├── ColorParser.cs            # Parses hex/RGB/named colors → HueColor
    ├── FuzzyMatcher.cs           # Case-insensitive partial name matching
    └── FileSettingsService.cs    # Reads app settings from %LOCALAPPDATA%/HueCompanion
```

### Transport Modes

- **stdio** (`--stdio` flag): Used by Claude Desktop and Claude Code. AI client launches the exe as a child process.
- **HTTP** (default): Runs on `https://localhost:5680`. Used by VS Code and for development. Requires the Hue Companion app to be running.

### In-App Integration

- `McpServerManager` (in HueCompanion project) manages the HTTP server as a child process
- Toggle in Settings enables/disables the server
- Settings page has copy buttons that generate config JSON with the correct exe path
- "Open guide" button links to the setup guide on the website

### Adding New MCP Tools

1. Create a new `[McpServerToolType]` class in `Tools/` (or add methods to existing)
2. Add `[McpServerTool]` attribute with Name and Description
3. Inject services via constructor (e.g., `IMultiBridgeService`)
4. Return JSON result strings (not exceptions) for errors
5. Use `FuzzyMatcher` for room/light name resolution
6. Use `ColorParser` for color input handling
