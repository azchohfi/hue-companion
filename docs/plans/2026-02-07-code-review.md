# Full Code Review — HueCompanion

**Date:** 2026-02-07

## CRITICAL (5 issues)

| # | Issue | Location |
|---|-------|----------|
| 1 | **Event subscription leaks in RoomCard/LightCard** — `OnDataContextChanged` subscribes to `PropertyChanged` but never unsubscribes. In recycled containers (ItemsRepeater, GridView), old VMs keep controls alive and fire stale handlers. | `Controls/RoomCard.xaml.cs:129`, `Controls/LightCard.xaml.cs:129` |
| 2 | **Page event subscriptions never cleaned up** — DashboardPage, RoomDetailPage, LightDetailPage, SetupPage, MyDashboardPage all subscribe to VM events in constructors but never unsubscribe (ScenesPage does it correctly as a reference pattern). | Multiple pages |
| 3 | **Race condition in `EnsureConnectedAsync`** — Multiple concurrent callers can race through reconnection, creating multiple `LocalHueApi` instances and duplicate event streams. Needs a `SemaphoreSlim`. | `HueBridgeService.cs:73-102` |
| 4 | **`RoomSceneAssignmentService` not thread-safe** — `_loaded` flag and `_assignments` dictionary accessed without synchronization from multiple VMs. | `RoomSceneAssignmentService.cs:23-47` |
| 5 | **API keys stored in plaintext** — Bridge `AppKey` serialized as plain JSON to `%LOCALAPPDATA%`. DPAPI encryption recommended. | `SettingsService.cs:72-76` |

## MAJOR (9 issues)

| # | Issue | Location |
|---|-------|----------|
| 6 | **ViewModel memory leaks** — DashboardVM, CustomDashboardVM, RoomDetailVM, SceneLibraryVM, ScenesVM, SettingsVM, BridgeManagementVM all subscribe to singleton service events but never unsubscribe. No `IDisposable`. | Multiple VMs |
| 7 | **`ColorConverter.XyToRgb` division by zero** — If `HueColor.Y == 0`, produces `NaN` through color pipeline. | `ColorConverter.cs:87-113` |
| 8 | **Fire-and-forget API calls lose exceptions** — `OnIsOnChanged` in 5+ VMs discard task results (`_ = SetRoomOnAsync(...)`) with no error handling. | `DashboardViewModel.cs:317`, others |
| 9 | **HueBridgeService Set\* methods silently fail** — Return void, no try-catch, silent `return` if `_hueApi == null`. Contrasts with Get methods which return `Result<T>`. | `HueBridgeService.cs:232-616` |
| 10 | **TimeRulerRenderer native resource leak** — `CanvasTextFormat` (DirectWrite wrapper) created but never disposed. `TimelineRenderer.Dispose()` doesn't clean it up. | `TimeRulerRenderer.cs`, `TimelineRenderer.cs:203-211` |
| 11 | **Hardcoded dark-theme colors in flyouts** — ColorPickerFlyout and NativeEffectFlyout hardcode dark gradients and `Foreground="White"`, broken in light theme. | `ColorPickerFlyout.xaml:17-22`, `NativeEffectFlyout.xaml:17-22` |
| 12 | **Tests don't test production code** — `SceneStorageServiceTests` and `SettingsServiceTests` use `new` keyword hiding to reimplement all logic. Changes to production code won't be caught. | `SceneStorageServiceTests.cs:395-535`, `SettingsServiceTests.cs:136-186` |
| 13 | **Extremely low test coverage** — Only 4 test files covering models + 2 services. 0 VM tests, 0 tests for AnimationEngine, ColorConverter, MultiBridgeService, HueBridgeService. Moq is installed but unused. | `HueCompanion.Tests/` |
| 14 | **`async void` without error handling** — `OnLaunched`, `NavigateToInitialPage`, `NavView_PaneOpened`, `OnSceneActivated`, `OnPinnedItemsChanged` are all async void with no try-catch. | Multiple files |

## MINOR (18 issues)

| # | Issue | Location |
|---|-------|----------|
| 15 | Toggle switch theme overrides not theme-aware (white-based colors global) | `AppStyles.xaml:73-106` |
| 16 | `HueColor` is mutable class used as value — aliasing bugs possible | `HueColor.cs` |
| 17 | `HueColors.WarmWhite` (0.45, 0.41) differs from `HueColor.WarmWhite` (0.4596, 0.4105) | Two files |
| 18 | Duplicate `GetIconForArchetype` in 3 places | `DashboardViewModel.cs`, `DashboardCardViewModel.cs`, `MainWindow.xaml.cs` |
| 19 | Duplicate `CreateCardGradient` across 4 files (with subtle inconsistencies) | RoomCard, LightCard, RoomDetailPage, LightDetailPage |
| 20 | Duplicate `AnimateBorderOpacity`/`AnimateSolidBrushColor` across pages | RoomDetailPage, LightDetailPage |
| 21 | Duplicate NativeEffect flyout logic | `RoomDetailPage.xaml.cs`, `ScenesPage.xaml.cs` |
| 22 | Color conversion matrix constants differ in precision (4-digit vs 7-digit) | `ColorConverter.cs` vs `HueColor.cs` |
| 23 | `_eventStreamCts` created but token never passed to anything | `HueBridgeService.cs:800-810` |
| 24 | `MultiBridgeService._connectionLock` declared, never used | `MultiBridgeService.cs:15` |
| 25 | `BridgeDiscoveryService.DiscoverBridgesAsync` ignores `cancellationToken` | `BridgeDiscoveryService.cs:12-58` |
| 26 | `AnimationEngine.Dispose()` doesn't await running tasks — `ObjectDisposedException` risk | `AnimationEngine.cs:468-476` |
| 27 | `GetRoomAsync` refetches ALL rooms to find one, wasteful during animation | `HueBridgeService.cs:218-229` |
| 28 | DebounceTimers not stopped on page navigation | RoomDetailPage, LightDetailPage |
| 29 | Empty catch blocks silently swallowing errors (6 locations) | RoomCard, RoomDetailPage, converters |
| 30 | Old-style `{Binding}` in SetupPage DataTemplate | `SetupPage.xaml` |
| 31 | `SettingsService.LoadAsync` bare `catch` swallows all exceptions | `SettingsService.cs:46-49` |
| 32 | CI duplication — `pr-validation.yml` triggers on push to main, duplicating `main-ci.yml` | `.github/workflows/pr-validation.yml` |

## NIT (9 issues)

| # | Issue | Location |
|---|-------|----------|
| 33 | Unused dead code: `_lastPlayheadUpdate`, `PlayheadUpdateThrottleMs` in SceneBuilderViewModel | `SceneBuilderViewModel.cs:106-107` |
| 34 | Unused NuGet package: `HueApi.ColorConverters` | `HueCompanion.Core.csproj` |
| 35 | `LightColors` property allocates new list on every access (called 3+ times in quick succession) | `DashboardViewModel.cs:247-261` |
| 36 | `NativeEffectInfo.All` uses same icon glyph for all 10 effects | `NativeEffectInfo.cs:41-53` |
| 37 | Build artifacts (`*.binlog`, `*.log`, `nul`) not in `.gitignore` | `.gitignore` |
| 38 | `Package.appxmanifest` has invalid XML version `1.0.1.0` instead of `1.0` | `Package.appxmanifest:1` |
| 39 | `CardStyle` and `ElevatedCardStyle` in AppStyles.xaml are identical | `AppStyles.xaml:131-146` |
| 40 | `BoolToOpacityConverter.cs` contains 11 converters — misleading filename | `Converters/BoolToOpacityConverter.cs` |
| 41 | Legacy `ScenesRepeater_ElementPrepared` handler is dead code | `RoomDetailPage.xaml.cs:757-760` |

## Positives

- Clean MVVM separation with `Result<T>` error propagation across service boundaries
- Consistent debounced slider pattern (150ms) across all pages
- Well-decomposed layered renderer architecture for Win2D timeline
- Cross-fade border animation technique is creative and flicker-free
- Undo/redo system follows Command pattern cleanly
- WCAG 2.5.5 AAA hit zones (44px) in Scene Builder
- Good XML documentation coverage
- Connected animation from dashboard cards to detail pages
- ScenesPage correctly demonstrates subscribe-in-Loaded / unsubscribe-in-Unloaded pattern

## Recommended Priority Order

1. **Fix event subscription leaks** (#1, #2, #6) — real memory pressure and stale handler bugs
2. **Add `SemaphoreSlim` to `EnsureConnectedAsync`** (#3) — prevents duplicate API clients and event streams
3. **Fix tests to test production code** (#12) — `new` hiding means current tests are false confidence
4. **Guard `ColorConverter.XyToRgb` against Y=0** (#7) — NaN propagation through color pipeline
5. **Wrap `async void` handlers in try-catch** (#14) — unhandled exceptions crash the app
