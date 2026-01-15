using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using HueWindows.Constants;
using HueWindows.Core.Models;
using HueWindows.Core.Services.Interfaces;
using HueWindows.Helpers;
using HueWindows.Views;
using WinRT.Interop;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HueWindows;

/// <summary>
/// The main application window with navigation shell.
/// </summary>
public sealed partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly INavigationService _navigationService;
    private readonly ISettingsService _settingsService;
    private readonly IHueBridgeService _bridgeService;
    private readonly IPinnedItemsService _pinnedItemsService;
    private readonly List<NavigationViewItem> _roomNavItems = new();
    private readonly List<NavigationViewItem> _zoneNavItems = new();
    private bool _itemsLoaded;

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

    public MainWindow()
    {
        this.InitializeComponent();

        // Set up Mica backdrop
        TrySetMicaBackdrop();

        // Set up custom title bar
        SetupTitleBar();

        // Set window size and icon
        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new Windows.Graphics.SizeInt32(AppConstants.Layout.DefaultWindowWidth, AppConstants.Layout.DefaultWindowHeight));
        appWindow.SetIcon("Assets/app.ico");

        // Get services
        _navigationService = App.Services.GetRequiredService<INavigationService>();
        _settingsService = App.Services.GetRequiredService<ISettingsService>();
        _bridgeService = App.Services.GetRequiredService<IHueBridgeService>();
        _pinnedItemsService = App.Services.GetRequiredService<IPinnedItemsService>();

        // Subscribe to pinned items changes
        _pinnedItemsService.PinnedItemsChanged += OnPinnedItemsChanged;
        UpdateMyDashboardVisibility();

        // Initialize navigation
        _navigationService.Initialize(ContentFrame);

        // Track navigation changes to update back button
        ContentFrame.Navigated += (s, e) => CanGoBack = _navigationService.CanGoBack;

        // Navigate based on whether bridge is configured
        NavigateToInitialPage();
    }

    private void SetupTitleBar()
    {
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
    }

    private async void NavigateToInitialPage()
    {
        var hasBridge = await _settingsService.HasConfiguredBridgeAsync();

        // Get navigation target from command-line args
        var cmdArgs = App.CommandLineArgs;
        var navTarget = cmdArgs.IsValid ? NavigationTarget.FromCommandLineArgs(cmdArgs) : null;

        if (hasBridge)
        {
            // Auto-connect to the saved bridge
            var bridge = _settingsService.Settings.ConfiguredBridge!;
            var connectResult = await _bridgeService.ConnectAsync(bridge.IpAddress!, bridge.AppKey!);

            if (connectResult.IsSuccess)
            {
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
                // Connection failed, go to setup to re-pair
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Connection failed: {connectResult.Error}");
                _navigationService.NavigateTo<SetupPage>();
            }
        }
        else
        {
            // No bridge configured - can still navigate to setup/settings if requested
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
                case NavigationTag navTag:
                    _navigationService.NavigateTo<RoomDetailPage>(navTag);
                    break;
            }
        }
    }

    private async void NavView_PaneOpened(NavigationView sender, object args)
    {
        // Load child items when pane opens
        if (!_itemsLoaded && _bridgeService.IsConnected)
        {
            await LoadChildNavigationItemsAsync();
        }
    }

    private void NavView_PaneClosed(NavigationView sender, object args)
    {
        // Collapse parent items when pane closes
        ZonesNavItem.IsExpanded = false;
        RoomsNavItem.IsExpanded = false;
    }

    private async Task LoadChildNavigationItemsAsync()
    {
        // Load zones as children of ZonesNavItem
        var zonesResult = await _bridgeService.GetZonesAsync();
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

        // Load rooms as children of RoomsNavItem
        var roomsResult = await _bridgeService.GetRoomsAsync();
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
            Icon = new FontIcon { Glyph = GetIconForArchetype(group.Archetype) },
            SelectsOnInvoked = true
        };
    }

    private static string GetIconForArchetype(RoomArchetype archetype)
    {
        return archetype switch
        {
            RoomArchetype.LivingRoom => "\uE7F4",
            RoomArchetype.Lounge => "\uE7F4",
            RoomArchetype.Kitchen => "\uED56",
            RoomArchetype.Dining => "\uE799",
            RoomArchetype.Bedroom => "\uEC32",
            RoomArchetype.KidsBedroom => "\uEC32",
            RoomArchetype.GuestRoom => "\uEC32",
            RoomArchetype.Bathroom => "\uE9FC",
            RoomArchetype.Toilet => "\uE9FC",
            RoomArchetype.Nursery => "\uE734",
            RoomArchetype.Recreation => "\uE7FC",
            RoomArchetype.ManCave => "\uE7FC",
            RoomArchetype.Office => "\uE821",
            RoomArchetype.Computer => "\uE7F8",
            RoomArchetype.Studio => "\uE722",
            RoomArchetype.Gym => "\uE805",
            RoomArchetype.Hallway => "\uE8B0",
            RoomArchetype.Staircase => "\uE74A",
            RoomArchetype.FrontDoor => "\uE7AD",
            RoomArchetype.Garage => "\uE804",
            RoomArchetype.Carport => "\uE804",
            RoomArchetype.Driveway => "\uE804",
            RoomArchetype.Terrace => "\uE8B3",
            RoomArchetype.Garden => "\uE8E2",
            RoomArchetype.Balcony => "\uE8B3",
            RoomArchetype.Porch => "\uE8B3",
            RoomArchetype.Pool => "\uE8A2",
            RoomArchetype.Barbecue => "\uE8F9",
            RoomArchetype.Home => "\uE80F",
            RoomArchetype.Downstairs => "\uE74B",
            RoomArchetype.Upstairs => "\uE74A",
            RoomArchetype.TopFloor => "\uE74A",
            RoomArchetype.Attic => "\uE74A",
            RoomArchetype.Music => "\uE8D6",
            RoomArchetype.TV => "\uE7F4",
            RoomArchetype.Reading => "\uE736",
            RoomArchetype.Closet => "\uE8AF",
            RoomArchetype.Storage => "\uE8AF",
            RoomArchetype.LaundryRoom => "\uE8AF",
            _ => "\uE781"
        };
    }
}
