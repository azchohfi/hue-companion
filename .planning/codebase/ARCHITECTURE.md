# Architecture

**Analysis Date:** 2026-01-20

## Pattern Overview

**Overall:** MVVM (Model-View-ViewModel) with Dependency Injection and Service-Oriented Architecture

**Key Characteristics:**
- Clear separation between UI presentation (`HueCompanion`), business logic (`HueCompanion.Core`), and testing (`HueCompanion.Tests`)
- WinUI 3 framework for Windows desktop UI with Mica backdrop and modern controls
- CommunityToolkit.MVVM for observable properties, relay commands, and MVVM messaging
- Multi-bridge support with aggregation layer (`IMultiBridgeService`) abstracting individual bridge connections
- Event-driven real-time state synchronization through bridge event streams
- Result<T> monad pattern for explicit error handling without exceptions
- Service locator pattern via `App.Services` (IServiceProvider)

## Layers

**Presentation Layer (HueCompanion):**
- Purpose: WinUI 3 views, controls, page navigation, and user interaction handling
- Location: `/c/Users/danie/Documents/Code/hue-companion/src/HueCompanion/`
- Contains: XAML pages, code-behind views, converters, dialogs, controls, styles, navigation helpers
- Depends on: HueCompanion.Core services and models via dependency injection
- Used by: Application entry point, end users interacting with UI

**Core Business Logic Layer (HueCompanion.Core):**
- Purpose: Domain models, service implementations, view models, and bridge communication
- Location: `/c/Users/danie/Documents/Code/hue-companion/src/HueCompanion.Core/`
- Contains: Service interfaces, implementations, view models, models, animation engine, storage logic
- Depends on: HueApi NuGet package, standard .NET libraries
- Used by: Presentation layer services and view models

**Testing Layer (HueCompanion.Tests):**
- Purpose: Unit tests for models and services
- Location: `/c/Users/danie/Documents/Code/hue-companion/src/HueCompanion.Tests/`
- Contains: Test fixtures, service mocks, model tests
- Depends on: HueCompanion.Core, xUnit testing framework

## Data Flow

**UI to Bridge Command Flow:**

1. User interacts with UI control (toggle, slider, button)
2. XAML binding or event handler triggers ViewModel relay command
3. ViewModel executes command, calls service interface method
4. Service (HueBridgeService or MultiBridgeService) translates command to HueApi call
5. HueApi sends HTTP request to physical Hue bridge
6. Bridge processes command, updates light state
7. Bridge broadcasts state change via SSE event stream
8. HueBridgeService receives event, parses it, raises LightStateChanged event
9. ViewModels subscribe to event and update ObservableProperties
10. WinUI bindings refresh UI automatically

**Example: Toggle Light On/Off**

```
RoomDetailPage.xaml (ToggleSwitch_Tapped)
  → RoomDetailViewModel.ToggleLightCommand
    → IMultiBridgeService.GetBridgeService(bridgeId)
      → IHueBridgeService.SetLightOnAsync(lightId, isOn)
        → HueApi.SetLightStateAsync()
          → HTTP PUT to bridge
            → Bridge event stream: light.on changed
              → LightStateChanged event fires
                → RoomDetailViewModel.OnMultiBridgeLightStateChanged
                  → Update UI observable properties
                    → XAML bindings refresh display
```

**Multi-Bridge Aggregation Flow:**

```
GetAllRoomsAsync() called
  ↓
IMultiBridgeService.GetAllRoomsAsync()
  ├─ Iterate each configured bridge
  ├─ Call IHueBridgeService.GetRoomsAsync() on each
  ├─ Augment RoomModel with BridgeId, BridgeName, ShowBridgePrefix
  └─ Return flattened list from all bridges
```

**State Management:**

- ViewModels hold observable state (ObservableProperty from MVVM Toolkit)
- Services maintain transient state (current connection, animation sessions)
- Settings persisted to `%LOCALAPPDATA%/HueCompanion/` (AppSettings.json)
- Scenes stored in `%LOCALAPPDATA%/HueCompanion/Scenes/` (AnimatedSceneModel JSON)
- No in-app state persistence between sessions; services recreated on app launch

## Key Abstractions

**IHueBridgeService:**
- Purpose: Abstract single bridge communication, isolate API implementation details
- Examples: `HueBridgeService` (implementation), `MultiBridgeService` (uses multiple)
- Pattern: Service interface with event publishers (LightStateChanged, Connected, Disconnected)

**IMultiBridgeService:**
- Purpose: Aggregate operations across multiple bridges, manage bridge lifecycle
- Examples: Multi-bridge room/zone queries, bridge discovery, connection management
- Pattern: Wraps multiple IHueBridgeService instances, routes commands by bridge ID

**ViewModels:**
- Purpose: Bind UI state to domain models, execute commands without coupling views to services
- Examples: `DashboardViewModel`, `RoomDetailViewModel`, `SceneBuilderViewModel`
- Pattern: Extend ObservableObject, use ObservableProperty for state, RelayCommand for actions

**Models:**
- Purpose: Represent domain entities (lights, rooms, scenes, animations)
- Examples: `RoomModel`, `LightModel`, `AnimatedSceneModel`, `AnimationDefinition`
- Pattern: Plain C# objects with properties, enums for types (LightGroupType, AnimationType, EventPreset)

**AnimationEngine:**
- Purpose: Execute keyframe-based and event-based light animations at 60fps
- Examples: Timed color transitions, random lightning effects, candle flicker
- Pattern: Disposable, takes list of lights and scene definition, runs async tasks with CancellationToken

**Result<T> / Result:**
- Purpose: Represent success/failure explicitly without throwing, carry error messages
- Examples: `Result<IReadOnlyList<RoomModel>>`, `Result`
- Pattern: Readonly struct, implicit bool conversion, error message property

## Entry Points

**Application Entry (App):**
- Location: `src/HueCompanion/App.xaml.cs`
- Triggers: Windows OS when .exe is launched or package activation
- Responsibilities: Initialize dependency injection, parse command-line args, configure crash logging, create MainWindow, handle unhandled exceptions

**Main Window:**
- Location: `src/HueCompanion/MainWindow.xaml.cs`
- Triggers: App.OnLaunched() creates and activates main window
- Responsibilities: Set up navigation frame, load rooms/zones on demand, initialize hotkey/tray services, handle window visibility/minimize-to-tray, determine initial page (Setup vs Dashboard)

**Page Navigation:**
- Entry points: `DashboardPage`, `RoomDetailPage`, `LightDetailPage`, `ScenesPage`, `SceneBuilderPage`, `SettingsPage`, `SetupPage`
- Trigger: NavigationService.NavigateTo<TPage>() called by MainWindow or inter-page navigation
- Responsibilities: Each page loads its corresponding ViewModel, subscribes to state changes, renders UI

## Error Handling

**Strategy:** Explicit Result<T> return values with error messages propagated to UI, wrapped exceptions logged to `crash.log`

**Patterns:**

- Service methods return `Result<T>` or `Result` instead of throwing
- Calling code checks `result.IsSuccess` and accesses `result.Value` or `result.Error`
- Unhandled exceptions caught at app level, logged to `%LOCALAPPDATA%/HueCompanion/crash.log`
- Network errors (HttpRequestException) caught in HueBridgeService, converted to Result.Failure
- XAML binding errors logged via Debug output

**Example:**
```csharp
var roomsResult = await _multiBridgeService.GetAllRoomsAsync();
if (roomsResult.IsSuccess)
{
    foreach (var room in roomsResult.Value!)
    {
        // Process room
    }
}
else
{
    ErrorMessage = roomsResult.Error;
}
```

## Cross-Cutting Concerns

**Logging:**
- Method: System.Diagnostics.Debug.WriteLine() for debug-only console logging
- Crash logging: App-level unhandled exception handler writes to crash.log
- No persistent application logging in production

**Validation:**
- Input validation in services before API calls
- Bridge connection validation before queries
- Scene and animation model validation in AnimationEngine

**Authentication:**
- App key (credential) stored securely in AppSettings via SettingsService
- Bridge IP discovery via BridgeDiscoveryService (UPnP discovery)
- App key obtained via bridge link button flow in SetupPage

**Command-Line Integration:**
- Entry: App.CommandLineArgs (parsed via CommandLineParser)
- Supports: `--page`, `--name` for navigation, `--screenshot-mode` for automation
- Example: `hue.cmd --page RoomDetail --name "Living Room"`

**Dependency Injection Container:**
- Built in App.ConfigureServices() via Microsoft.Extensions.DependencyInjection
- Registered as App.Services (IServiceProvider)
- Singletons: Services (bridge, settings, storage, animation), MessengerService
- Transients: ViewModels (new instance per navigation)

---

*Architecture analysis: 2026-01-20*
