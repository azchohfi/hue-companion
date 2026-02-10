using System.Text.Json;
using HueCompanion.Core.Models;
using HueCompanion.Core.Services.Interfaces;

namespace HueCompanion.Mcp.Services;

/// <summary>
/// Settings service that reads bridge configuration from a shared JSON file.
/// The WinUI app exports bridge config to %LOCALAPPDATA%/HueCompanion/bridges.json.
/// Falls back to the main settings.json if bridges.json doesn't exist.
/// </summary>
public class FileSettingsService : ISettingsService
{
    private readonly string _bridgesPath;
    private readonly string _settingsPath;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public AppSettings Settings { get; private set; } = new();

    public FileSettingsService()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(localAppData, "HueCompanion");
        _bridgesPath = Path.Combine(appFolder, "bridges.json");
        _settingsPath = Path.Combine(appFolder, "settings.json");
    }

    public async Task LoadAsync()
    {
        // Try bridges.json first (shared export from WinUI app)
        if (File.Exists(_bridgesPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_bridgesPath);
                var bridges = JsonSerializer.Deserialize<List<BridgeModel>>(json, JsonOptions);
                if (bridges is { Count: > 0 })
                {
                    Settings.ConfiguredBridges = bridges;
                    return;
                }
            }
            catch (JsonException) { /* malformed JSON, fall through */ }
        }

        // Fall back to settings.json
        if (File.Exists(_settingsPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_settingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (settings != null)
                {
                    Settings = settings;
                    return;
                }
            }
            catch (JsonException) { /* malformed JSON, fall through */ }
        }

        Settings = new AppSettings();
    }

    public async Task SaveAsync()
    {
        var dir = Path.GetDirectoryName(_settingsPath)!;
        Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(Settings, JsonOptions);
        await File.WriteAllTextAsync(_settingsPath, json);
    }

    public Task<bool> HasConfiguredBridgeAsync()
    {
        return Task.FromResult(Settings.ConfiguredBridges.Count > 0);
    }
}
