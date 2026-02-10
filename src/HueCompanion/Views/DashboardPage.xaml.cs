using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using HueCompanion.Constants;
using HueCompanion.Core.Models;
using HueCompanion.Core.Services.Interfaces;
using HueCompanion.Core.Utilities;
using HueCompanion.Core.ViewModels;
using HueCompanion.Helpers;
using HueCompanion.Utilities;
using Microsoft.UI.Xaml.Navigation;

namespace HueCompanion.Views;

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

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel.RoomSelected -= OnRoomSelected;
        (ViewModel as IDisposable)?.Dispose();
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
    /// Shows a dialog to create a new room or zone.
    /// </summary>
    private async void AddRoomOrZone_Click(object sender, RoutedEventArgs e)
    {
        var nameBox = new TextBox
        {
            PlaceholderText = "Name",
            Margin = new Thickness(0, 0, 0, 12)
        };

        var typeRadio = new RadioButtons
        {
            Header = "Type",
            Margin = new Thickness(0, 0, 0, 12)
        };
        typeRadio.Items.Add("Room");
        typeRadio.Items.Add("Zone");
        typeRadio.SelectedIndex = 0;

        var archetypes = Core.Utilities.RoomIconHelper.GetAllArchetypes();
        var archetypeGrid = new GridView
        {
            SelectionMode = ListViewSelectionMode.Single,
            MaxHeight = 280,
            ItemsSource = archetypes,
            Header = "Archetype"
        };
        archetypeGrid.ItemTemplate = (DataTemplate)Microsoft.UI.Xaml.Markup.XamlReader.Load(
            @"<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>
                <StackPanel Width='72' Padding='6' Spacing='2' HorizontalAlignment='Center'>
                    <FontIcon Glyph='{Binding IconGlyph}' FontSize='18' HorizontalAlignment='Center'/>
                    <TextBlock Text='{Binding DisplayName}' FontSize='10' TextAlignment='Center'
                               TextTrimming='CharacterEllipsis' HorizontalAlignment='Center'/>
                </StackPanel>
            </DataTemplate>");
        archetypeGrid.SelectedIndex = 0;

        var panel = new StackPanel();
        panel.Children.Add(nameBox);
        panel.Children.Add(typeRadio);
        panel.Children.Add(archetypeGrid);

        var dialog = new ContentDialog
        {
            Title = "Create Room or Zone",
            Content = panel,
            PrimaryButtonText = "Create",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(nameBox.Text))
        {
            var type = typeRadio.SelectedIndex == 0 ? Core.Models.LightGroupType.Room : Core.Models.LightGroupType.Zone;
            var selectedArchetype = archetypeGrid.SelectedItem is ArchetypeItem item
                ? item.Archetype
                : Core.Models.RoomArchetype.Other;

            await ViewModel.CreateRoomOrZoneAsync(nameBox.Text.Trim(), type, selectedArchetype);
        }
    }

    /// <summary>
    /// Handles staggered entrance animation for cards.
    /// </summary>
    private void CardsRepeater_ElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
    {
        var element = args.Element;
        var index = _entranceAnimationIndex++;

        AnimationHelper.AnimateEntrance(element, delayMs: index * AppConstants.Animation.EntranceStaggerDelayMs);
    }
}
