# External Integrations

**Analysis Date:** 2026-01-20

## APIs & External Services

**Philips Hue Bridge (CLIP v2 API):**
- Service: Hue Bridge on local network (LAN)
- What it's used for: Light control, scene management, real-time state updates
  - Query rooms, zones, lights, scenes
  - Toggle lights on/off, set brightness, color, color temperature
  - Execute scenes (built-in Hue scenes)
  - Real-time event streaming for state synchronization
- SDK/Client: HueApi 3.1.2 (NuGet package)
- Authentication: App key (registration via link button press)
- Protocol: HTTPS (self-signed certificates, skipped verification)
- Event streaming: Server-Sent Events (SSE) via `StartEventStream()` in `HueBridgeService`

**Bridge Discovery Services:**
- HTTP-based discovery: discovery.meethue.com (fallback if mDNS unavailable)
- mDNS discovery: Local network broadcast discovery
- Both via `BridgeDiscoveryService` which uses HueApi's `HttpBridgeLocator` and `MdnsBridgeLocator`

## Data Storage

**Local File Storage:**
- Settings: `%LOCALAPPDATA%\HueWindows\settings.json` (AppSettings model as JSON)
  - Contains: Bridge credentials, user preferences, pinned items, scene assignments
  - Service: `SettingsService` (read/write via System.Text.Json)

**Scene Storage:**
- Built-in scenes: Bundled in application package at `Assets\Scenes\scene_*.json`
- User-created scenes: `%LOCALAPPDATA%\HueWindows\Scenes\scene_*.json`
- Format: AnimatedSceneModel (JSON with camelCase naming policy)
- Service: `SceneStorageService` with separate paths for built-in vs user scenes

**Crash Logs:**
- Location: `%LOCALAPPDATA%\HueWindows\crash.log`
- Format: Plain text with timestamp, source, exception details
- Usage: Unhandled exception logging (set in App.xaml.cs exception handlers)

**Databases:**
- None - application uses local file storage only (JSON-based)

**Caching:**
- In-memory collections in ViewModels (no persistent caching layer)
- Scene library cached in `SceneLibraryViewModel`
- Light/room state cached in `RoomDetailViewModel`

## Authentication & Identity

**Auth Provider:**
- Custom local-only authentication (no cloud)
- Hue Bridge registration process:
  - User must press physical link button on bridge
  - App calls `HueApi.LocalHueApi.RegisterAsync()` to request app key
  - App key stored in settings for future connections
  - Implementation: `BridgeDiscoveryService.RegisterAsync()`

**Credential Storage:**
- Bridge IP address + app key stored in `settings.json` (plain text in LocalAppData)
- Multi-bridge support: List of `BridgeCredential` objects in `AppSettings.ConfiguredBridges`
- No encryption of stored credentials (runs locally on user's machine)

## Monitoring & Observability

**Error Tracking:**
- None (no remote error tracking)
- Local crash logging: See "Crash Logs" under Data Storage

**Logs:**
- Crash logs written to `%LOCALAPPDATA%\HueWindows\crash.log`
- Debug output via `System.Diagnostics.Debug.WriteLine()` in Visual Studio debugger
- Three-layer exception handling in App.xaml.cs:
  - `UnhandledException` (XAML layer)
  - `UnhandledExceptionEventArgs` (AppDomain)
  - `UnobservedTaskException` (async/await)

## CI/CD & Deployment

**Hosting:**
- No remote hosting - desktop application only
- MSIX package deployment:
  - Microsoft Store (future)
  - Sideloading via MSIX package file
  - GitHub Actions builds artifacts (uploaded as GitHub release)

**CI Pipeline:**
- GitHub Actions via `.github\workflows\`:
  - `main-ci.yml` - Main branch: build (x64/ARM64), unit tests, UI tests, code coverage, visual regression
  - `pr-validation.yml` - Pull requests: build and basic validation
  - `release.yml` - Tag-triggered (v*.*.* pattern): full build, test, publish to GitHub releases
- Build platforms: Both x64 and ARM64 via `<Platforms>x64;ARM64</Platforms>`
- Test framework: xunit with Codecov coverage upload
- Artifacts: MSIX packages uploaded to GitHub releases

## Environment Configuration

**Required environment vars:**
- `CODECOV_TOKEN` - For codecov.io code coverage uploads (set in GitHub Actions secrets)
- All other configuration is file-based (no runtime env vars needed)

**Secrets location:**
- GitHub Actions: `.github\workflows\*.yml` references `${{ secrets.CODECOV_TOKEN }}`
- Application: No external secrets required (bridge credentials stored locally)

## Webhooks & Callbacks

**Incoming:**
- None - application is client-only

**Outgoing:**
- Hue Bridge event streaming (SSE): Real-time light state changes pushed from bridge via `StartEventStream()`
  - Callback handler: `OnEventStreamMessage()` in `HueBridgeService`
  - Event types: Light state changes (on/off, brightness, color, color temperature)

## Event Streaming

**Server-Sent Events (SSE):**
- Bridge maintains open SSE connection for real-time updates
- Started via `HueBridgeService.StartEventStreamAsync()` after connection
- Received events deserialized as `EventStreamResponse` list
- Per-frame processing (not buffered) for immediate UI updates
- Cancellation token allows graceful shutdown: `_eventStreamCts`

## Bridge Discovery Methods

**HTTP-based Discovery:**
- Endpoint: `discovery.meethue.com` (Philips cloud discovery)
- Method: `HttpBridgeLocator.LocateBridgesAsync()`
- Returns: BridgeId and IP address for bridges on network
- Timeout: Configurable (typically 5 seconds)

**mDNS Discovery:**
- Protocol: Multicast DNS (local network only)
- Method: `MdnsBridgeLocator.LocateBridgesAsync()`
- Fallback if HTTP discovery fails or unavailable
- No external service dependency

## Multi-Bridge Architecture

**Service:**
- `MultiBridgeService` - Manages multiple bridge connections simultaneously
- `GetDefaultBridgeService()` - Returns primary bridge service
- `GetBridgeService(bridgeId)` - Returns specific bridge service by ID
- Bridge credentials stored in `AppSettings.ConfiguredBridges` list

---

*Integration audit: 2026-01-20*
