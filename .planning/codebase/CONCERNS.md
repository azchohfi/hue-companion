# Codebase Concerns

**Analysis Date:** 2026-01-20

## Tech Debt

**Fire-and-Forget Task Execution:**
- Issue: Multiple ViewModels execute async operations without awaiting completion or capturing exceptions. This can lead to unhandled exceptions and task loss if operations fail silently.
- Files: `src/HueWindows.Core/ViewModels/RoomDetailViewModel.cs` (line 295, 297, 535, 683), `src/HueWindows.Core/ViewModels/LightDetailViewModel.cs` (line 128), `src/HueWindows.Core/ViewModels/SettingsViewModel.cs` (lines 199, 240, 246, 261), `src/HueWindows.Core/ViewModels/DashboardViewModel.cs` (line 317), `src/HueWindows.Core/ViewModels/DashboardCardViewModel.cs` (lines 198, 202), `src/HueWindows.Core/Services/AnimationEngine.cs` (line 322)
- Impact: Failed light state commands silently fail without user feedback or retry. Settings save operations may not complete. Animation triggers may be lost.
- Fix approach: Implement proper async/await patterns. Create a helper to capture and log failed async operations. Use Result pattern consistently across all async operations to track failure state.

**Silent Exception Swallowing:**
- Issue: Generic `catch` blocks without variable capture or specific exception types throughout codebase. These hide real errors.
- Files: `src/HueWindows/Views/RoomDetailPage.xaml.cs` (line 832), `src/HueWindows/App.xaml.cs` (line 95), `src/HueWindows/Controls/RoomCard.xaml.cs` (line 248), `src/HueWindows.Core/ViewModels/RoomDetailViewModel.cs` (line 690), `src/HueWindows.Core/Services/AnimationEngine.cs` (lines 246, 249, 282-285), `src/HueWindows/Converters/BoolToOpacityConverter.cs` (lines 143, 203, 234), `src/HueWindows/Utilities/VisualTreeExtensions.cs` (line 26)
- Impact: Animations fail silently, converters fail without logging, UI state becomes inconsistent. Difficult to debug production issues.
- Fix approach: Add logging to all catch blocks. Use specific exception types rather than bare `catch`. Log exception details for debugging.

**Large Monolithic Components:**
- Issue: `SceneBuilderPage.xaml.cs` (1805 lines) and `SceneBuilderViewModel.cs` (1413 lines) are significantly oversized with complex timeline rendering logic mixed with event handling and state management.
- Files: `src/HueWindows/Views/SceneBuilderPage.xaml.cs`, `src/HueWindows.Core/ViewModels/SceneBuilderViewModel.cs`
- Impact: Difficult to test, high bug risk during modifications, steep learning curve for new developers.
- Fix approach: Extract timeline rendering into separate class. Move playback state management to dedicated service. Create specialized view models for event tracks and keyframe editing.

**Missing Connection Retry Logic:**
- Issue: `HueBridgeService.EnsureConnectedAsync()` attempts one reconnection but doesn't implement exponential backoff or max retry attempts. Event stream failures may not trigger reconnection.
- Files: `src/HueWindows.Core/Services/HueBridgeService.cs` (lines 73-102)
- Impact: Bridge disconnection can result in stale UI state for extended periods. User may not notice connection loss until attempting an action.
- Fix approach: Implement exponential backoff retry with configurable max attempts. Monitor event stream health separately from command execution. Add connection state events for UI notification.

**No Rate Limiting on Bridge Commands:**
- Issue: Slider debouncing (150ms) is only enforced client-side. No API-level rate limiting or command batching implemented.
- Files: `src/HueWindows/Views/RoomDetailPage.xaml.cs`, `src/HueWindows/Views/LightDetailPage.xaml.cs`, `src/HueWindows.Core/Services/AnimationEngine.cs`
- Impact: Rapid animation updates (30 FPS) could overwhelm Hue bridge if debouncing timer is disabled. May trigger API throttling or connection drops.
- Fix approach: Implement request queue with rate limiting in `HueBridgeService`. Add API error handling for 429 (Too Many Requests) responses. Consider command batching for grouped light updates.

---

## Known Bugs

**Scene Activation Silent Failure:**
- Symptoms: Activating a scene appears successful but light states don't change. No error notification shown.
- Files: `src/HueWindows.Core/ViewModels/RoomDetailViewModel.cs` (lines 670-695)
- Trigger: Scene activation command fails due to bridge unavailability or invalid scene ID
- Workaround: Check bridge connection status before attempting scene activation. Manually refresh room state.

**Catch Block Without Exception Variable:**
- Symptoms: Animation failures or UI updates fail silently with no indication of what went wrong
- Files: `src/HueWindows/Views/RoomDetailPage.xaml.cs` (line 832: `catch { /* Animation failure is non-critical - ignore */ }`)
- Trigger: Any exception during border scale animation or toggle animation execution
- Workaround: Log animation failures to debug output. UI will continue to function but visual feedback may be inconsistent.

**Event Stream Context Capture in RoomDetailViewModel:**
- Symptoms: Scene activation event may be raised on wrong thread if synchronization context is null
- Files: `src/HueWindows.Core/ViewModels/RoomDetailViewModel.cs` (lines 680-688)
- Trigger: Event stream processing occurs when synchronization context is lost (background thread operation)
- Workaround: Ensure scene activation handler is thread-safe and can update UI from any thread via DispatcherQueue.

---

## Security Considerations

**Bridge Credentials in Settings Storage:**
- Risk: IP addresses and app keys stored in LocalApplicationData without encryption. If machine is compromised, attacker gains bridge access.
- Files: `src/HueWindows.Core/Services/SceneStorageService.cs` (line 43-44), `src/HueWindows.Core/Models/BridgeModel.cs`
- Current mitigation: Windows file permissions on LocalApplicationData folder provide basic protection
- Recommendations: Encrypt credentials using DPAPI. Use Windows Credential Manager instead of JSON files. Implement credential rotation on bridging.

**No Input Validation on Scene Import:**
- Risk: Importing user-created scene JSON could contain malicious data (extremely large numbers, circular references, etc.) causing DoS
- Files: `src/HueWindows.Core/Services/SceneStorageService.cs` (lines 111-145), `src/HueWindows.Core/ViewModels/SceneBuilderViewModel.cs` (lines 710-730)
- Current mitigation: Basic schema validation on animation structure
- Recommendations: Add size limits on JSON files (max 5MB). Validate numeric ranges for animation values. Test with fuzzing inputs.

**Event Stream Data Processing:**
- Risk: Event stream JSON parsing has limited error handling. Malformed events could crash event processor.
- Files: `src/HueWindows.Core/Services/HueBridgeService.cs` (lines 828-891)
- Current mitigation: Try-catch around event parsing with continue-on-error
- Recommendations: Validate JSON structure before parsing. Implement circuit breaker if event parsing fails repeatedly. Log malformed events.

---

## Performance Bottlenecks

**Timeline Rendering in Scene Builder:**
- Problem: Full canvas redraw on every frame (~60fps) with no culling. Rendering 100+ keyframes causes visible slowdown.
- Files: `src/HueWindows/Views/SceneBuilderPage.xaml.cs` (RenderTimeline method spans multiple methods, ~500+ lines of rendering code)
- Cause: No spatial caching, no dirty-rect invalidation, manual shape creation/destruction each frame
- Improvement path: Implement dirty-rect tracking. Cache rendered shapes. Use shape pooling to avoid GC churn. Consider native rendering API (DirectX) for high-density keyframe scenes.

**GetRoomsAsync Full Reload:**
- Problem: `GetRoomsAsync` fetches ALL lights then filters in memory. For bridges with 50+ lights, this causes noticeable UI lag.
- Files: `src/HueWindows.Core/Services/HueBridgeService.cs` (lines 113-187)
- Cause: No pagination, no server-side filtering in HueApi wrapper
- Improvement path: Implement caching with TTL (30s). Load rooms first, then fetch lights only for visible rooms. Use Task.WhenAll() for parallel bridge requests in MultiBridgeService.

**Event Stream Startup Overhead:**
- Problem: Each bridge connection immediately starts event stream even if app is backgrounded. Multiple event stream handlers cause high CPU usage.
- Files: `src/HueWindows.Core/Services/HueBridgeService.cs` (lines 779-807), `src/HueWindows.Core/Services/MultiBridgeService.cs` (lines 88-95)
- Cause: No connection pooling, no subscription cleanup on window minimize
- Improvement path: Pause event streams when app is backgrounded. Implement connection pooling to share single stream per bridge.

---

## Fragile Areas

**AnimationEngine Simultaneous Event Execution:**
- Files: `src/HueWindows.Core/Services/AnimationEngine.cs` (lines 289-333)
- Why fragile: Fire-and-forget execution of triggers without tracking. If a trigger fails midway, other simultaneous triggers may not be attempted. No error rollback mechanism.
- Safe modification: Add exception tracking to `ExecuteTriggerAsync`. Implement idempotent trigger logic. Log all trigger executions for debugging.
- Test coverage: No unit tests for AnimationEngine event execution. Triggers are only tested indirectly through scene playback tests (incomplete).

**RoomDetailViewModel Scene Activation State:**
- Files: `src/HueWindows.Core/ViewModels/RoomDetailViewModel.cs` (lines 670-695)
- Why fragile: Success/failure of scene activation not reported back to UI. UI shows spinning loader indefinitely if activation fails silently.
- Safe modification: Return Task instead of void from `ActivateSceneAsync`. Add timeout (5s) for scene activation. Catch and log exceptions with user-facing error message.
- Test coverage: No tests for scene activation success/failure paths. Manual testing only.

**Multi-Bridge Connection Management:**
- Files: `src/HueWindows.Core/Services/MultiBridgeService.cs` (lines 88-156)
- Why fragile: Concurrent connections and disconnections may race. No locking around `_bridgeServices` dictionary operations outside of GetOrAdd.
- Safe modification: Use ReaderWriterLockSlim for thread-safe operations on bridge service collection. Add cancellation token support to all async operations.
- Test coverage: No unit tests for concurrent connection scenarios. Only happy-path integration tests.

**SceneBuilder Playback State:**
- Files: `src/HueWindows/Views/SceneBuilderPage.xaml.cs` (lines 1-150), `src/HueWindows.Core/ViewModels/SceneBuilderViewModel.cs` (lines 1-200)
- Why fragile: Playback timer, playhead position, and animation engine state kept in separate components. Stop/pause/resume operations may leave state inconsistent.
- Safe modification: Move all playback state into dedicated PlaybackStateMachine class. Make state transitions explicit and logged.
- Test coverage: Gaps in pause/resume, stop-while-playing, and rapid user interactions.

---

## Scaling Limits

**Single Event Stream per Bridge:**
- Current capacity: 1 simultaneous connection per bridge with continuous event polling
- Limit: Bridges have max 256 device connections. Event stream holds 1 connection slot. If too many clients connect, event stream fails.
- Scaling path: Implement server-side caching. Create background service to multiplex single stream across UI clients.

**In-Memory Scene Library:**
- Current capacity: All user scenes loaded into memory at app startup
- Limit: Each scene JSON averages 10-50KB. 1000 scenes would require 50MB+ RAM. Beyond that, UI navigation becomes sluggish.
- Scaling path: Implement lazy-loading with disk-based cache. Create scene search index. Virtualize scene list in UI.

**Timeline Canvas Size:**
- Current capacity: ~60 second timeline at current zoom level can display ~100 keyframes
- Limit: Beyond 200 keyframes, rendering becomes noticeably slow (drops below 30fps)
- Scaling path: Implement progressive rendering (draw visible area first, background fill later). Use hierarchical zoom levels.

---

## Dependencies at Risk

**HueApi NuGet Package:**
- Risk: Package last updated Jan 2024. Community-maintained library with limited maintenance history. Breaking changes possible in future Hue bridge API versions.
- Impact: New Hue API endpoints not supported until package is updated. May need to fork or replace.
- Migration plan: Monitor HueApi releases. Maintain wrapper layer `HueBridgeService` to ease future API migration. Consider evaluating official Hue SDK if released.

**Microsoft.UI.Xaml WinUI 3:**
- Risk: WinUI 3 still under active development. Some stability issues reported in complex layouts.
- Impact: Timeline rendering may have platform-specific bugs. Application may require OS updates for stability.
- Migration plan: File bugs with Microsoft team. Use platform-specific workarounds for known issues (e.g., canvas rendering performance).

**Community Toolkit MVVM:**
- Risk: Moderate community adoption. May lack features required for advanced scenarios.
- Impact: Complex validation or nested observable hierarchies may require custom implementation.
- Migration plan: Maintain abstraction layer (IViewModel interface) to swap MVVM frameworks if needed.

---

## Missing Critical Features

**No Settings Backup/Export:**
- Problem: Bridge credentials and scene library are stored locally with no export mechanism. If app is reinstalled or settings corrupt, all configuration is lost.
- Blocks: Multi-device setup, disaster recovery, scene sharing between users

**No Animation Preview Without Lights:**
- Problem: Scene Builder requires connected lights to preview animations. Cannot design scenes offline.
- Blocks: Mobile/laptop-based scene design, offline use cases

**No Conflict Resolution for Multi-Bridge Scenes:**
- Problem: When controlling same light from multiple bridges (bridge link scenario), no automatic conflict resolution.
- Blocks: Complex multi-room automation

---

## Test Coverage Gaps

**HueBridgeService Network Failures:**
- What's not tested: HttpRequestException handling, connection timeout scenarios, bridge API throttling (429 responses)
- Files: `src/HueWindows.Core/Services/HueBridgeService.cs`
- Risk: Network errors may crash app or leave UI in invalid state. Production may encounter scenarios not covered by manual testing.
- Priority: High

**AnimationEngine Event Triggers:**
- What's not tested: Simultaneous event execution, cancellation during trigger, failed light updates during animation
- Files: `src/HueWindows.Core/Services/AnimationEngine.cs`
- Risk: Animations may fail silently in edge cases. Event triggers may not execute in correct order.
- Priority: High

**MultiBridgeService Concurrent Operations:**
- What's not tested: Multiple concurrent bridge connections, rapid connect/disconnect cycles, bridge disconnection while fetching data
- Files: `src/HueWindows.Core/Services/MultiBridgeService.cs`
- Risk: Race conditions may cause duplicate connections or state corruption.
- Priority: High

**Scene Storage File Corruption Recovery:**
- What's not tested: Partially written JSON files, disk full scenarios, permission errors during save
- Files: `src/HueWindows.Core/Services/SceneStorageService.cs`
- Risk: User scenes may be lost if save operation fails midway. No rollback mechanism.
- Priority: Medium

**RoomDetailViewModel Scene Activation Timeout:**
- What's not tested: Scene activation that never completes, rapid scene changes, scene deletion during activation
- Files: `src/HueWindows.Core/ViewModels/RoomDetailViewModel.cs`
- Risk: UI may show indefinite loading spinner. Scene state may become inconsistent.
- Priority: Medium

**SceneBuilderPage Playback Edge Cases:**
- What's not tested: Stop while playing event animations, rapid pause/resume, seeking to time beyond animation duration, scene changes during playback
- Files: `src/HueWindows/Views/SceneBuilderPage.xaml.cs`
- Risk: Playback may get stuck in invalid state. Animations may not clean up properly.
- Priority: Medium

---

*Concerns audit: 2026-01-20*
