using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using HueWindows.Core.Services.Interfaces;
using HueWindows.Core.ViewModels;

namespace HueWindows.Views;

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

    private void OnRoomSelected(object? sender, Guid roomId)
    {
        var navigationService = App.Services.GetRequiredService<INavigationService>();
        navigationService.NavigateTo<RoomDetailPage>(roomId);
    }

    public Visibility InvertBool(bool value)
    {
        return value ? Visibility.Collapsed : Visibility.Visible;
    }

    public Visibility ShowEmptyState(bool isLoading, bool hasItems)
    {
        return !isLoading && !hasItems ? Visibility.Visible : Visibility.Collapsed;
    }
}
