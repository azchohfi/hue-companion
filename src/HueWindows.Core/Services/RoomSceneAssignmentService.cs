using System.Text.Json;
using HueWindows.Core.Services.Interfaces;

namespace HueWindows.Core.Services;

/// <summary>
/// Service for persisting animated scene assignments to rooms.
/// </summary>
public class RoomSceneAssignmentService : IRoomSceneAssignmentService
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private Dictionary<Guid, List<string>> _assignments = new();
    private bool _loaded;

    public RoomSceneAssignmentService(string? filePath = null)
    {
        if (filePath != null)
        {
            _filePath = filePath;
        }
        else
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var folder = Path.Combine(appData, "HueWindows");
            Directory.CreateDirectory(folder);
            _filePath = Path.Combine(folder, "room-scene-assignments.json");
        }
    }

    private async Task EnsureLoadedAsync()
    {
        if (_loaded) return;

        await _lock.WaitAsync();
        try
        {
            if (_loaded) return; // Double-check after acquiring lock

            if (File.Exists(_filePath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(_filePath);
                    var data = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json);
                    if (data != null)
                    {
                        _assignments = data.ToDictionary(
                            kvp => Guid.Parse(kvp.Key),
                            kvp => kvp.Value
                        );
                    }
                }
                catch
                {
                    _assignments = new();
                }
            }
            _loaded = true;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task SaveAsync()
    {
        var data = _assignments.ToDictionary(
            kvp => kvp.Key.ToString(),
            kvp => kvp.Value
        );
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_filePath, json);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetAssignedScenesAsync(Guid roomId)
    {
        await EnsureLoadedAsync();
        await _lock.WaitAsync();
        try
        {
            return _assignments.TryGetValue(roomId, out var scenes) ? scenes.ToList() : Array.Empty<string>();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task AssignSceneToRoomAsync(Guid roomId, string sceneId)
    {
        await EnsureLoadedAsync();
        await _lock.WaitAsync();
        try
        {
            if (!_assignments.TryGetValue(roomId, out var scenes))
            {
                scenes = new List<string>();
                _assignments[roomId] = scenes;
            }

            if (!scenes.Contains(sceneId))
            {
                scenes.Add(sceneId);
                await SaveAsync();
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task RemoveSceneFromRoomAsync(Guid roomId, string sceneId)
    {
        await EnsureLoadedAsync();
        await _lock.WaitAsync();
        try
        {
            if (_assignments.TryGetValue(roomId, out var scenes))
            {
                scenes.Remove(sceneId);
                await SaveAsync();
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<Guid, List<string>>> GetAllAssignmentsAsync()
    {
        await EnsureLoadedAsync();
        await _lock.WaitAsync();
        try
        {
            return _assignments.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToList());
        }
        finally
        {
            _lock.Release();
        }
    }
}
