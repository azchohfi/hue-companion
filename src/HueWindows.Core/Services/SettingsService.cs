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
    public AppSettings Settings { get; set; } = new();

    public SettingsService(string? basePath = null)
    {
        string appFolder;
        if (basePath != null)
        {
            appFolder = basePath;
        }
        else
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            appFolder = Path.Combine(localAppData, "HueWindows");
        }
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

        // Migrate legacy single bridge to multi-bridge list (runs once at startup)
        await MigrateLegacyBridgeAsync();
    }

    /// <summary>
    /// Migrates legacy single bridge configuration to the multi-bridge list.
    /// </summary>
    private async Task MigrateLegacyBridgeAsync()
    {
        #pragma warning disable CS0618 // Type or member is obsolete
        if (Settings.ConfiguredBridge != null && Settings.ConfiguredBridges.Count == 0)
        {
            Settings.ConfiguredBridges.Add(Settings.ConfiguredBridge);
            Settings.ConfiguredBridge = null;
            await SaveAsync();
        }
        #pragma warning restore CS0618
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
        // Check new multi-bridge list first
        var hasMultiBridge = Settings.ConfiguredBridges.Any(b =>
            !string.IsNullOrEmpty(b.AppKey) && !string.IsNullOrEmpty(b.IpAddress));

        if (hasMultiBridge)
            return Task.FromResult(true);

        // Check legacy single bridge for backward compatibility
        #pragma warning disable CS0618 // Type or member is obsolete
        var hasLegacyBridge = Settings.ConfiguredBridge != null
            && !string.IsNullOrEmpty(Settings.ConfiguredBridge.AppKey)
            && !string.IsNullOrEmpty(Settings.ConfiguredBridge.IpAddress);
        #pragma warning restore CS0618

        return Task.FromResult(hasLegacyBridge);
    }
}
