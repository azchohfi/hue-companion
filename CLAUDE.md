# CLAUDE.md - Project Context

## Build Commands

```bash
# Build
dotnet build src/HueWindows/HueWindows.csproj

# Run
dotnet run --project src/HueWindows/HueWindows.csproj

# Build for specific architecture
dotnet build src/HueWindows/HueWindows.csproj -p:Platform=x64
```

## Architecture

**WinUI 3 + MVVM** - Standard MVVM pattern using CommunityToolkit.Mvvm.

```
src/
├── HueWindows/              # WinUI 3 App (views, controls, styles)
├── HueWindows.Core/         # Business logic (models, viewmodels, services)
└── HueWindows.Tests/        # Unit tests
```

## Key Files

| File | Purpose |
|------|---------|
| `Views/DashboardPage.xaml` | Main home page with room/zone cards |
| `Views/RoomDetailPage.xaml` | Room detail with scenes and lights |
| `Views/LightDetailPage.xaml` | Individual light control (color/temp) |
| `Controls/RoomCard.xaml` | Widget card for rooms/zones on dashboard |
| `Controls/LightCard.xaml` | Light row card with toggle and color indicator |
| `Styles/AppStyles.xaml` | Global styles and theme resource overrides |
| `Converters/BoolToOpacityConverter.cs` | XAML converters including gradient converters |
| `Constants/AppConstants.cs` | Animation durations, color values |
| `Core/Services/HueBridgeService.cs` | Hue API communication layer |
| `Core/ViewModels/DashboardViewModel.cs` | Dashboard data and commands |
| `Core/ViewModels/RoomDetailViewModel.cs` | Room/zone detail with lights and scenes |
| `Core/ViewModels/LightDetailViewModel.cs` | Individual light control state |

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
