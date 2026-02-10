using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using HueCompanion.Core.Models;
using HueCompanion.Core.Services.Interfaces;
using HueCompanion.Core.ViewModels;
using HueCompanion.Helpers;
using Microsoft.UI.Xaml.Navigation;

namespace HueCompanion.Views;

/// <summary>
/// Custom dashboard page showing pinned items.
/// </summary>
public sealed partial class MyDashboardPage : Page
{
    public CustomDashboardViewModel ViewModel { get; }

    public MyDashboardPage()
    {
        ViewModel = App.Services.GetRequiredService<CustomDashboardViewModel>();
        ViewModel.CardSelected += OnCardSelected;

        this.InitializeComponent();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel.CardSelected -= OnCardSelected;
        (ViewModel as IDisposable)?.Dispose();
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadPinnedItemsAsync();
    }

    private void OnCardSelected(object? sender, (Guid Id, PinnedItemType Type) args)
    {
        var navigationService = App.Services.GetRequiredService<INavigationService>();

        switch (args.Type)
        {
            case PinnedItemType.Room:
                navigationService.NavigateTo<RoomDetailPage>(
                    new NavigationTag(LightGroupType.Room, args.Id));
                break;
            case PinnedItemType.Zone:
                navigationService.NavigateTo<RoomDetailPage>(
                    new NavigationTag(LightGroupType.Zone, args.Id));
                break;
            case PinnedItemType.Light:
                navigationService.NavigateTo<LightDetailPage>(args.Id);
                break;
        }
    }

    private async void PinnedItemsListView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
    {
        // Persist the new order after drag-and-drop
        await ViewModel.OnItemsReorderedAsync();
    }

    private void GoToDashboard_Click(object sender, RoutedEventArgs e)
    {
        var navigationService = App.Services.GetRequiredService<INavigationService>();
        navigationService.NavigateTo<DashboardPage>();
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
}
