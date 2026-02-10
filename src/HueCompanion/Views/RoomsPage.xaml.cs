using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using HueCompanion.Core.Models;
using HueCompanion.Core.Services.Interfaces;
using HueCompanion.Core.ViewModels;
using HueCompanion.Helpers;

namespace HueCompanion.Views;

/// <summary>
/// Page showing all rooms.
/// </summary>
public sealed partial class RoomsPage : Page
{
    public DashboardViewModel ViewModel { get; }

    public RoomsPage()
    {
        ViewModel = App.Services.GetRequiredService<DashboardViewModel>();
        ViewModel.RoomSelected += OnRoomSelected;

        this.InitializeComponent();
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadRoomsAsync();
        UpdateSubtitle();
    }

    private void UpdateSubtitle()
    {
        var count = ViewModel.RoomCards.Count;
        SubtitleText.Text = count == 1 ? "1 room" : $"{count} rooms";
    }

    private void OnRoomSelected(object? sender, (Guid Id, LightGroupType Type, bool IsOn, List<(byte R, byte G, byte B)> Colors) args)
    {
        var navigationService = App.Services.GetRequiredService<INavigationService>();
        var navParams = new RoomNavigationParams(args.Type, args.Id, args.IsOn, args.Colors);
        navigationService.NavigateTo<RoomDetailPage>(navParams);
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
