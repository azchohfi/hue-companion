using HueCompanion.Core.Models;

namespace HueCompanion.Core.Services.Interfaces;

/// <summary>
/// Service for managing global system hotkeys.
/// </summary>
public interface IHotkeyService : IDisposable
{
    /// <summary>
    /// Event raised when the registered hotkey is pressed.
    /// </summary>
    event EventHandler? HotkeyPressed;

    /// <summary>
    /// Gets whether a hotkey is currently registered.
    /// </summary>
    bool IsRegistered { get; }

    /// <summary>
    /// Gets the last error message if registration failed.
    /// </summary>
    string? LastError { get; }

    /// <summary>
    /// Registers the global hotkey with the specified settings.
    /// </summary>
    /// <param name="settings">The hotkey settings to register.</param>
    /// <returns>A result indicating success or failure with an error message.</returns>
    HotkeyRegistrationResult Register(HotkeySettings settings);

    /// <summary>
    /// Unregisters the currently registered hotkey.
    /// </summary>
    void Unregister();

    /// <summary>
    /// Tests if a hotkey combination is available (not in use by another application).
    /// </summary>
    /// <param name="settings">The hotkey settings to test.</param>
    /// <returns>True if the hotkey is available; otherwise, false.</returns>
    bool IsHotkeyAvailable(HotkeySettings settings);
}

/// <summary>
/// Result of a hotkey registration attempt.
/// </summary>
public class HotkeyRegistrationResult
{
    /// <summary>
    /// Gets whether the registration was successful.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets the error message if registration failed.
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// Gets the Win32 error code if registration failed.
    /// </summary>
    public int? ErrorCode { get; }

    private HotkeyRegistrationResult(bool isSuccess, string? errorMessage = null, int? errorCode = null)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
        ErrorCode = errorCode;
    }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static HotkeyRegistrationResult Success() => new(true);

    /// <summary>
    /// Creates a failure result with the specified error details.
    /// </summary>
    public static HotkeyRegistrationResult Failure(string message, int? errorCode = null) =>
        new(false, message, errorCode);

    /// <summary>
    /// Creates a conflict result when the hotkey is already registered.
    /// </summary>
    public static HotkeyRegistrationResult Conflict(string hotkeyString) =>
        new(false, $"The hotkey '{hotkeyString}' is already registered by another application.", 1409);
}
