using System.Collections.Concurrent;
using HueWindows.Core.Models;
using HueWindows.Core.Services.Interfaces;

namespace HueWindows.Core.Services;

/// <summary>
/// Service for managing and communicating with multiple Hue bridges.
/// </summary>
public class MultiBridgeService : IMultiBridgeService, IDisposable
{
    private readonly ISettingsService _settingsService;
    private readonly ConcurrentDictionary<string, IHueBridgeService> _bridgeServices = new();
    private readonly ConcurrentDictionary<string, bool> _connectionStatus = new();
    private readonly object _connectionLock = new();
    private bool _disposed;

    public IReadOnlyList<BridgeModel> ConfiguredBridges =>
        _settingsService.Settings.ConfiguredBridges;

    public IReadOnlyDictionary<string, bool> ConnectionStatus =>
        _connectionStatus;

    public event EventHandler<BridgeConnectionEventArgs>? BridgeConnectionChanged;
    public event EventHandler<MultiBridgeLightStateChangedEventArgs>? LightStateChanged;

    public MultiBridgeService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public async Task<Result> AddBridgeAsync(BridgeModel bridge)
    {
        // Check if bridge already exists
        if (_settingsService.Settings.ConfiguredBridges.Any(b => b.BridgeId == bridge.BridgeId))
        {
            return Result.Failure("A bridge with this ID is already configured.");
        }

        // Add to settings
        _settingsService.Settings.ConfiguredBridges.Add(bridge);
        await _settingsService.SaveAsync();

        // Try to connect
        await ConnectBridgeAsync(bridge.BridgeId);

        return Result.Success();
    }

    public async Task<Result> RemoveBridgeAsync(string bridgeId)
    {
        // Disconnect first
        DisconnectBridge(bridgeId);

        // Remove from settings
        var bridge = _settingsService.Settings.ConfiguredBridges
            .FirstOrDefault(b => b.BridgeId == bridgeId);

        if (bridge != null)
        {
            _settingsService.Settings.ConfiguredBridges.Remove(bridge);
            await _settingsService.SaveAsync();
        }

        // Clean up service
        _bridgeServices.TryRemove(bridgeId, out _);
        _connectionStatus.TryRemove(bridgeId, out _);

        return Result.Success();
    }

    public async Task<Result> UpdateBridgeNameAsync(string bridgeId, string friendlyName)
    {
        var bridge = _settingsService.Settings.ConfiguredBridges
            .FirstOrDefault(b => b.BridgeId == bridgeId);

        if (bridge == null)
        {
            return Result.Failure("Bridge not found.");
        }

        bridge.FriendlyName = friendlyName;
        await _settingsService.SaveAsync();

        return Result.Success();
    }

    public async Task ConnectAllAsync()
    {
        var tasks = _settingsService.Settings.ConfiguredBridges
            .Select(bridge => ConnectBridgeAsync(bridge.BridgeId))
            .ToList();

        await Task.WhenAll(tasks);
    }

    public async Task<Result> ConnectBridgeAsync(string bridgeId)
    {
        var bridge = _settingsService.Settings.ConfiguredBridges
            .FirstOrDefault(b => b.BridgeId == bridgeId);

        if (bridge == null)
        {
            return Result.Failure("Bridge not found in configuration.");
        }

        IHueBridgeService service;

        // Create service if it doesn't exist (thread-safe)
        service = _bridgeServices.GetOrAdd(bridgeId, _ =>
        {
            var newService = new HueBridgeService();

            // Subscribe to events - these will handle status updates
            newService.Connected += (s, e) => OnBridgeConnected(bridgeId);
            newService.Disconnected += (s, e) => OnBridgeDisconnected(bridgeId);
            newService.LightStateChanged += (s, e) => OnLightStateChanged(bridgeId, e);

            return newService;
        });

        // Connect - let the event handlers manage status updates to avoid duplicates
        var result = await service.ConnectAsync(bridge.IpAddress, bridge.AppKey);

        if (result.IsSuccess)
        {
            bridge.LastConnected = DateTime.UtcNow;
            await _settingsService.SaveAsync();
        }
        // Note: Connection status and events are handled by OnBridgeConnected/OnBridgeDisconnected

        return result;
    }

    public void DisconnectBridge(string bridgeId)
    {
        if (_bridgeServices.TryGetValue(bridgeId, out var service))
        {
            service.Disconnect();
            _connectionStatus[bridgeId] = false;

            BridgeConnectionChanged?.Invoke(this, new BridgeConnectionEventArgs
            {
                BridgeId = bridgeId,
                IsConnected = false
            });
        }
    }

    public void DisconnectAll()
    {
        foreach (var bridgeId in _bridgeServices.Keys.ToList())
        {
            DisconnectBridge(bridgeId);
        }
    }

    public async Task<Result<IReadOnlyList<RoomModel>>> GetAllRoomsAsync()
    {
        var allRooms = new List<RoomModel>();
        var errors = new List<string>();

        foreach (var bridge in _settingsService.Settings.ConfiguredBridges)
        {
            var result = await GetRoomsForBridgeAsync(bridge.BridgeId);
            if (result.IsSuccess && result.Value != null)
            {
                allRooms.AddRange(result.Value);
            }
            else if (result.Error != null)
            {
                errors.Add($"{bridge.DisplayName}: {result.Error}");
            }
        }

        // Handle duplicate room names by marking them to show bridge prefix
        var roomsByName = allRooms.GroupBy(r => r.Name).ToList();
        foreach (var group in roomsByName.Where(g => g.Count() > 1))
        {
            foreach (var room in group)
            {
                room.ShowBridgePrefix = true;
            }
        }

        if (allRooms.Count == 0 && errors.Count > 0)
        {
            return Result<IReadOnlyList<RoomModel>>.Failure(string.Join("; ", errors));
        }

        return Result<IReadOnlyList<RoomModel>>.Success(allRooms);
    }

    public async Task<Result<IReadOnlyList<RoomModel>>> GetAllZonesAsync()
    {
        var allZones = new List<RoomModel>();
        var errors = new List<string>();

        foreach (var bridge in _settingsService.Settings.ConfiguredBridges)
        {
            var result = await GetZonesForBridgeAsync(bridge.BridgeId);
            if (result.IsSuccess && result.Value != null)
            {
                allZones.AddRange(result.Value);
            }
            else if (result.Error != null)
            {
                errors.Add($"{bridge.DisplayName}: {result.Error}");
            }
        }

        // Handle duplicate zone names by marking them to show bridge prefix
        var zonesByName = allZones.GroupBy(z => z.Name).ToList();
        foreach (var group in zonesByName.Where(g => g.Count() > 1))
        {
            foreach (var zone in group)
            {
                zone.ShowBridgePrefix = true;
            }
        }

        if (allZones.Count == 0 && errors.Count > 0)
        {
            return Result<IReadOnlyList<RoomModel>>.Failure(string.Join("; ", errors));
        }

        return Result<IReadOnlyList<RoomModel>>.Success(allZones);
    }

    public async Task<Result<IReadOnlyList<RoomModel>>> GetRoomsForBridgeAsync(string bridgeId)
    {
        if (!_bridgeServices.TryGetValue(bridgeId, out var service))
        {
            return Result<IReadOnlyList<RoomModel>>.Failure("Bridge service not found. Please connect first.");
        }

        if (!service.IsConnected)
        {
            return Result<IReadOnlyList<RoomModel>>.Failure("Bridge is not connected.");
        }

        var result = await service.GetRoomsAsync();
        if (result.IsSuccess && result.Value != null)
        {
            var bridge = _settingsService.Settings.ConfiguredBridges
                .FirstOrDefault(b => b.BridgeId == bridgeId);

            // Add bridge information to each room
            foreach (var room in result.Value)
            {
                room.BridgeId = bridgeId;
                room.BridgeName = bridge?.DisplayName;
            }
        }

        return result;
    }

    public async Task<Result<IReadOnlyList<RoomModel>>> GetZonesForBridgeAsync(string bridgeId)
    {
        if (!_bridgeServices.TryGetValue(bridgeId, out var service))
        {
            return Result<IReadOnlyList<RoomModel>>.Failure("Bridge service not found. Please connect first.");
        }

        if (!service.IsConnected)
        {
            return Result<IReadOnlyList<RoomModel>>.Failure("Bridge is not connected.");
        }

        var result = await service.GetZonesAsync();
        if (result.IsSuccess && result.Value != null)
        {
            var bridge = _settingsService.Settings.ConfiguredBridges
                .FirstOrDefault(b => b.BridgeId == bridgeId);

            // Add bridge information to each zone
            foreach (var zone in result.Value)
            {
                zone.BridgeId = bridgeId;
                zone.BridgeName = bridge?.DisplayName;
            }
        }

        return result;
    }

    public IHueBridgeService? GetBridgeService(string bridgeId)
    {
        _bridgeServices.TryGetValue(bridgeId, out var service);
        return service;
    }

    /// <summary>
    /// Gets the default bridge service (first connected bridge) for backward compatibility.
    /// </summary>
    public IHueBridgeService? GetDefaultBridgeService()
    {
        // Return first connected bridge service
        foreach (var kvp in _connectionStatus)
        {
            if (kvp.Value && _bridgeServices.TryGetValue(kvp.Key, out var service))
            {
                return service;
            }
        }

        // Fallback to first bridge service regardless of connection status
        return _bridgeServices.Values.FirstOrDefault();
    }

    private void OnBridgeConnected(string bridgeId)
    {
        _connectionStatus[bridgeId] = true;
        BridgeConnectionChanged?.Invoke(this, new BridgeConnectionEventArgs
        {
            BridgeId = bridgeId,
            IsConnected = true
        });
    }

    private void OnBridgeDisconnected(string bridgeId)
    {
        _connectionStatus[bridgeId] = false;
        BridgeConnectionChanged?.Invoke(this, new BridgeConnectionEventArgs
        {
            BridgeId = bridgeId,
            IsConnected = false
        });
    }

    private void OnLightStateChanged(string bridgeId, LightStateChangedEventArgs e)
    {
        LightStateChanged?.Invoke(this, new MultiBridgeLightStateChangedEventArgs
        {
            BridgeId = bridgeId,
            LightId = e.LightId,
            IsOn = e.IsOn,
            Brightness = e.Brightness,
            Color = e.Color
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        DisconnectAll();

        // Dispose any disposable bridge services
        foreach (var service in _bridgeServices.Values)
        {
            if (service is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        _bridgeServices.Clear();
        _connectionStatus.Clear();
    }
}
