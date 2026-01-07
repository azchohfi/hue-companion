# HueWindows - Future Improvements & Feature Ideas

This document captures ideas for improving code structure, extensibility, usability, and adding delightful features. Items are organized by category with priority indicators.

**Priority Legend**: 🔴 High | 🟡 Medium | 🟢 Low

---

## Code Structure & Extensibility

### ✅ ~~Re-enable Drag-to-Brightness Gesture~~ (FIXED)
**Location**: `src/HueWindows/Controls/RoomCard.xaml.cs`

~~The signature drag-to-brightness interaction is disabled due to a crash.~~

**Solution applied**: Switched from raw pointer events to `ManipulationDelta` events which handle pointer capture internally. See `TODO-drag-crash.md` for details.

**Related constants** (defined in RoomCard.xaml.cs):
- `DragThreshold = 10` pixels
- `ThrottleMs = 100` milliseconds
- `PixelsPerPercent = 3`

---

### ✅ ~~Implement Real-time Event Stream~~ (FIXED)
**Location**: `src/HueWindows.Core/Services/HueBridgeService.cs`

~~`StartEventStreamAsync()` is stubbed but infrastructure exists.~~

**Solution applied**: Implemented SSE event stream using HueApi's `OnEventStreamMessage` event. Parses light state changes (on/off, brightness, color) from `ExtensionData` and fires `LightStateChanged` events. Stream starts automatically on bridge connection.

---

### 🟡 Add Result Pattern to Services
**Current behavior**: Services return `null` or empty collections on failure, hiding error context.

```csharp
// Current
public async Task<IEnumerable<RoomModel>> GetRoomsAsync()
    => /* returns empty on error */

// Recommended
public async Task<Result<IEnumerable<RoomModel>>> GetRoomsAsync()
    => /* returns Result<T> with error message */
```

**Benefits**:
- Better error messages in UI
- Distinguishes "no rooms" from "failed to load"
- Enables retry logic with context

---

### 🟡 Extract Remaining Magic Numbers
**Locations to audit**:
- Color adjustment `±20` in `RoomCard.xaml.cs:UpdateToggleColor()`
- Animation durations scattered in XAML
- Layout thresholds (600px compact, 1000px expanded)

**Recommendation**: Create `src/HueWindows/Constants/AppConstants.cs`

---

### 🟢 Extract Color Logic to Dedicated Service
**Current location**: `src/HueWindows.Core/Models/HueColor.cs`

Complex color math (CIE xy ↔ RGB, gamma correction, luminance) could be:
- Easier to test in isolation
- Extended for color theme generation
- Optimized with precomputed lookup tables

```csharp
public interface IColorService
{
    Color XyToRgb(double x, double y, double brightness);
    (double x, double y) RgbToXy(Color color);
    double CalculateLuminance(Color color);
    Color[] GenerateHarmony(Color baseColor, HarmonyType type);
}
```

---

## Usability Improvements

### ✅ ~~Scene Preview Colors~~ (IMPLEMENTED)
**Location**: `src/HueWindows.Core/Models/SceneModel.cs`

~~`SceneModel` has a `Colors` property but it's not populated from the API.~~

**Solution applied**: Scene palette colors are now parsed from the Hue API `scene.Palette.Color` or falling back to `scene.Actions[].Action.Color`. Up to 4 color swatches are displayed as colored circles below scene names in the UI.

**Note on scene images**: The Hue API exposes a `metadata.image` property with a `public_image` resource ID, but the actual images are hosted on Philips's cloud servers, not accessible via the bridge API. Scene images cannot be reliably retrieved.

---

### 🔴 Improve Empty States
**Current**: Generic "No rooms found" message

**Recommended**: Actionable empty states with context
```
No rooms found

Your Hue bridge is connected but has no rooms configured.
[Open Hue App] [Refresh] [Setup Guide →]
```

**Locations**:
- `DashboardPage.xaml` - No rooms/zones
- `RoomDetailPage.xaml` - No scenes/lights
- `SettingsPage.xaml` - No bridge configured

---

### 🟡 Add Transition Animations
**Current**: No page transitions (instant swap)

**Recommended Connected Animations**:
- Room card → Room detail page (card expands to fill screen)
- Light item → Light detail page
- Scene activation (color ripple effect from button)

**WinUI 3 API**: `ConnectedAnimationService`

---

### 🟡 Add Loading Skeletons
**Current**: Spinner with "Loading..." text

**Recommended**: Skeleton placeholders matching content shape
- Gray pulsing rectangles for room cards
- Maintains layout stability during load
- Feels faster than spinner

---

### 🟡 Haptic/Visual Feedback for Brightness Gestures
When drag-to-brightness is re-enabled:
- Subtle scale animation on drag start (1.0 → 1.02)
- Progress ring visualization around the card
- Haptic feedback at 0% and 100% bounds (if supported)
- Tooltip showing percentage during drag

---

### 🟢 Quick Actions on Room Cards
Add swipe gestures or long-press context menu:
- **Swipe left**: Quick scene picker flyout
- **Swipe right**: All off / All on toggle
- **Long-press**: Pin to Quick Access / Favorite

---

## Product Delight Features

### 🔴 Global Hotkey + Quick Access Overlay
**Priority**: High impact, medium effort

System-wide hotkey (e.g., `Win+Shift+H`) opens an always-on-top overlay with pinned rooms and scenes for instant control without switching apps.

#### Architecture
```
┌─────────────────────────────────────────────────────────────┐
│                        App.xaml.cs                          │
│  ┌─────────────────┐  ┌──────────────────┐                 │
│  │ GlobalHotkey    │  │ QuickAccessWindow │                │
│  │ Service         │──│ (Overlay)         │                │
│  └─────────────────┘  └──────────────────┘                 │
│         │                      │                            │
│         │ RegisterHotKey       │ Shows pinned rooms/scenes │
│         │ (Win32 P/Invoke)     │ Always-on-top, borderless │
│         ▼                      ▼                            │
│  ┌─────────────────────────────────────────┐               │
│  │            SettingsService              │               │
│  │  - PinnedRoomIds: List<Guid>            │               │
│  │  - HotkeyModifier: ModifierKeys         │               │
│  │  - HotkeyKey: VirtualKey                │               │
│  └─────────────────────────────────────────┘               │
└─────────────────────────────────────────────────────────────┘
```

#### Overlay UI Design
```
┌──────────────────────────────────────┐
│  💡 Quick Access            ✕       │  ← Compact header
├──────────────────────────────────────┤
│  ┌──────────┐  ┌──────────┐         │
│  │ 🛋️       │  │ 🍳       │         │  ← Pinned rooms (mini cards)
│  │ Living   │  │ Kitchen  │         │
│  │ ●━━━━━○  │  │ ●━━━━━○  │         │  ← Toggle + brightness
│  │   75%    │  │   50%    │         │
│  └──────────┘  └──────────┘         │
│                                      │
│  ── Scenes ──────────────────────── │
│  [🌅 Relax] [🌙 Night] [💡 Bright]  │  ← Quick scene buttons
│                                      │
│  [Open Full App]           [⚙️]     │  ← Footer actions
└──────────────────────────────────────┘
```

#### New Files Required
| File | Purpose |
|------|---------|
| `Services/GlobalHotkeyService.cs` | P/Invoke hotkey registration |
| `Services/IGlobalHotkeyService.cs` | Interface for DI |
| `QuickAccessWindow.xaml/.cs` | Overlay window UI |
| `ViewModels/QuickAccessViewModel.cs` | Pinned items data binding |
| `Controls/MiniRoomCard.xaml` | Compact room card for overlay |
| `Models/HotkeyConfig.cs` | Hotkey settings model |

#### GlobalHotkeyService Implementation
```csharp
using System.Runtime.InteropServices;
using WinRT.Interop;

public interface IGlobalHotkeyService : IDisposable
{
    event EventHandler? HotkeyPressed;
    bool Register(ModifierKeys modifiers, VirtualKey key);
    void Unregister();
}

public class GlobalHotkeyService : IGlobalHotkeyService
{
    private const int WM_HOTKEY = 0x0312;
    private const int HOTKEY_ID = 9000;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    // Subclass window to intercept WM_HOTKEY messages
    // Fire HotkeyPressed event when detected
}
```

#### Settings Model Extension
```csharp
public class AppSettings
{
    // Existing
    public BridgeModel? ConfiguredBridge { get; set; }
    public AppTheme Theme { get; set; } = AppTheme.System;

    // New - Pinned Items
    public List<Guid> PinnedRoomIds { get; set; } = new();
    public List<Guid> PinnedSceneIds { get; set; } = new();

    // New - Hotkey Configuration
    public HotkeyConfig QuickAccessHotkey { get; set; } = new()
    {
        Modifiers = ModifierKeys.Windows | ModifierKeys.Shift,
        Key = VirtualKey.H  // H for Hue
    };
    public bool HotkeyEnabled { get; set; } = true;
}
```

#### Hotkey Conflict Warnings
Common conflicts to detect and warn about:
- `Win+H` - Windows Dictation
- `Win+Shift+S` - Snipping Tool
- `Win+G` - Xbox Game Bar

**Recommended defaults**: `Win+Shift+H`, `Win+Alt+L`, or `Ctrl+Alt+H`

#### Edge Cases
| Scenario | Solution |
|----------|----------|
| App not running | Add startup option + background service |
| Hotkey already registered | Show error, suggest alternative |
| No pinned items | Show "Pin rooms from dashboard" prompt |
| Bridge disconnected | Show cached state with "Offline" badge |
| Multiple monitors | Open on monitor with cursor |

#### Implementation Phases
1. **Phase 1**: GlobalHotkeyService + basic overlay showing all rooms
2. **Phase 2**: Pinning mechanism + settings persistence
3. **Phase 3**: Hotkey configuration UI in Settings
4. **Phase 4**: Polish (animations, compact mode, multi-monitor)

---

### 🟡 Keyboard Shortcuts
Power users expect keyboard control:

| Key | Action |
|-----|--------|
| `1-9` | Toggle room by position |
| `↑/↓` | Adjust selected room brightness ±10% |
| `Space` | Toggle selected room on/off |
| `S` | Open scenes for selected room |
| `Escape` | Go back / close overlay |
| `R` | Refresh room list |

**Implementation**: Handle in `MainWindow.xaml.cs` with `KeyDown` event

---

### 🟡 Smart Suggestions
Time-of-day and usage pattern suggestions:

```
┌──────────────────────────────────┐
│ 🌙 It's getting late             │
│ [Activate "Evening" scene →]     │
└──────────────────────────────────┘
```

**Triggers**:
- Sunset time (use Windows location services)
- User's historical patterns (track scene activations by time)
- Morning wake-up suggestions

---

### 🟡 Undo Support
Track state changes and allow undo:

```
┌────────────────────────────────────────────┐
│ Living Room turned off     [Undo] (Ctrl+Z) │
└────────────────────────────────────────────┘
```

**Implementation**:
- Maintain stack of recent commands
- Store previous state before each change
- Show toast notification with undo button
- Auto-dismiss after 5 seconds

---

### 🟢 Ambient Mode / Screensaver
Transform dashboard into dynamic ambient display:
- Slowly cycling colors based on active light states
- Optional clock overlay
- Responds to light changes in real-time
- Activates after idle timeout

---

### 🟢 System Tray Icon
Minimize to system tray with quick flyout:
- Toggle favorite rooms
- Activate favorite scenes
- Quick brightness slider
- Open full app

**Requires**: `NotifyIcon` implementation via P/Invoke or community package

---

### 🟢 Color Harmony Tools
In `LightDetailPage`, add color presets:
- Complementary colors
- Analogous colors
- Warm/Cool presets
- "Match this room" - sample dominant color from another room

---

## Architecture Enhancements

### 🟡 Offline Caching Layer
```
IHueBridgeService
    ↓
CachedHueBridgeService (decorator)
    ↓  ↓
    │  SQLite/JSON cache
    ↓
HueBridgeService (network)
```

**Benefits**:
- View last-known state when offline
- Queue changes for when connection restores
- Faster initial load from cache
- Graceful degradation

---

### 🟢 Multi-Bridge Support
For users with multiple homes/locations:

```csharp
public interface IBridgeManager
{
    IReadOnlyList<BridgeConnection> Connections { get; }
    BridgeConnection? ActiveBridge { get; set; }
    Task<BridgeConnection> AddBridgeAsync();
    Task RemoveBridgeAsync(Guid bridgeId);
}
```

**UI changes**:
- Bridge selector in navigation header
- Per-bridge room/zone lists
- Settings page shows all bridges

---

## Performance Optimizations

### 🟢 Virtualization for Large Light Counts
Current `ItemsRepeater` works for ~50 items. For 100+ lights:
- Enable UI virtualization
- Debounce color calculations
- Lazy-load room details

### 🟢 Precompute Frequent Color Conversions
Cache common color temperature → RGB conversions
- Mirek 153-500 range has finite values
- Build lookup table at startup

---

## Quick Wins Checklist

- [ ] Extract `PixelsPerPercent = 3` to constant
- [ ] Extract color adjustment `±20` to constant
- [ ] Add keyboard shortcut hints to tooltips
- [ ] Improve error message copy with actions
- [ ] Add "What's New" on version update
- [ ] Add app rating prompt after 7 days of use

---

## Notes

- All UI changes should respect the existing "liquid glass" aesthetic
- Test on Windows 10 (Acrylic) and Windows 11 (Mica) for backdrop differences
- Consider accessibility: keyboard navigation, screen reader support, high contrast themes
- The app uses WinRT interop patterns - prefer `Microsoft.UI.Windowing` over raw P/Invoke when possible
