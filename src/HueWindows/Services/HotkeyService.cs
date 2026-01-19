using System.ComponentModel;
using System.Runtime.InteropServices;
using HueWindows.Core.Models;
using HueWindows.Core.Services.Interfaces;
using Microsoft.UI.Xaml;

namespace HueWindows.Services;

/// <summary>
/// Service for managing global system hotkeys using Win32 interop.
/// </summary>
public sealed class HotkeyService : IHotkeyService
{
    private const int WM_HOTKEY = 0x0312;
    private const int HOTKEY_ID = 0x0001;

    // Win32 error codes
    private const int ERROR_HOTKEY_ALREADY_REGISTERED = 1409;

    private IntPtr _windowHandle;
    private bool _isRegistered;
    private HotkeySettings? _currentSettings;
    private bool _isDisposed;

    /// <inheritdoc/>
    public event EventHandler? HotkeyPressed;

    /// <inheritdoc/>
    public bool IsRegistered => _isRegistered;

    /// <inheritdoc/>
    public string? LastError { get; private set; }

    #region Win32 Interop

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    private const int GWLP_WNDPROC = -4;

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    private WndProcDelegate? _wndProcDelegate;
    private IntPtr _originalWndProc;

    #endregion

    /// <summary>
    /// Initializes the hotkey service with the specified window handle.
    /// </summary>
    /// <param name="windowHandle">The main window handle to receive hotkey messages.</param>
    public void Initialize(IntPtr windowHandle)
    {
        if (_windowHandle != IntPtr.Zero)
            throw new InvalidOperationException("HotkeyService is already initialized.");

        _windowHandle = windowHandle;

        // Subclass the window to intercept WM_HOTKEY messages
        _wndProcDelegate = new WndProcDelegate(WndProc);
        _originalWndProc = GetWindowLongPtr(_windowHandle, GWLP_WNDPROC);
        SetWindowLongPtr(_windowHandle, GWLP_WNDPROC, Marshal.GetFunctionPointerForDelegate(_wndProcDelegate));
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
        {
            // Dispatch to UI thread
            var mainWindow = App.MainWindow;
            if (mainWindow != null)
            {
                mainWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    HotkeyPressed?.Invoke(this, EventArgs.Empty);
                });
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[HotkeyService] WM_HOTKEY received but MainWindow is null - hotkey event dropped");
            }
            return IntPtr.Zero;
        }

        return CallWindowProc(_originalWndProc, hWnd, msg, wParam, lParam);
    }

    /// <inheritdoc/>
    public HotkeyRegistrationResult Register(HotkeySettings settings)
    {
        if (_windowHandle == IntPtr.Zero)
            return HotkeyRegistrationResult.Failure("HotkeyService not initialized. Call Initialize() first.");

        if (!settings.IsEnabled)
            return HotkeyRegistrationResult.Success();

        // Unregister existing hotkey if any
        if (_isRegistered)
            Unregister();

        var modifiers = (uint)settings.Modifiers;
        var vk = (uint)settings.Key;

        // Add MOD_NOREPEAT to prevent multiple events while holding the key
        const uint MOD_NOREPEAT = 0x4000;
        modifiers |= MOD_NOREPEAT;

        if (!RegisterHotKey(_windowHandle, HOTKEY_ID, modifiers, vk))
        {
            var errorCode = Marshal.GetLastWin32Error();
            LastError = GetErrorMessage(errorCode, settings);

            if (errorCode == ERROR_HOTKEY_ALREADY_REGISTERED)
            {
                return HotkeyRegistrationResult.Conflict(settings.DisplayString);
            }

            return HotkeyRegistrationResult.Failure(LastError, errorCode);
        }

        _isRegistered = true;
        _currentSettings = settings;
        LastError = null;

        System.Diagnostics.Debug.WriteLine($"[HotkeyService] Registered hotkey: {settings.DisplayString}");
        return HotkeyRegistrationResult.Success();
    }

    /// <inheritdoc/>
    public void Unregister()
    {
        if (_windowHandle == IntPtr.Zero || !_isRegistered)
            return;

        if (UnregisterHotKey(_windowHandle, HOTKEY_ID))
        {
            System.Diagnostics.Debug.WriteLine($"[HotkeyService] Unregistered hotkey: {_currentSettings?.DisplayString}");
        }
        else
        {
            var errorCode = Marshal.GetLastWin32Error();
            System.Diagnostics.Debug.WriteLine($"[HotkeyService] Failed to unregister hotkey. Error: {errorCode}");
        }

        _isRegistered = false;
        _currentSettings = null;
    }

    /// <inheritdoc/>
    public bool IsHotkeyAvailable(HotkeySettings settings)
    {
        if (_windowHandle == IntPtr.Zero)
            return false;

        var modifiers = (uint)settings.Modifiers;
        var vk = (uint)settings.Key;
        const uint MOD_NOREPEAT = 0x4000;
        modifiers |= MOD_NOREPEAT;

        // Try to register with a temporary ID
        const int TEST_HOTKEY_ID = 0x9999;

        if (RegisterHotKey(_windowHandle, TEST_HOTKEY_ID, modifiers, vk))
        {
            UnregisterHotKey(_windowHandle, TEST_HOTKEY_ID);
            return true;
        }

        return false;
    }

    private static string GetErrorMessage(int errorCode, HotkeySettings settings)
    {
        return errorCode switch
        {
            ERROR_HOTKEY_ALREADY_REGISTERED =>
                $"The hotkey '{settings.DisplayString}' is already registered by another application.",
            1400 => "Invalid window handle.",
            1401 => "Invalid hotkey configuration.",
            _ => $"Failed to register hotkey. Win32 error code: {errorCode}"
        };
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_isDisposed)
            return;

        Unregister();

        // Restore original window procedure
        if (_windowHandle != IntPtr.Zero && _originalWndProc != IntPtr.Zero)
        {
            SetWindowLongPtr(_windowHandle, GWLP_WNDPROC, _originalWndProc);
        }

        _wndProcDelegate = null;
        _isDisposed = true;
    }
}
