# Coding Conventions

**Analysis Date:** 2026-01-20

## Naming Patterns

**Files:**
- PascalCase for all C# files: `HueBridgeService.cs`, `DashboardViewModel.cs`
- One public class/interface per file (follow standard C# conventions)
- Interface files prefixed with `I`: `IHueBridgeService.cs`, `IAnimationService.cs`
- Enum files pluralized: `HueColors.cs`, `AppSettings.cs` (when containing enums/models)

**Functions/Methods:**
- PascalCase for all public and protected methods: `ConnectAsync()`, `GetBridgeAsync()`, `LoadRoomsAsync()`
- Async methods suffixed with `Async`: `ConnectAsync()`, `SaveSceneAsync()`, `LoadUserScenesAsync()`
- Private methods also PascalCase: `EnsureConnectedAsync()`, `SelectLightsForAnimation()`
- Command methods (from RelayCommand) follow PascalCase: `LoadRoomsCommand`, `SetLightOnCommand`

**Variables:**
- camelCase for local variables: `bridgeService`, `errorMessage`, `isLoading`
- Underscore prefix for private fields: `_hueApi`, `_eventStreamCts`, `_multiBridgeService`
- All-caps for constants: `SettingsFileName = "settings.json"`, `StandardDurationMs = 300`
- Observable property backing fields use leading underscore: `[ObservableProperty] private ObservableCollection<RoomCardViewModel> _roomCards`

**Types:**
- PascalCase for classes: `HueBridgeService`, `AnimationEngine`, `SceneStorageService`
- PascalCase for interfaces: `IHueBridgeService`, `ISettingsService`, `IMultiBridgeService`
- PascalCase for enums: `AnimationType`, `LightAssignment`, `RepeatMode`, `TransitionStyle`
- Enum values PascalCase: `AnimationType.Keyframe`, `LightAssignment.All`, `RepeatMode.Loop`
- Record types (tuples) use explicit tuple syntax or named records
- Generic type parameters: `Result<T>`, `IReadOnlyList<AnimatedSceneModel>`

## Code Style

**Formatting:**
- File-scoped namespaces using `namespace X.Y.Z;` (file-scoped, no braces)
- Consistent with Visual Studio default formatting
- No explicit line length limit enforced
- Indentation: 4 spaces (inferred from codebase)

**Linting:**
- No explicit .editorconfig file in project root (uses Visual Studio defaults)
- All generated files use IDE defaults
- No Roslyn analyzers configured beyond defaults

## Import Organization

**Order:**
1. System namespaces: `using System;`, `using System.Collections.ObjectModel;`
2. Third-party packages: `using CommunityToolkit.Mvvm.Input;`, `using HueApi;`
3. Project namespaces: `using HueCompanion.Core.Models;`, `using HueCompanion.Core.Services;`
4. Blank line between groups

**Path Aliases:**
- No path aliases configured in csproj files
- All imports use fully qualified namespaces

## Error Handling

**Patterns:**
- Result pattern for operations that can fail: `Result<T>` and `Result` types in `src/HueCompanion.Core/Models/Result.cs`
- Success checks using implicit bool conversion: `if (result)` or `if (result.IsSuccess)`
- Explicit `Result.Success(value)` and `Result.Failure(error)` factory methods
- Exception handling in service methods catches specific exception types first:
  - `HttpRequestException` for network errors in `HueBridgeService.cs:58`
  - `ObjectDisposedException` for disposed resources in `HueBridgeService.cs:83`
  - Generic `Exception` as fallback catch-all
- Operations that fail gracefully return `Result.Failure(string error)` with descriptive messages
- See `src/HueCompanion.Core/Services/SettingsService.cs:46` - corrupted files fall back to defaults rather than throwing

**Examples:**
```csharp
// From HueBridgeService.cs:33-68
public async Task<Result> ConnectAsync(string ipAddress, string appKey)
{
    try
    {
        _hueApi = new LocalHueApi(ipAddress, appKey);
        var bridge = await _hueApi.GetBridgeAsync();
        if (bridge?.Data == null || bridge.Data.Count == 0)
        {
            _hueApi = null;
            return Result.Failure("Could not retrieve bridge information...");
        }
        return Result.Success();
    }
    catch (HttpRequestException ex)
    {
        _hueApi = null;
        return Result.Failure($"Network error: {ex.Message}");
    }
    catch (Exception ex)
    {
        _hueApi = null;
        return Result.Failure($"Connection failed: {ex.Message}");
    }
}
```

## Logging

**Framework:** Console logging via `System.Diagnostics` or no explicit logging in observable services

**Patterns:**
- ViewModels expose state changes via `ObservableProperty` attributes (MVVM Toolkit)
- Services publish state changes via events: `event EventHandler<LightStateChangedEventArgs>? LightStateChanged`
- No centralized logging framework (Serilog/NLog not used)
- Error messages propagated through `Result.Error` property

## Comments

**When to Comment:**
- XML documentation comments (triple-slash `///`) on all public types, methods, and properties
- Inline comments explain non-obvious algorithm or domain-specific logic
- No commented-out code blocks observed (keep codebase clean)

**JSDoc/TSDoc:**
- Uses C# XML documentation standard: `/// <summary>`, `/// <param>`, `/// <returns>`, `/// <remarks>`
- All public APIs documented with summary tags
- Parameter documentation for complex methods
- Example from `src/HueCompanion.Core/Models/HueColor.cs:3-4`:
```csharp
/// <summary>
/// Represents a color for Hue lights, using CIE xy color space.
/// </summary>
public class HueColor
```

## Function Design

**Size:** Methods are relatively concise (20-50 lines typical), complex logic extracted to helpers

**Parameters:**
- Null checks on constructor parameters using `?? throw new ArgumentNullException(nameof(param))`
- See `src/HueCompanion.Core/Services/AnimationEngine.cs:18-26`:
```csharp
public AnimationEngine(
    IHueBridgeService bridgeService,
    AnimatedSceneModel scene,
    List<Guid> targetLights)
{
    _bridgeService = bridgeService ?? throw new ArgumentNullException(nameof(bridgeService));
    _scene = scene ?? throw new ArgumentNullException(nameof(scene));
    _targetLights = targetLights ?? throw new ArgumentNullException(nameof(targetLights));
}
```

**Return Values:**
- Async methods return `Task<Result<T>>` or `Task<Result>` for consistency
- Non-async methods return direct values or `Result` types
- Collections returned as `IReadOnlyList<T>` to prevent external mutation
- Tuple returns for multiple values: `(byte R, byte G, byte B)` from `HueColor.ToRgb()`

## Module Design

**Exports:**
- Each module exports via public classes/interfaces in its namespace
- Service interfaces `I*Service` define contracts, implementations in same namespace
- Models in `Models` folder, services in `Services` folder, ViewModels in `ViewModels` folder

**Barrel Files:**
- No barrel files (index.ts-equivalent) observed in C# project
- Direct imports using full namespaces

## ViewModels

**MVVM Toolkit Pattern:**
- All ViewModels inherit from `ObservableObject`
- Observable properties use `[ObservableProperty]` attribute on backing field
- Property names follow pattern: public `RoomCards`, backing field `_roomCards`
- Commands use `[RelayCommand]` attribute on async/sync methods
- Dependency injection through constructor

**Example from `src/HueCompanion.Core/ViewModels/DashboardViewModel.cs:16-40`:**
```csharp
public partial class DashboardViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<RoomCardViewModel> _roomCards = new();

    [RelayCommand]
    public async Task LoadRoomsAsync()
    {
        // Implementation
    }
}
```

---

*Convention analysis: 2026-01-20*
