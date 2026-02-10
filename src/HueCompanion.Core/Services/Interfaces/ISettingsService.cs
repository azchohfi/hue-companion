using HueCompanion.Core.Models;

namespace HueCompanion.Core.Services.Interfaces;

/// <summary>
/// Service for managing application settings persistence.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Gets the current application settings.
    /// </summary>
    AppSettings Settings { get; }

    /// <summary>
    /// Loads settings from storage.
    /// </summary>
    Task LoadAsync();

    /// <summary>
    /// Saves current settings to storage.
    /// </summary>
    Task SaveAsync();

    /// <summary>
    /// Checks if a bridge has been configured.
    /// </summary>
    Task<bool> HasConfiguredBridgeAsync();
}
