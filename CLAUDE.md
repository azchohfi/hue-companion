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
| `Styles/AppStyles.xaml` | Global styles and theme resource overrides |
| `Core/Services/HueBridgeService.cs` | Hue API communication layer |
| `Core/ViewModels/DashboardViewModel.cs` | Dashboard data and commands |
| `Core/ViewModels/RoomCardViewModel.cs` | Room/zone card state |

## Styling Approach

Uses **WinUI 3 lightweight styling** - override theme resources rather than re-templating controls.

```xaml
<!-- Global theme resource override in AppStyles.xaml -->
<SolidColorBrush x:Key="ToggleSwitchFillOff" Color="#30FFFFFF"/>

<!-- Per-control override in code-behind -->
RoomToggle.Resources["ToggleSwitchFillOn"] = new SolidColorBrush(roomColor);
```

Key theme resources used:
- `ToggleSwitchFillOn/Off` - Track fill colors
- `ToggleSwitchKnobFillOn/Off` - Knob colors
- `CardBackgroundFillColorDefaultBrush` - Card backgrounds
- `AccentFillColorDefaultBrush` - Accent color

## Room Cards

Room cards use a **liquid glass aesthetic**:
- Semi-transparent gradient background (`#25FFFFFF` to `#15FFFFFF`)
- Colored toggle switch shows room's dominant light color when on
- Drag vertically to adjust brightness
- Tap to navigate to room detail

Toggle color is set per-control via `RoomToggle.Resources[]` overrides in `RoomCard.xaml.cs:UpdateToggleColor()`.

## Navigation

- `MainWindow.xaml` hosts NavigationView with rooms/zones in nav items
- Rooms and zones both use `RoomDetailPage` (zones are just grouped lights)
- Navigation parameters pass room/zone ID
- `NavigationHelper.cs` manages page navigation

## Hue API

Uses **HueApi** NuGet package (CLIP v2 API).

```csharp
// Key service methods in HueBridgeService.cs
GetRoomsAsync()      // Returns rooms with lights
GetZonesAsync()      // Returns zones with lights
GetScenesAsync()     // Returns scenes for a room/zone
SetLightStateAsync() // Control individual light
SetGroupStateAsync() // Control room/zone
```

Rooms and zones are both "grouped_light" resources in Hue API - the difference is the room/zone type metadata.

## Data Flow

1. `DashboardViewModel.LoadRoomsAsync()` fetches rooms and zones
2. Creates `RoomCardViewModel` for each
3. `RoomCard` control binds to viewmodel
4. Toggle/brightness changes call commands on viewmodel
5. ViewModel calls `HueBridgeService` to update Hue bridge
