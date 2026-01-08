using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using HueWindows.Core.Models;
using HueWindows.Core.Services.Interfaces;
using HueWindows.Core.ViewModels;
using HueWindows.Helpers;
using PinnedItemType = HueWindows.Core.Models.PinnedItemType;

namespace HueWindows.Views;

/// <summary>
/// Page showing room/zone details with lights and scenes.
/// </summary>
public sealed partial class RoomDetailPage : Page
{
    public RoomDetailViewModel ViewModel { get; }
    private readonly IPinnedItemsService _pinnedItemsService;

    public RoomDetailPage()
    {
        ViewModel = App.Services.GetRequiredService<RoomDetailViewModel>();
        _pinnedItemsService = App.Services.GetRequiredService<IPinnedItemsService>();
        ViewModel.LightSelected += OnLightSelected;

        this.InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        // Support both simple Guid (room) and NavigationTag (room or zone)
        if (e.Parameter is NavigationTag navTag)
        {
            await ViewModel.LoadRoomAsync(navTag.Id, navTag.Type);
        }
        else if (e.Parameter is Guid roomId)
        {
            await ViewModel.LoadRoomAsync(roomId, LightGroupType.Room);
        }
    }

    private void OnLightSelected(object? sender, Guid lightId)
    {
        var navigationService = App.Services.GetRequiredService<INavigationService>();
        navigationService.NavigateTo<LightDetailPage>(lightId);
    }

    private void BrightnessSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        // Only fire command if value actually changed from user interaction
        if (Math.Abs(e.NewValue - e.OldValue) > 0.5)
        {
            ViewModel.SetBrightnessCommand.Execute(e.NewValue / 100.0);
        }
    }

    private void LightCard_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is Guid lightId)
        {
            var navigationService = App.Services.GetRequiredService<INavigationService>();
            navigationService.NavigateTo<LightDetailPage>(lightId);
        }
    }

    /// <summary>
    /// Helper to check if scenes exist.
    /// </summary>
    public Visibility HasScenes(int count)
    {
        return count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Helper to check if error message exists.
    /// </summary>
    public bool HasErrorMessage(string? message)
    {
        return !string.IsNullOrEmpty(message);
    }

    /// <summary>
    /// Handles adding a light to the custom dashboard.
    /// </summary>
    private async void AddLightToDashboard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem menuItem && menuItem.Tag is Guid lightId)
        {
            await _pinnedItemsService.PinAsync(lightId, PinnedItemType.Light);
        }
    }
}
