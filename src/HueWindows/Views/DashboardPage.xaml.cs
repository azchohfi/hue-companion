using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using HueWindows.Constants;
using HueWindows.Core.Models;
using HueWindows.Core.Services.Interfaces;
using HueWindows.Core.ViewModels;
using HueWindows.Helpers;
using HueWindows.Utilities;

namespace HueWindows.Views;

/// <summary>
/// Dashboard page showing room cards.
/// </summary>
public sealed partial class DashboardPage : Page
{
    public DashboardViewModel ViewModel { get; }
    private readonly IPinnedItemsService _pinnedItemsService;
    private int _entranceAnimationIndex; // Track stagger index across repeaters

    public DashboardPage()
    {
        ViewModel = App.Services.GetRequiredService<DashboardViewModel>();
        _pinnedItemsService = App.Services.GetRequiredService<IPinnedItemsService>();
        ViewModel.RoomSelected += OnRoomSelected;

        this.InitializeComponent();
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        _entranceAnimationIndex = 0; // Reset stagger index on page load
        await ViewModel.LoadRoomsAsync();
    }

    private void OnRoomSelected(object? sender, (Guid Id, LightGroupType Type, bool IsOn, List<(byte R, byte G, byte B)> Colors) args)
    {
        var navigationService = App.Services.GetRequiredService<INavigationService>();
        var navParams = new RoomNavigationParams(args.Type, args.Id, args.IsOn, args.Colors);
        navigationService.NavigateTo<RoomDetailPage>(navParams);
    }

    /// <summary>
    /// Helper method to invert a boolean for visibility binding.
    /// </summary>
    public Visibility InvertBool(bool value)
    {
        return value ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>
    /// Helper method to check if error message exists.
    /// </summary>
    public bool HasErrorMessage(string? message)
    {
        return !string.IsNullOrEmpty(message);
    }

    /// <summary>
    /// Opens the Philips Hue app from Microsoft Store.
    /// </summary>
    private async void OpenHueApp_Click(object sender, RoutedEventArgs e)
    {
        // Try to launch Hue app directly, fall back to Store page
        var launched = await Windows.System.Launcher.LaunchUriAsync(
            new Uri("philipshue://"));

        if (!launched)
        {
            // Fall back to Microsoft Store page for Hue app
            await Windows.System.Launcher.LaunchUriAsync(
                new Uri("ms-windows-store://pdp/?productid=9WZDNCRFJB54"));
        }
    }

    /// <summary>
    /// Handles adding a room or zone to the custom dashboard.
    /// </summary>
    private async void AddToDashboard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem menuItem && menuItem.Tag is RoomCardViewModel roomCard)
        {
            await _pinnedItemsService.PinAsync(roomCard.ItemId, roomCard.ItemType);
        }
    }

    /// <summary>
    /// Handles staggered entrance animation for cards.
    /// </summary>
    private async void CardsRepeater_ElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
    {
        var element = args.Element;
        var index = _entranceAnimationIndex++;

        // Start invisible
        element.Opacity = 0;

        // Staggered delay based on index
        var delay = TimeSpan.FromMilliseconds(index * AppConstants.Animation.EntranceStaggerDelayMs);

        // Use dispatcher to ensure we're on UI thread after delay
        DispatcherQueue.TryEnqueue(async () =>
        {
            await Task.Delay(delay);
            AnimationHelper.AnimateOpacity(element, 1.0);
        });
    }
}
