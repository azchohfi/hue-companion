using System.Text.Json;
using HueWindows.Core.Models;
using HueWindows.Core.Services.Interfaces;

namespace HueWindows.Core.Services;

/// <summary>
/// Service for persisting application settings to local storage.
/// </summary>
public class SettingsService : ISettingsService
{
    private const string SettingsFileName = "settings.json";
    private readonly string _settingsPath;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    /// <inheritdoc/>
    public AppSettings Settings { get; private set; } = new();

    public SettingsService()
    {
        // Store settings in LocalAppData
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(localAppData, "HueWindows");
        Directory.CreateDirectory(appFolder);
        _settingsPath = Path.Combine(appFolder, SettingsFileName);
    }

    /// <inheritdoc/>
    public async Task LoadAsync()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = await File.ReadAllTextAsync(_settingsPath);
                Settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            else
            {
                Settings = new AppSettings();
            }
        }
        catch
        {
            // If settings are corrupted, start fresh
            Settings = new AppSettings();
        }
    }

    /// <inheritdoc/>
    public async Task SaveAsync()
    {
        var json = JsonSerializer.Serialize(Settings, JsonOptions);
        await File.WriteAllTextAsync(_settingsPath, json);
    }

    /// <inheritdoc/>
    public Task<bool> HasConfiguredBridgeAsync()
    {
        var hasBridge = Settings.ConfiguredBridge != null
            && !string.IsNullOrEmpty(Settings.ConfiguredBridge.AppKey)
            && !string.IsNullOrEmpty(Settings.ConfiguredBridge.IpAddress);

        return Task.FromResult(hasBridge);
    }
}
