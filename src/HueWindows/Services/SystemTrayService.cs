using System.Drawing;
using System.Runtime.InteropServices;
using HueWindows.Core.Services.Interfaces;

namespace HueWindows.Services;

/// <summary>
/// Service for managing the system tray icon and context menu using Win32 Shell_NotifyIcon.
/// </summary>
public sealed class SystemTrayService : ISystemTrayService
{
    private IntPtr _windowHandle;
    private bool _isVisible;
    private bool _isDisposed;
    private bool _isInitialized;
    private string _tooltip = "Hue Windows";

    private const int WM_USER = 0x0400;
    private const int WM_TRAYICON = WM_USER + 1;
    private const int WM_COMMAND = 0x0111;

    // Context menu item IDs
    private const int IDM_SHOW = 1001;
    private const int IDM_HIDE = 1002;
    private const int IDM_SEPARATOR = 0;
    private const int IDM_EXIT = 1099;

    private IntPtr _contextMenu;
    private IntPtr _originalWndProc;
    private WndProcDelegate? _wndProcDelegate;
    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    /// <inheritdoc/>
    public event EventHandler? ShowWindowRequested;
    /// <inheritdoc/>
    public event EventHandler? HideWindowRequested;
    /// <inheritdoc/>
    public event EventHandler? ToggleWindowRequested;
    /// <inheritdoc/>
    public event EventHandler? ExitRequested;
    /// <inheritdoc/>
    public event EventHandler? TrayIconClicked;

    /// <inheritdoc/>
    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible != value)
            {
                _isVisible = value;
                if (_isInitialized)
                {
                    if (value)
                        ShowTrayIcon();
                    else
                        HideTrayIcon();
                }
            }
        }
    }

    #region Win32 Interop

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uID;
        public NotifyIconFlags uFlags;
        public int uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public int dwState;
        public int dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public int uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public NotifyIconInfoFlags dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [Flags]
    private enum NotifyIconFlags
    {
        NIF_MESSAGE = 0x00000001,
        NIF_ICON = 0x00000002,
        NIF_TIP = 0x00000004,
        NIF_STATE = 0x00000008,
        NIF_INFO = 0x00000010,
        NIF_GUID = 0x00000020,
        NIF_REALTIME = 0x00000040,
        NIF_SHOWTIP = 0x00000080
    }

    [Flags]
    private enum NotifyIconInfoFlags
    {
        NIIF_NONE = 0x00000000,
        NIIF_INFO = 0x00000001,
        NIIF_WARNING = 0x00000002,
        NIIF_ERROR = 0x00000003,
        NIIF_USER = 0x00000004,
        NIIF_NOSOUND = 0x00000010,
        NIIF_LARGE_ICON = 0x00000020,
        NIIF_RESPECT_QUIET_TIME = 0x00000080
    }

    private enum NotifyIconMessage
    {
        NIM_ADD = 0x00000000,
        NIM_MODIFY = 0x00000001,
        NIM_DELETE = 0x00000002,
        NIM_SETFOCUS = 0x00000003,
        NIM_SETVERSION = 0x00000004
    }

    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_RBUTTONUP = 0x0205;
    private const int WM_LBUTTONDBLCLK = 0x0203;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(NotifyIconMessage dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenu(IntPtr hMenu, MenuFlags uFlags, IntPtr uIDNewItem, string lpNewItem);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    private static extern bool TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool ModifyMenu(IntPtr hMenu, uint uPosition, MenuFlags uFlags, IntPtr uIDNewItem, string lpNewItem);

    [DllImport("user32.dll")]
    private static extern bool EnableMenuItem(IntPtr hMenu, uint uIDEnableItem, MenuFlags uEnable);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr LoadImage(IntPtr hInst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

    private const uint IMAGE_ICON = 1;
    private const uint LR_LOADFROMFILE = 0x00000010;
    private const uint LR_DEFAULTSIZE = 0x00000040;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [Flags]
    private enum MenuFlags : uint
    {
        MF_STRING = 0x00000000,
        MF_SEPARATOR = 0x00000800,
        MF_ENABLED = 0x00000000,
        MF_GRAYED = 0x00000001,
        MF_DISABLED = 0x00000002,
        MF_CHECKED = 0x00000008,
        MF_UNCHECKED = 0x00000000,
        MF_BYCOMMAND = 0x00000000,
        MF_BYPOSITION = 0x00000400
    }

    private const uint TPM_LEFTALIGN = 0x0000;
    private const uint TPM_BOTTOMALIGN = 0x0020;
    private const uint TPM_RIGHTBUTTON = 0x0002;
    private const int GWLP_WNDPROC = -4;

    #endregion

    /// <inheritdoc/>
    public void Initialize(IntPtr windowHandle)
    {
        if (_isInitialized)
            throw new InvalidOperationException("SystemTrayService is already initialized.");

        _windowHandle = windowHandle;

        // Create context menu
        CreateContextMenu();

        // Subclass the window to intercept tray messages
        _wndProcDelegate = new WndProcDelegate(WndProc);
        _originalWndProc = GetWindowLongPtr(_windowHandle, GWLP_WNDPROC);
        SetWindowLongPtr(_windowHandle, GWLP_WNDPROC, Marshal.GetFunctionPointerForDelegate(_wndProcDelegate));

        _isInitialized = true;

        // Show the tray icon if visibility is set
        if (_isVisible)
            ShowTrayIcon();
    }

    private void CreateContextMenu()
    {
        _contextMenu = CreatePopupMenu();

        AppendMenu(_contextMenu, MenuFlags.MF_STRING, (IntPtr)IDM_SHOW, "Show Hue Windows");
        AppendMenu(_contextMenu, MenuFlags.MF_STRING, (IntPtr)IDM_HIDE, "Hide to Tray");
        AppendMenu(_contextMenu, MenuFlags.MF_SEPARATOR, IntPtr.Zero, string.Empty);
        AppendMenu(_contextMenu, MenuFlags.MF_STRING, (IntPtr)IDM_EXIT, "Exit");
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_TRAYICON)
        {
            var mouseMsg = lParam.ToInt32() & 0xFFFF;

            switch (mouseMsg)
            {
                case WM_LBUTTONUP:
                case WM_LBUTTONDBLCLK:
                    App.MainWindow?.DispatcherQueue.TryEnqueue(() =>
                    {
                        TrayIconClicked?.Invoke(this, EventArgs.Empty);
                        ToggleWindowRequested?.Invoke(this, EventArgs.Empty);
                    });
                    break;

                case WM_RBUTTONUP:
                    ShowContextMenu();
                    break;
            }
            return IntPtr.Zero;
        }
        else if (msg == WM_COMMAND)
        {
            var menuId = wParam.ToInt32() & 0xFFFF;
            HandleMenuCommand(menuId);
            return IntPtr.Zero;
        }

        return CallWindowProc(_originalWndProc, hWnd, msg, wParam, lParam);
    }

    private void HandleMenuCommand(int menuId)
    {
        App.MainWindow?.DispatcherQueue.TryEnqueue(() =>
        {
            switch (menuId)
            {
                case IDM_SHOW:
                    ShowWindowRequested?.Invoke(this, EventArgs.Empty);
                    break;
                case IDM_HIDE:
                    HideWindowRequested?.Invoke(this, EventArgs.Empty);
                    break;
                case IDM_EXIT:
                    ExitRequested?.Invoke(this, EventArgs.Empty);
                    break;
            }
        });
    }

    private void ShowContextMenu()
    {
        GetCursorPos(out var point);
        SetForegroundWindow(_windowHandle);
        TrackPopupMenu(_contextMenu, TPM_LEFTALIGN | TPM_RIGHTBUTTON, point.X, point.Y, 0, _windowHandle, IntPtr.Zero);
    }

    private void ShowTrayIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
        var hIcon = LoadImage(IntPtr.Zero, iconPath, IMAGE_ICON, 16, 16, LR_LOADFROMFILE);

        var nid = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _windowHandle,
            uID = 1,
            uFlags = NotifyIconFlags.NIF_ICON | NotifyIconFlags.NIF_MESSAGE | NotifyIconFlags.NIF_TIP | NotifyIconFlags.NIF_SHOWTIP,
            uCallbackMessage = WM_TRAYICON,
            hIcon = hIcon,
            szTip = _tooltip,
            szInfo = string.Empty,
            szInfoTitle = string.Empty
        };

        Shell_NotifyIcon(NotifyIconMessage.NIM_ADD, ref nid);
    }

    private void HideTrayIcon()
    {
        var nid = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _windowHandle,
            uID = 1
        };

        Shell_NotifyIcon(NotifyIconMessage.NIM_DELETE, ref nid);
    }

    /// <inheritdoc/>
    public void SetTooltip(string tooltip)
    {
        _tooltip = tooltip;

        if (!_isInitialized || !_isVisible)
            return;

        var nid = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _windowHandle,
            uID = 1,
            uFlags = NotifyIconFlags.NIF_TIP | NotifyIconFlags.NIF_SHOWTIP,
            szTip = tooltip,
            szInfo = string.Empty,
            szInfoTitle = string.Empty
        };

        Shell_NotifyIcon(NotifyIconMessage.NIM_MODIFY, ref nid);
    }

    /// <inheritdoc/>
    public void ShowBalloon(string title, string message, TrayBalloonIcon icon = TrayBalloonIcon.Info)
    {
        if (!_isInitialized || !_isVisible)
            return;

        var infoFlags = icon switch
        {
            TrayBalloonIcon.Info => NotifyIconInfoFlags.NIIF_INFO,
            TrayBalloonIcon.Warning => NotifyIconInfoFlags.NIIF_WARNING,
            TrayBalloonIcon.Error => NotifyIconInfoFlags.NIIF_ERROR,
            _ => NotifyIconInfoFlags.NIIF_NONE
        };

        var nid = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _windowHandle,
            uID = 1,
            uFlags = NotifyIconFlags.NIF_INFO,
            szTip = _tooltip,
            szInfo = message,
            szInfoTitle = title,
            dwInfoFlags = infoFlags
        };

        Shell_NotifyIcon(NotifyIconMessage.NIM_MODIFY, ref nid);
    }

    /// <inheritdoc/>
    public void UpdateMenuState(bool isWindowVisible)
    {
        if (_contextMenu == IntPtr.Zero)
            return;

        // Enable/disable menu items based on window visibility
        EnableMenuItem(_contextMenu, IDM_SHOW, isWindowVisible ? MenuFlags.MF_GRAYED : MenuFlags.MF_ENABLED);
        EnableMenuItem(_contextMenu, IDM_HIDE, isWindowVisible ? MenuFlags.MF_ENABLED : MenuFlags.MF_GRAYED);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_isDisposed)
            return;

        // Hide tray icon
        if (_isVisible && _isInitialized)
            HideTrayIcon();

        // Destroy menu
        if (_contextMenu != IntPtr.Zero)
        {
            DestroyMenu(_contextMenu);
            _contextMenu = IntPtr.Zero;
        }

        // Restore original window procedure
        if (_windowHandle != IntPtr.Zero && _originalWndProc != IntPtr.Zero)
        {
            SetWindowLongPtr(_windowHandle, GWLP_WNDPROC, _originalWndProc);
        }

        _wndProcDelegate = null;
        _isDisposed = true;
    }
}
