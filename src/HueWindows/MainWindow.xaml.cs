using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using HueWindows.Core.Services.Interfaces;
using HueWindows.Views;
using WinRT.Interop;

namespace HueWindows;

/// <summary>
/// The main application window with navigation shell.
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly INavigationService _navigationService;
    private readonly ISettingsService _settingsService;

    public MainWindow()
    {
        this.InitializeComponent();

        // Set up Mica backdrop
        TrySetMicaBackdrop();

        // Set window size
        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new Windows.Graphics.SizeInt32(1200, 800));

        // Get services
        _navigationService = App.Services.GetRequiredService<INavigationService>();
        _settingsService = App.Services.GetRequiredService<ISettingsService>();

        // Initialize navigation
        _navigationService.Initialize(ContentFrame);

        // Navigate based on whether bridge is configured
        NavigateToInitialPage();
    }

    private async void NavigateToInitialPage()
    {
        var hasBridge = await _settingsService.HasConfiguredBridgeAsync();
        if (hasBridge)
        {
            // Auto-connect to the saved bridge
            var bridgeService = App.Services.GetRequiredService<IHueBridgeService>();
            var bridge = _settingsService.Settings.ConfiguredBridge!;
            var connected = await bridgeService.ConnectAsync(bridge.IpAddress!, bridge.AppKey!);

            if (connected)
            {
                _navigationService.NavigateTo<DashboardPage>();
                NavView.SelectedItem = NavView.MenuItems[0];
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

    private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.IsSettingsInvoked)
        {
            _navigationService.NavigateTo<SettingsPage>();
        }
        else if (args.InvokedItemContainer is NavigationViewItem item)
        {
            var tag = item.Tag?.ToString();
            switch (tag)
            {
                case "Dashboard":
                    _navigationService.NavigateTo<DashboardPage>();
                    break;
            }
        }
    }

    private void NavView_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
    {
        if (_navigationService.CanGoBack)
        {
            _navigationService.GoBack();
        }
    }
}
