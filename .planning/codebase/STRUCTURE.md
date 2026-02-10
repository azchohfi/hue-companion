# Codebase Structure

**Analysis Date:** 2026-01-20

## Directory Layout

```
hue-companion/
├── src/                                    # Source code root
│   ├── HueCompanion/                         # WinUI 3 presentation layer
│   │   ├── App.xaml                        # Application root XAML
│   │   ├── App.xaml.cs                     # DI container setup, crash logging
│   │   ├── MainWindow.xaml(.cs)            # Navigation shell, hotkey/tray services
│   │   ├── Views/                          # Page definitions (XAML + code-behind)
│   │   ├── Controls/                       # Reusable UI components (cards, dialogs)
│   │   ├── Styles/                         # Global XAML theme resources
│   │   ├── Converters/                     # XAML value converters
│   │   ├── Constants/                      # UI constants (animations, layout)
│   │   ├── Helpers/                        # Navigation, command parsing, dispatching
│   │   ├── Services/                       # UI-specific services (hotkey, tray)
│   │   ├── Utilities/                      # Generic utilities
│   │   └── Assets/                         # Images, app icon, embedded scenes
│   │
│   ├── HueCompanion.Core/                    # Business logic layer (platform-agnostic)
│   │   ├── Services/                       # Bridge communication, storage, animation
│   │   │   ├── Interfaces/                 # Service contracts
│   │   │   ├── HueBridgeService.cs         # Single bridge API wrapper
│   │   │   ├── MultiBridgeService.cs       # Multi-bridge aggregation
│   │   │   ├── AnimationEngine.cs          # Keyframe/event animation executor
│   │   │   ├── AnimationService.cs         # Scene playback coordinator
│   │   │   ├── SceneStorageService.cs      # JSON persistence
│   │   │   ├── SettingsService.cs          # AppSettings persistence
│   │   │   ├── BridgeDiscoveryService.cs   # UPnP discovery
│   │   │   └── Easing.cs                   # Animation easing functions
│   │   │
│   │   ├── ViewModels/                     # MVVM ViewModels for each page
│   │   │   ├── DashboardViewModel.cs       # Room/zone grid display
│   │   │   ├── RoomDetailViewModel.cs      # Room control + scene playback
│   │   │   ├── LightDetailViewModel.cs     # Individual light control
│   │   │   ├── SceneBuilderViewModel.cs    # Timeline editor state
│   │   │   ├── ScenesViewModel.cs          # Built-in/user scene library
│   │   │   └── SettingsViewModel.cs        # Bridge/hotkey configuration
│   │   │
│   │   ├── Models/                         # Domain entity models
│   │   │   ├── RoomModel.cs                # Room/zone entity
│   │   │   ├── LightModel.cs               # Light entity
│   │   │   ├── SceneModel.cs               # Bridge scene reference
│   │   │   ├── AnimatedSceneModel.cs       # User-created scene with animations
│   │   │   ├── AnimationDefinition.cs      # Keyframe/event animation template
│   │   │   ├── HueColor.cs                 # Color representation
│   │   │   ├── Result.cs                   # Result<T> success/failure monad
│   │   │   ├── AppSettings.cs              # User settings object
│   │   │   └── EventPattern.cs             # Random effect trigger definitions
│   │   │
│   │   └── Events/                         # Custom event types
│   │
│   └── HueCompanion.Tests/                   # xUnit test suite
│       ├── Services/                       # Service unit tests
│       └── Models/                         # Model unit tests
│
├── docs/                                   # Documentation
├── assets/                                 # Project-level assets (screenshots, design)
├── .planning/                              # GSD phase planning
│   └── codebase/                           # Architecture reference docs
├── CLAUDE.md                               # Developer instructions
├── README.md                               # Project overview
├── HueCompanion.sln                          # Visual Studio solution
└── Directory.Build.props                   # MSBuild properties
```

## Directory Purposes

**HueCompanion (Presentation Layer):**
- Purpose: WinUI 3 UI components, page navigation, user interaction
- Contains: XAML views, code-behind, converters, styles, UI-specific services
- Key files: `App.xaml.cs` (DI setup), `MainWindow.xaml.cs` (shell), Views/*.xaml

**HueCompanion.Core (Business Logic Layer):**
- Purpose: Domain logic, bridge communication, data models, view models
- Contains: Service implementations, interfaces, models, viewmodels, animation engine
- Key files: `Services/HueBridgeService.cs` (API wrapper), `Services/MultiBridgeService.cs` (aggregation), `Services/AnimationEngine.cs` (60fps animation)

**HueCompanion.Tests (Testing):**
- Purpose: Unit test coverage for core business logic
- Contains: xUnit test cases for services and models
- Key files: Test fixtures, mock implementations

**Views/**
- Purpose: Page definitions for navigation destinations
- Contains: XAML markup (layout) + code-behind (event handlers)
- Pattern: Each View has corresponding ViewModel in HueCompanion.Core/ViewModels
- Files: `DashboardPage.xaml`, `RoomDetailPage.xaml`, `LightDetailPage.xaml`, `ScenesPage.xaml`, `SceneBuilderPage.xaml`, `SettingsPage.xaml`, `SetupPage.xaml`

**Controls/**
- Purpose: Reusable UI components
- Contains: Card controls (RoomCard, LightCard), dialogs, color pickers, effect flyouts
- Files: `RoomCard.xaml` (room/zone card widget), `LightCard.xaml` (light row), `ColorPickerFlyout.xaml`, `SaveEffectDialog.xaml`

**Styles/**
- Purpose: Global XAML theme resources and control customizations
- Contains: Color brushes, control template overrides, animations
- Files: `AppStyles.xaml` (ToggleSwitchStyle, ToggleSwitch overrides, theme colors)

**Converters/**
- Purpose: XAML value converters for binding transformations
- Contains: Bool-to-opacity, color-to-brush, color-to-gradient converters
- Files: `BoolToOpacityConverter.cs`, `HexToGradientBackgroundConverter.cs`, `HexToGradientBorderConverter.cs`

**Constants/**
- Purpose: Centralized constant values
- Contains: Animation durations, layout dimensions, icon glyphs, default colors
- Files: `AppConstants.cs` (StandardDurationMs = 300, InactiveIconAlpha = 128, window size)

**Helpers/**
- Purpose: UI infrastructure utilities
- Contains: Navigation service, command-line parsing, dispatcher marshaling
- Files: `NavigationService.cs`, `CommandLineParser.cs`, `DispatcherHelper.cs`, `NavigationTag.cs`

**Services/** (HueCompanion.Core)
- Purpose: Core business logic
- Contains: Bridge API wrapper, animation engine, scene storage, settings persistence
- Interfaces: `Interfaces/I*.cs` (IHueBridgeService, IMultiBridgeService, IAnimationService, ISettingsService, etc.)
- Implementations: `*.cs` files for each interface

**ViewModels/** (HueCompanion.Core)
- Purpose: MVVM state and commands for each page
- Contains: ObservableProperty state, RelayCommand handlers, service orchestration
- Pattern: Inherit from ObservableObject (MVVM Toolkit), one ViewModel per page/major feature
- Files: `DashboardViewModel.cs`, `RoomDetailViewModel.cs`, `LightDetailViewModel.cs`, `SceneBuilderViewModel.cs`

**Models/** (HueCompanion.Core)
- Purpose: Domain entities and value objects
- Contains: Light, Room, Scene definitions; animation keyframes; configuration models
- Files: `RoomModel.cs`, `LightModel.cs`, `SceneModel.cs`, `AnimatedSceneModel.cs`, `AnimationDefinition.cs`, `HueColor.cs`, `Result.cs`, `AppSettings.cs`

## Key File Locations

**Entry Points:**
- `src/HueCompanion/App.xaml.cs`: Application initialization, DI setup, crash logging
- `src/HueCompanion/MainWindow.xaml.cs`: Window shell, navigation, hotkey/tray integration
- `HueCompanion.sln`: Solution file, references all three projects

**Configuration:**
- `src/HueCompanion/Constants/AppConstants.cs`: UI animation durations, layout sizes, icon glyphs
- `src/HueCompanion.Core/Models/AppSettings.cs`: Settings object schema
- `Directory.Build.props`: MSBuild version and platform configuration

**Core Logic:**
- `src/HueCompanion.Core/Services/HueBridgeService.cs`: Single bridge HueApi wrapper
- `src/HueCompanion.Core/Services/MultiBridgeService.cs`: Multi-bridge orchestration
- `src/HueCompanion.Core/Services/AnimationEngine.cs`: 60fps animation executor
- `src/HueCompanion.Core/Services/SceneStorageService.cs`: JSON scene persistence
- `src/HueCompanion.Core/Services/SettingsService.cs`: AppSettings JSON storage

**View Layer:**
- `src/HueCompanion/MainWindow.xaml`: Navigation shell XAML
- `src/HueCompanion/Views/DashboardPage.xaml`: Home page with room/zone cards
- `src/HueCompanion/Views/RoomDetailPage.xaml`: Room control + scene playback
- `src/HueCompanion/Views/LightDetailPage.xaml`: Individual light control
- `src/HueCompanion/Views/SceneBuilderPage.xaml`: Timeline editor UI
- `src/HueCompanion/Styles/AppStyles.xaml`: Global theme resources

**Testing:**
- `src/HueCompanion.Tests/Services/`: Service unit tests
- `src/HueCompanion.Tests/Models/`: Model unit tests

## Naming Conventions

**Files:**
- Views: `[PageName]Page.xaml` and `[PageName]Page.xaml.cs` (e.g., DashboardPage.xaml)
- ViewModels: `[FeatureName]ViewModel.cs` (e.g., RoomDetailViewModel.cs)
- Models: `[EntityName]Model.cs` (e.g., RoomModel.cs)
- Services: `[ServiceName]Service.cs` (e.g., HueBridgeService.cs)
- Interfaces: `I[ServiceName].cs` (e.g., IHueBridgeService.cs)
- Converters: `[Conversion]Converter.cs` (e.g., BoolToOpacityConverter.cs)

**Directories:**
- Feature/domain areas: PascalCase (Views, Controls, Services, ViewModels, Models)
- Cross-cutting concerns: PascalCase (Helpers, Converters, Constants, Styles, Utilities)
- Platform-specific: Grouped by concern, not platform (all Views in single Views/ folder, not separated by platform)

**Namespaces:**
- Global namespace prefix: `HueCompanion` or `HueCompanion.Core`
- Layers: `HueCompanion.Core.Services.Interfaces`, `HueCompanion.Core.ViewModels`, `HueCompanion.Core.Models`
- Features: `HueCompanion.Helpers`, `HueCompanion.Services`, `HueCompanion.Converters`, `HueCompanion.Constants`

**C# Code:**
- Classes/Methods: PascalCase
- Properties: PascalCase (including backing fields with underscore prefix `_fieldName`)
- Parameters: camelCase
- Constants: UPPER_CASE if file-scoped, PascalCase if public
- Private fields: `_camelCase`
- Async methods: Suffix `Async`

## Where to Add New Code

**New Page/Feature:**
- XAML View: `src/HueCompanion/Views/[FeatureName]Page.xaml`
- Code-behind: `src/HueCompanion/Views/[FeatureName]Page.xaml.cs`
- ViewModel: `src/HueCompanion.Core/ViewModels/[FeatureName]ViewModel.cs`
- Register ViewModel in DI: `App.xaml.cs` ConfigureServices() with `services.AddTransient<[FeatureName]ViewModel>()`
- Add navigation route: `MainWindow.xaml.cs` NavView_ItemInvoked() switch statement

**New Control:**
- XAML Control: `src/HueCompanion/Controls/[ControlName].xaml`
- Code-behind: `src/HueCompanion/Controls/[ControlName].xaml.cs`
- Register if reusable: Include in Views folder or document in CLAUDE.md

**New Service:**
- Interface: `src/HueCompanion.Core/Services/Interfaces/I[ServiceName].cs`
- Implementation: `src/HueCompanion.Core/Services/[ServiceName].cs`
- Register in DI: `App.xaml.cs` ConfigureServices() with appropriate lifetime (Singleton for stateful, Transient for stateless)
- Document in CLAUDE.md service architecture section

**New Model:**
- Definition: `src/HueCompanion.Core/Models/[ModelName].cs`
- Use: Reference in services and viewmodels
- Persist if needed: Add to SceneStorageService or SettingsService JSON handling

**New Utility/Helper:**
- Presentation helpers: `src/HueCompanion/Helpers/[HelperName].cs`
- Shared utilities: `src/HueCompanion/Utilities/[UtilityName].cs`
- Converters: `src/HueCompanion/Converters/[Conversion]Converter.cs`

**Tests:**
- Service tests: `src/HueCompanion.Tests/Services/[ServiceName]Tests.cs`
- Model tests: `src/HueCompanion.Tests/Models/[ModelName]Tests.cs`
- Fixtures: `src/HueCompanion.Tests/[FeatureName]Fixtures.cs`

## Special Directories

**Assets/ (src/HueCompanion/Assets/)**
- Purpose: Application resources (icons, images, embedded scene templates)
- Generated: No
- Committed: Yes
- Contents: `app.ico`, `Scenes/` (built-in animated scenes), PNG/image files

**Assets/Scenes/ (src/HueCompanion/Assets/Scenes/)**
- Purpose: Embedded JSON scene templates (built-in scenes)
- Format: AnimatedSceneModel JSON structure
- Committed: Yes, as template seed data
- Usage: Loaded by AnimationService on application start

**(HueCompanion.Core) bin/ and obj/**
- Purpose: Build output and intermediate files
- Generated: Yes
- Committed: No (.gitignored)
- Pattern: Organized by configuration (Debug, Release) and platform (x64, ARM64)

**(HueCompanion) bin/ and obj/**
- Purpose: Build output for presentation layer
- Generated: Yes
- Committed: No (.gitignored)
- Pattern: WinUI specific: net8.0-windows10.0.22621.0 directory

**%LOCALAPPDATA%/HueCompanion/ (Runtime)**
- Purpose: Application data directory (settings, scenes, logs)
- Structure:
  - `AppSettings.json`: Serialized AppSettings object
  - `Scenes/`: User-created AnimatedSceneModel JSON files
  - `crash.log`: Unhandled exception log
- Lifetime: Persistent across application launches
- Creation: Automatic when services first write to directory

---

*Structure analysis: 2026-01-20*
