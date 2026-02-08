using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using HueWindows.Constants;
using HueWindows.Core.Models;
using HueWindows.Core.Services.Interfaces;
using HueWindows.Core.Utilities;
using HueWindows.Helpers;
using HueWindows.Utilities;
using HueWindows.Views;
using HueWindows.Services;
using WinRT.Interop;
using Windows.UI;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace HueWindows;

/// <summary>
/// The main application window with navigation shell.
/// </summary>
public sealed partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly INavigationService _navigationService;
    private readonly ISettingsService _settingsService;
    private readonly IMultiBridgeService _multiBridgeService;
    private readonly IPinnedItemsService _pinnedItemsService;
    private readonly HotkeyService _hotkeyService;
    private readonly SystemTrayService _systemTrayService;
    private readonly List<NavigationViewItem> _roomNavItems = new();
    private readonly List<NavigationViewItem> _zoneNavItems = new();
    private bool _itemsLoaded;
    private IntPtr _windowHandle;
    private bool _isExiting;
    private readonly object _exitLock = new();

    private bool _canGoBack;
    public bool CanGoBack
    {
        get => _canGoBack;
        set
        {
            if (_canGoBack != value)
            {
                _canGoBack = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #region Win32 Interop for Window Visibility

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    private const int SW_HIDE = 0;
    private const int SW_SHOW = 5;
    private const int SW_MINIMIZE = 6;
    private const int SW_RESTORE = 9;

    #endregion

    public MainWindow()
    {
        this.InitializeComponent();

        // Set up Mica backdrop
        TrySetMicaBackdrop();

        // Set up custom title bar
        SetupTitleBar();

        // Set dynamic title with version
        var appTitle = GetAppTitle();
        this.Title = appTitle;
        TitleTextBlock.Text = appTitle;

        // Set window size and icon
        _windowHandle = WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(_windowHandle);
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new Windows.Graphics.SizeInt32(AppConstants.Layout.DefaultWindowWidth, AppConstants.Layout.DefaultWindowHeight));
        appWindow.SetIcon("Assets/app.ico");

        // Get services
        _navigationService = App.Services.GetRequiredService<INavigationService>();
        _settingsService = App.Services.GetRequiredService<ISettingsService>();
        _multiBridgeService = App.Services.GetRequiredService<IMultiBridgeService>();
        _pinnedItemsService = App.Services.GetRequiredService<IPinnedItemsService>();
        _hotkeyService = App.Services.GetRequiredService<HotkeyService>();
        _systemTrayService = App.Services.GetRequiredService<SystemTrayService>();

        // Initialize hotkey and system tray services
        InitializeHotkeyService();
        InitializeSystemTrayService();

        // Subscribe to pinned items changes
        _pinnedItemsService.PinnedItemsChanged += OnPinnedItemsChanged;
        UpdateMyDashboardVisibility();

        // Subscribe to room/zone mutations for nav refresh
        _multiBridgeService.RoomsOrZonesChanged += OnRoomsOrZonesChanged;

        // Initialize navigation
        _navigationService.Initialize(ContentFrame);

        // Track navigation changes to update back button
        ContentFrame.Navigated += (s, e) => CanGoBack = _navigationService.CanGoBack;

        // Handle window closing for tray behavior
        appWindow.Closing += OnWindowClosing;

        // Navigate based on whether bridge is configured
        NavigateToInitialPage();

        // Check if we should start minimized
        if (_settingsService.Settings.StartMinimized && _settingsService.Settings.MinimizeToTray)
        {
            HideToTray();
        }
    }

    private void InitializeHotkeyService()
    {
        _hotkeyService.Initialize(_windowHandle);
        _hotkeyService.HotkeyPressed += OnHotkeyPressed;

        // Register the configured hotkey
        var hotkeySettings = _settingsService.Settings.Hotkey;
        if (hotkeySettings.IsEnabled)
        {
            var result = _hotkeyService.Register(hotkeySettings);
            if (!result.IsSuccess)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Failed to register hotkey: {result.ErrorMessage}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Hotkey registered: {hotkeySettings.DisplayString}");
            }
        }
    }

    private void InitializeSystemTrayService()
    {
        _systemTrayService.Initialize(_windowHandle);
        _systemTrayService.IsVisible = true;

        _systemTrayService.ShowWindowRequested += (s, e) => ShowFromTray();
        _systemTrayService.HideWindowRequested += (s, e) => HideToTray();
        _systemTrayService.ToggleWindowRequested += (s, e) => ToggleWindowVisibility();
        _systemTrayService.ExitRequested += (s, e) => ExitApplication();

        _systemTrayService.SetTooltip("Hue Windows");
        _systemTrayService.UpdateMenuState(IsWindowVisible(_windowHandle));
    }

    private void OnHotkeyPressed(object? sender, EventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("[MainWindow] Global hotkey pressed");
        ToggleWindowVisibility();
    }

    /// <summary>
    /// Toggles the window visibility between shown and hidden to tray.
    /// </summary>
    public void ToggleWindowVisibility()
    {
        // Use Win32 to check actual window visibility state
        var isActuallyVisible = IsWindowVisible(_windowHandle);

        if (isActuallyVisible)
        {
            HideToTray();
        }
        else
        {
            ShowFromTray();
        }
    }

    /// <summary>
    /// Shows the window from the system tray.
    /// </summary>
    public void ShowFromTray()
    {
        if (IsIconic(_windowHandle))
        {
            ShowWindow(_windowHandle, SW_RESTORE);
        }
        else
        {
            ShowWindow(_windowHandle, SW_SHOW);
        }

        SetForegroundWindow(_windowHandle);
        this.Activate();

        _systemTrayService.UpdateMenuState(true);
        System.Diagnostics.Debug.WriteLine("[MainWindow] Window shown from tray");
    }

    /// <summary>
    /// Hides the window to the system tray.
    /// </summary>
    public void HideToTray()
    {
        ShowWindow(_windowHandle, SW_HIDE);
        _systemTrayService.UpdateMenuState(false);
        System.Diagnostics.Debug.WriteLine("[MainWindow] Window hidden to tray");
    }

    private void OnWindowClosing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
    {
        // Prevent race condition with ExitApplication
        lock (_exitLock)
        {
            if (_isExiting)
            {
                // Already exiting via ExitApplication, allow close
                return;
            }

            // If minimize to tray is enabled, hide instead of closing
            if (_settingsService.Settings.MinimizeToTray)
            {
                args.Cancel = true;
                HideToTray();
            }
            else
            {
                // Clean up services
                CleanupServices();
            }
        }
    }

    private void ExitApplication()
    {
        lock (_exitLock)
        {
            if (_isExiting)
                return;

            _isExiting = true;
        }

        // Clean up services before exiting
        CleanupServices();

        Application.Current.Exit();
    }

    private void CleanupServices()
    {
        try
        {
            _hotkeyService.Dispose();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Error disposing HotkeyService: {ex.Message}");
        }

        try
        {
            _systemTrayService.Dispose();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Error disposing SystemTrayService: {ex.Message}");
        }
    }

    private string GetAppTitle()
    {
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        var titleAttr = System.Reflection.CustomAttributeExtensions.GetCustomAttribute<System.Reflection.AssemblyTitleAttribute>(assembly);
        var infoVersion = System.Reflection.CustomAttributeExtensions.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(assembly);

        var title = titleAttr?.Title ?? "Hue Companion";
        var version = infoVersion?.InformationalVersion ?? "0.0.0";

        return $"{title} - {version}";
    }

    private void SetupTitleBar()
    {
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
    }

    private async void NavigateToInitialPage()
    {
        try
        {
            var hasBridge = await _settingsService.HasConfiguredBridgeAsync();
            var cmdArgs = App.CommandLineArgs;

            if (hasBridge)
            {
                // Auto-connect to all configured bridges
                await _multiBridgeService.ConnectAllAsync();

                // Get navigation target from command-line args
                // Note: For multi-bridge, name resolution uses first connected bridge
                NavigationTarget? navTarget = null;
                if (cmdArgs.IsValid && cmdArgs.Page != null)
                {
                    if (!string.IsNullOrEmpty(cmdArgs.Name))
                    {
                        // Search all bridges for name resolution
                        navTarget = await NavigationTarget.ResolveAsync(cmdArgs, _multiBridgeService);
                    }
                    else
                    {
                        navTarget = NavigationTarget.FromCommandLineArgs(cmdArgs);
                    }
                }

                if (navTarget != null)
                {
                    // Command-line navigation takes precedence
                    NavigateToTarget(navTarget);
                }
                else
                {
                    // Default navigation behavior
                    if (_pinnedItemsService.HasPinnedItems)
                    {
                        _navigationService.NavigateTo<MyDashboardPage>();
                        NavView.SelectedItem = MyDashboardNavItem;
                    }
                    else
                    {
                        _navigationService.NavigateTo<DashboardPage>();
                        NavView.SelectedItem = DashboardNavItem;
                    }
                }

                // Set up screenshot mode timer if enabled
                if (cmdArgs.ScreenshotMode)
                {
                    SetupScreenshotModeTimer(cmdArgs.ScreenshotDelayMs);
                }
            }
            else
            {
                // No bridge configured - can only navigate to setup/settings (no name resolution possible)
                NavigationTarget? navTarget = null;
                if (cmdArgs.IsValid && cmdArgs.Page != null)
                {
                    // Only use sync resolution (can't resolve names without bridge connection)
                    navTarget = NavigationTarget.FromCommandLineArgs(cmdArgs);
                }

                if (navTarget != null && (navTarget.PageType == typeof(SetupPage) || navTarget.PageType == typeof(SettingsPage)))
                {
                    NavigateToTarget(navTarget);

                    if (cmdArgs.ScreenshotMode)
                    {
                        SetupScreenshotModeTimer(cmdArgs.ScreenshotDelayMs);
                    }
                }
                else
                {
                    _navigationService.NavigateTo<SetupPage>();
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] NavigateToInitialPage failed: {ex.Message}");
        }
    }

    private void NavigateToTarget(NavigationTarget target)
    {
        _navigationService.NavigateTo(target.PageType, target.Parameter);
        UpdateNavViewSelection(target);
    }

    private void UpdateNavViewSelection(NavigationTarget target)
    {
        if (target.PageType == typeof(DashboardPage))
            NavView.SelectedItem = DashboardNavItem;
        else if (target.PageType == typeof(MyDashboardPage))
            NavView.SelectedItem = MyDashboardNavItem;
        else if (target.PageType == typeof(RoomsPage))
            NavView.SelectedItem = RoomsNavItem;
        else if (target.PageType == typeof(ZonesPage))
            NavView.SelectedItem = ZonesNavItem;
        else if (target.PageType == typeof(ScenesPage))
            NavView.SelectedItem = ScenesNavItem;
        else if (target.PageType == typeof(SettingsPage))
            NavView.SelectedItem = NavView.SettingsItem;
        // For room/zone/light detail pages, don't set selection (they're drill-down pages)
    }

    private void SetupScreenshotModeTimer(int delayMs)
    {
        var timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(delayMs)
        };

        timer.Tick += (s, e) =>
        {
            timer.Stop();
            Application.Current.Exit();
        };

        timer.Start();
    }

    private void OnPinnedItemsChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(UpdateMyDashboardVisibility);
    }

    private void UpdateMyDashboardVisibility()
    {
        MyDashboardNavItem.Visibility = _pinnedItemsService.HasPinnedItems
            ? Visibility.Visible
            : Visibility.Collapsed;
    }


    private bool TrySetMicaBackdrop()
    {
        if (MicaController.IsSupported())
        {
            SystemBackdrop = new MicaBackdrop { Kind = MicaKind.Base };
            return true;
        }

        // Fallback to Acrylic if Mica not supported
        if (DesktopAcrylicController.IsSupported())
        {
            SystemBackdrop = new DesktopAcrylicBackdrop();
            return true;
        }

        return false;
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (_navigationService.CanGoBack)
        {
            _navigationService.GoBack();
        }
    }

    private void PaneToggleButton_Click(object sender, RoutedEventArgs e)
    {
        NavView.IsPaneOpen = !NavView.IsPaneOpen;
    }

    private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.IsSettingsInvoked)
        {
            _navigationService.NavigateTo<SettingsPage>();
            return;
        }

        if (args.InvokedItemContainer is NavigationViewItem item)
        {
            switch (item.Tag)
            {
                case "MyDashboard":
                    _navigationService.NavigateTo<MyDashboardPage>();
                    break;
                case "Dashboard":
                    _navigationService.NavigateTo<DashboardPage>();
                    break;
                case "Rooms":
                    _navigationService.NavigateTo<RoomsPage>();
                    break;
                case "Zones":
                    _navigationService.NavigateTo<ZonesPage>();
                    break;
                case "Scenes":
                    _navigationService.NavigateTo<ScenesPage>();
                    break;
                case NavigationTag navTag:
                    _navigationService.NavigateTo<RoomDetailPage>(navTag);
                    break;
            }
        }
    }

    private async void NavView_PaneOpened(NavigationView sender, object args)
    {
        try
        {
            // Load child items when pane opens
            if (!_itemsLoaded && _multiBridgeService.ConnectionStatus.Any(kvp => kvp.Value))
            {
                await LoadChildNavigationItemsAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] NavView_PaneOpened failed: {ex.Message}");
        }
    }

    private void NavView_PaneClosed(NavigationView sender, object args)
    {
        // Collapse parent items when pane closes
        ZonesNavItem.IsExpanded = false;
        RoomsNavItem.IsExpanded = false;
    }

    private async void OnRoomsOrZonesChanged(object? sender, EventArgs e)
    {
        // Refresh nav items on UI thread
        if (DispatcherQueue?.HasThreadAccess == true)
        {
            await RefreshNavigationItemsAsync();
        }
        else
        {
            DispatcherQueue?.TryEnqueue(async () => await RefreshNavigationItemsAsync());
        }
    }

    private async Task RefreshNavigationItemsAsync()
    {
        _itemsLoaded = false;
        await LoadChildNavigationItemsAsync();
    }

    private async Task LoadChildNavigationItemsAsync()
    {
        // Clear existing items to allow re-loading
        foreach (var item in _zoneNavItems)
            ZonesNavItem.MenuItems.Remove(item);
        _zoneNavItems.Clear();

        foreach (var item in _roomNavItems)
            RoomsNavItem.MenuItems.Remove(item);
        _roomNavItems.Clear();

        // Load zones from all bridges
        var zonesResult = await _multiBridgeService.GetAllZonesAsync();
        if (zonesResult.IsSuccess)
        {
            foreach (var zone in zonesResult.Value!)
            {
                var navItem = CreateNavItemForGroup(zone);
                ZonesNavItem.MenuItems.Add(navItem);
                _zoneNavItems.Add(navItem);
            }
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Error loading zones: {zonesResult.Error}");
        }

        // Load rooms from all bridges
        var roomsResult = await _multiBridgeService.GetAllRoomsAsync();
        if (roomsResult.IsSuccess)
        {
            foreach (var room in roomsResult.Value!)
            {
                var navItem = CreateNavItemForGroup(room);
                RoomsNavItem.MenuItems.Add(navItem);
                _roomNavItems.Add(navItem);
            }
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Error loading rooms: {roomsResult.Error}");
        }

        _itemsLoaded = true;
    }

    private NavigationViewItem CreateNavItemForGroup(RoomModel group)
    {
        return new NavigationViewItem
        {
            Content = group.Name,
            Tag = new NavigationTag(group.GroupType, group.Id),
            Icon = new FontIcon { Glyph = RoomIconHelper.GetIconForRoom(group.Id, group.Archetype, _settingsService.Settings.CustomRoomIcons) },
            SelectsOnInvoked = true
        };
    }

    /// <summary>
    /// Updates the nav item icon for a specific room/zone.
    /// Called when the user changes a custom icon from the room detail page.
    /// </summary>
    public void UpdateNavItemIcon(Guid groupId, string glyph)
    {
        foreach (var navItem in _roomNavItems.Concat(_zoneNavItems))
        {
            if (navItem.Tag is NavigationTag tag && tag.Id == groupId)
            {
                navItem.Icon = new FontIcon { Glyph = glyph };
                break;
            }
        }
    }

    /// <summary>
    /// Updates the ambient color wash behind the content area.
    /// Called by pages when room light colors change.
    /// </summary>
    public void UpdateAmbientColor(IReadOnlyList<(byte R, byte G, byte B)> colors)
    {
        var brush = BrushFactory.CreateRadialAmbientBrush(colors);
        AmbientWash.Background = brush;

        // Fade in the wash if not already visible
        if (AmbientWash.Opacity < 1.0)
        {
            AnimationHelper.AnimateOpacity(AmbientWash, 1.0, AppConstants.Animation.SlowDurationMs);
        }
    }

    /// <summary>
    /// Updates the NavigationView selection indicator color to match room colors.
    /// </summary>
    public void UpdateNavIndicatorColor(Color color)
    {
        NavView.Resources["NavigationViewSelectionIndicatorForeground"] = new SolidColorBrush(color);
    }

}
