using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
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
    private readonly List<NavigationViewItem> _roomNavItems = new();
    private readonly List<NavigationViewItem> _zoneNavItems = new();

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

        // Set window size
        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new Windows.Graphics.SizeInt32(1200, 800));

        // Get services
        _navigationService = App.Services.GetRequiredService<INavigationService>();
        _settingsService = App.Services.GetRequiredService<ISettingsService>();
        _bridgeService = App.Services.GetRequiredService<IHueBridgeService>();

        // Subscribe to bridge connection events
        _bridgeService.Connected += OnBridgeConnected;
        _bridgeService.Disconnected += OnBridgeDisconnected;

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
        if (hasBridge)
        {
            // Auto-connect to the saved bridge
            var bridge = _settingsService.Settings.ConfiguredBridge!;
            var connected = await _bridgeService.ConnectAsync(bridge.IpAddress!, bridge.AppKey!);

            if (connected)
            {
                _navigationService.NavigateTo<DashboardPage>();
                NavView.SelectedItem = HomeNavItem;
            }
            else
            {
                // Connection failed, go to setup to re-pair
                _navigationService.NavigateTo<SetupPage>();
            }
        }
        else
        {
            _navigationService.NavigateTo<SetupPage>();
        }
    }

    private async void OnBridgeConnected(object? sender, EventArgs e)
    {
        // Load navigation items when bridge connects
        await LoadNavigationItemsAsync();
    }

    private void OnBridgeDisconnected(object? sender, EventArgs e)
    {
        // Clear navigation items when bridge disconnects
        DispatcherQueue.TryEnqueue(() =>
        {
            ClearDynamicNavItems();
        });
    }

    private async Task LoadNavigationItemsAsync()
    {
        // Run on UI thread
        DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                // Clear existing dynamic items
                ClearDynamicNavItems();

                // Load rooms
                var rooms = await _bridgeService.GetRoomsAsync();
                int roomsInsertIndex = NavView.MenuItems.IndexOf(RoomsHeader) + 1;
                foreach (var room in rooms)
                {
                    var navItem = CreateNavItemForGroup(room);
                    NavView.MenuItems.Insert(roomsInsertIndex++, navItem);
                    _roomNavItems.Add(navItem);
                }

                // Load zones
                var zones = await _bridgeService.GetZonesAsync();
                int zonesInsertIndex = NavView.MenuItems.IndexOf(ZonesHeader) + 1;
                foreach (var zone in zones)
                {
                    var navItem = CreateNavItemForGroup(zone);
                    NavView.MenuItems.Insert(zonesInsertIndex++, navItem);
                    _zoneNavItems.Add(navItem);
                }

                // Hide headers if no items
                RoomsHeader.Visibility = rooms.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                ZonesHeader.Visibility = zones.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Error loading nav: {ex.Message}");
            }
        });
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

    private void ClearDynamicNavItems()
    {
        foreach (var item in _roomNavItems)
            NavView.MenuItems.Remove(item);
        foreach (var item in _zoneNavItems)
            NavView.MenuItems.Remove(item);
        _roomNavItems.Clear();
        _zoneNavItems.Clear();
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
                case "Dashboard":
                    _navigationService.NavigateTo<DashboardPage>();
                    break;
                case NavigationTag navTag:
                    // Navigate to RoomDetailPage with the room/zone ID and type
                    _navigationService.NavigateTo<RoomDetailPage>(navTag);
                    break;
            }
        }
    }
}
