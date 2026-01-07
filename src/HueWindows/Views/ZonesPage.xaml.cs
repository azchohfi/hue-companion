using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using HueWindows.Core.Services.Interfaces;
using HueWindows.Core.ViewModels;
using HueWindows.Helpers;

namespace HueWindows.Views;

/// <summary>
/// Page showing all zones.
/// </summary>
public sealed partial class ZonesPage : Page
{
    public DashboardViewModel ViewModel { get; }

    public ZonesPage()
    {
        ViewModel = App.Services.GetRequiredService<DashboardViewModel>();
        ViewModel.RoomSelected += OnZoneSelected;

        this.InitializeComponent();
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadRoomsAsync();
        UpdateSubtitle();
    }

    private void UpdateSubtitle()
    {
        var count = ViewModel.ZoneCards.Count;
        SubtitleText.Text = count == 1 ? "1 zone" : $"{count} zones";
    }

    private void OnZoneSelected(object? sender, Guid zoneId)
    {
        var navigationService = App.Services.GetRequiredService<INavigationService>();
        var navTag = new NavigationTag(Core.Models.LightGroupType.Zone, zoneId);
        navigationService.NavigateTo<RoomDetailPage>(navTag);
    }

    public Visibility InvertBool(bool value)
    {
        return value ? Visibility.Collapsed : Visibility.Visible;
    }

    public Visibility ShowEmptyState(bool isLoading, bool hasItems)
    {
        return !isLoading && !hasItems ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void OpenHueApp_Click(object sender, RoutedEventArgs e)
    {
        var launched = await Windows.System.Launcher.LaunchUriAsync(
            new Uri("philipshue://"));

        if (!launched)
        {
            await Windows.System.Launcher.LaunchUriAsync(
                new Uri("ms-windows-store://pdp/?productid=9WZDNCRFJB54"));
        }
    }
}
