# Multi-Bridge Support Implementation

This document describes the implementation of multi-bridge support for HueWindows (Issue #9).

## Overview

The application now supports connecting to multiple Philips Hue bridges simultaneously, with:
- Auto-discovery of all bridges on the local network
- Bridge management UI in Settings
- Unified dashboard showing rooms/zones from all bridges
- Duplicate room name handling with bridge prefixes
- Per-bridge authentication and connection status indicators

## Architecture Changes

### 1. Core Models

#### `BridgeModel` (`Models/BridgeModel.cs`)
- Added `DisplayName` property that returns `FriendlyName` or a truncated `BridgeId`
- User can set a custom `FriendlyName` to identify bridges

#### `RoomModel` (`Models/RoomModel.cs`)
- Added `BridgeId` property to track which bridge a room belongs to
- Added `BridgeName` property for UI display (shows bridge name when needed)

#### `AppSettings` (`Models/AppSettings.cs`)
- Deprecated `ConfiguredBridge` property (marked obsolete)
- Added `ConfiguredBridges` list to store multiple bridges
- Legacy single-bridge settings are automatically migrated on first load

### 2. Services

#### `IMultiBridgeService` (New)
Main interface for managing multiple bridges:
- `ConnectAllAsync()` - Connects to all configured bridges
- `AddBridgeAsync()` / `RemoveBridgeAsync()` - Manage bridge list
- `UpdateBridgeNameAsync()` - Rename bridges
- `GetAllRoomsAsync()` / `GetAllZonesAsync()` - Fetch from all bridges
- `GetBridgeService(bridgeId)` - Get individual bridge service instance

Events:
- `BridgeConnectionChanged` - Fired when any bridge connects/disconnects
- `LightStateChanged` - Fired when lights change on any bridge (includes bridge ID)

#### `MultiBridgeService` (New implementation)
- Manages a dictionary of `HueBridgeService` instances (one per bridge)
- Handles duplicate room/zone names by prefixing with bridge name
- Tracks connection status for each bridge
- Forwards light state change events with bridge context

### 3. ViewModels

#### `DashboardViewModel`
- Updated to use `IMultiBridgeService` instead of `IHueBridgeService`
- Loads rooms and zones from all connected bridges
- Displays them in a unified view

#### `BridgeManagementViewModel` (New)
Manages the bridge configuration UI:
- `DiscoverBridgesAsync()` - Scans network for new bridges
- Shows configured bridges with connection status
- Allows adding/removing/renaming bridges
- Per-bridge reconnect functionality

Sub-ViewModels:
- `BridgeInfoViewModel` - Represents a configured bridge
- `DiscoveredBridgeViewModel` - Represents a newly discovered bridge

#### `SettingsViewModel`
- Updated to show bridge count and connection status
- Integrated `BridgeManagementViewModel` for bridge management UI
- Migrates legacy single-bridge settings automatically

#### `SetupViewModel`
- Updated to use `IMultiBridgeService`
- Adds new bridges to the multi-bridge configuration
- Compatible with both initial setup and adding additional bridges

### 4. Dependency Injection

Updated `App.xaml.cs`:
- Removed `IHueBridgeService` singleton registration
- Added `IMultiBridgeService` singleton registration
- `MultiBridgeService` creates `HueBridgeService` instances internally

## Key Features

### 1. Auto-Discovery
The `BridgeDiscoveryService` discovers all Hue bridges on the local network using:
- HTTP-based discovery (discovery.meethue.com)
- mDNS/Bonjour for local-only networks

### 2. Bridge Management UI
Located in Settings, shows:
- List of configured bridges with:
  - Custom friendly name (editable)
  - IP address
  - Connection status (connected/disconnected/connecting)
  - Last connected timestamp
- Discovery section:
  - "Discover Bridges" button
  - List of newly found bridges
  - "Add" button with link button prompt

Actions:
- Rename bridge (click friendly name)
- Remove bridge (delete button)
- Reconnect to bridge (reconnect button)

### 3. Duplicate Room Name Handling
When multiple bridges have rooms with the same name:
- Automatically prefixes with bridge name (e.g., "Bridge A - Kitchen", "Bridge B - Kitchen")
- Only adds prefix when duplicates exist (keeps clean names otherwise)
- Applies to both rooms and zones

### 4. Connection Status Indicators
Each bridge shows:
- ✓ Connected (green) - Successfully connected
- ⚠ Disconnected (yellow) - Not currently connected
- ⏳ Connecting... (animated) - Connection in progress
- ✗ Error (red) - Connection error with message

### 5. Unified Dashboard
The main dashboard aggregates:
- All rooms from all connected bridges
- All zones from all connected bridges
- Real-time updates from all bridges
- Each card shows which bridge it belongs to (when duplicates exist)

## Migration Strategy

### Legacy Single-Bridge Support
The app automatically migrates old configurations:

1. On first load, checks if `ConfiguredBridge` exists and `ConfiguredBridges` is empty
2. Moves the single bridge to the `ConfiguredBridges` list
3. Clears `ConfiguredBridge` (set to null)
4. Saves settings

This ensures existing users seamlessly upgrade without losing their bridge configuration.

### Backward Compatibility
- `ConfiguredBridge` property is marked `[Obsolete]` but not removed
- `HasConfiguredBridgeAsync()` checks both old and new properties
- All UI components work with zero or multiple bridges

## Testing Recommendations

### Unit Tests
1. Test `MultiBridgeService`:
   - Adding/removing bridges
   - Connecting to multiple bridges
   - Fetching rooms/zones from all bridges
   - Duplicate name handling

2. Test migration logic:
   - Single bridge migration
   - Empty state
   - Multiple bridges from fresh install

### Integration Tests
1. Discovery with multiple bridges on network
2. Connection failures (one bridge down, others up)
3. Real-time updates from multiple bridges
4. UI responsiveness with many rooms/zones

### Manual Testing
1. **Fresh Install**:
   - Discover bridges
   - Add first bridge
   - Add second bridge
   - Verify unified dashboard

2. **Migration**:
   - Start with single-bridge config
   - Update app
   - Verify bridge migrated to list
   - Add second bridge

3. **Duplicate Names**:
   - Create rooms with same name on different bridges
   - Verify prefix is added
   - Rename bridge
   - Verify prefix updates

4. **Connection Management**:
   - Disconnect one bridge (unplug)
   - Verify UI shows disconnected status
   - Reconnect
   - Verify UI updates

## Future Enhancements

1. **Bridge Icons**: Add visual icons/colors for each bridge
2. **Bridge Grouping**: Group rooms/zones by bridge in navigation
3. **Per-Bridge Settings**: Light refresh interval, auto-reconnect settings
4. **Bridge Health Monitoring**: Track uptime, latency, error rates
5. **Scenes Across Bridges**: Create scenes that span multiple bridges
6. **Bridge Sync**: Synchronize settings/scenes between bridges

## Files Changed

### New Files
- `src/HueWindows.Core/Services/Interfaces/IMultiBridgeService.cs`
- `src/HueWindows.Core/Services/MultiBridgeService.cs`
- `src/HueWindows.Core/ViewModels/BridgeManagementViewModel.cs`
- `MULTIBRIDGE_IMPLEMENTATION.md`

### Modified Files
- `src/HueWindows.Core/Models/AppSettings.cs`
- `src/HueWindows.Core/Models/BridgeModel.cs`
- `src/HueWindows.Core/Models/RoomModel.cs`
- `src/HueWindows.Core/Services/SettingsService.cs`
- `src/HueWindows.Core/ViewModels/DashboardViewModel.cs`
- `src/HueWindows.Core/ViewModels/SettingsViewModel.cs`
- `src/HueWindows.Core/ViewModels/SetupViewModel.cs`
- `src/HueWindows/App.xaml.cs`
- `src/HueWindows/MainWindow.xaml.cs`

## API Surface

### IMultiBridgeService
```csharp
public interface IMultiBridgeService
{
    IReadOnlyList<BridgeModel> ConfiguredBridges { get; }
    IReadOnlyDictionary<string, bool> ConnectionStatus { get; }
    
    event EventHandler<BridgeConnectionEventArgs>? BridgeConnectionChanged;
    event EventHandler<MultiBridgeLightStateChangedEventArgs>? LightStateChanged;
    
    Task<Result> AddBridgeAsync(BridgeModel bridge);
    Task<Result> RemoveBridgeAsync(string bridgeId);
    Task<Result> UpdateBridgeNameAsync(string bridgeId, string friendlyName);
    Task ConnectAllAsync();
    Task<Result> ConnectBridgeAsync(string bridgeId);
    void DisconnectBridge(string bridgeId);
    void DisconnectAll();
    Task<Result<IReadOnlyList<RoomModel>>> GetAllRoomsAsync();
    Task<Result<IReadOnlyList<RoomModel>>> GetAllZonesAsync();
    Task<Result<IReadOnlyList<RoomModel>>> GetRoomsForBridgeAsync(string bridgeId);
    Task<Result<IReadOnlyList<RoomModel>>> GetZonesForBridgeAsync(string bridgeId);
    IHueBridgeService? GetBridgeService(string bridgeId);
}
```

## Known Limitations

1. **Command-line name resolution**: Only uses first connected bridge
2. **Navigation items**: Shows all rooms/zones but doesn't group by bridge
3. **Scene management**: Scenes are per-bridge, no cross-bridge scenes yet
4. **Network discovery timeout**: Fixed at 10 seconds

## Support

For issues or questions about the multi-bridge implementation, please refer to:
- Issue #9 on GitHub
- This implementation document
- Inline code comments in the modified files
