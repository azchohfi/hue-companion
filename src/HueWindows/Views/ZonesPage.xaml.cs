using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using HueWindows.Core.Models;
using HueWindows.Core.Services.Interfaces;
using HueWindows.Core.ViewModels;
using HueWindows.Helpers;
using Windows.ApplicationModel.DataTransfer;

namespace HueWindows.Views;

/// <summary>
/// Page showing all zones with section layout and drag-and-drop editing.
/// </summary>
public sealed partial class ZonesPage : Page
{
    public ZonesPageViewModel ViewModel { get; }

    // Drag state
    private const string DragLightIdFormat = "ZoneLightId";
    private const string DragSourceZoneIdFormat = "SourceZoneId";

    public ZonesPage()
    {
        ViewModel = App.Services.GetRequiredService<ZonesPageViewModel>();
        ViewModel.ZoneTapped += OnZoneTapped;

        this.InitializeComponent();
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadAsync();
        UpdateSubtitle();
    }

    private void UpdateSubtitle()
    {
        var count = ViewModel.ZoneSections.Count;
        SubtitleText.Text = count == 1 ? "1 zone" : $"{count} zones";
    }

    private void OnZoneTapped(object? sender, (Guid Id, LightGroupType Type, bool IsOn, List<(byte R, byte G, byte B)> Colors) args)
    {
        var navigationService = App.Services.GetRequiredService<INavigationService>();
        var navParams = new RoomNavigationParams(args.Type, args.Id, args.IsOn, args.Colors);
        navigationService.NavigateTo<RoomDetailPage>(navParams);
    }

    // --- Static helper methods for x:Bind ---

    public static string FormatLightCount(int count)
    {
        return count == 1 ? "1 light" : $"{count} lights";
    }

    public static Windows.UI.Color GetLightDotColor(bool isOn, (byte R, byte G, byte B)? colorRgb)
    {
        if (!isOn)
            return Windows.UI.Color.FromArgb(80, 255, 255, 255);

        if (colorRgb.HasValue)
            return Windows.UI.Color.FromArgb(255, colorRgb.Value.R, colorRgb.Value.G, colorRgb.Value.B);

        return Windows.UI.Color.FromArgb(255, 255, 200, 100); // Warm white default
    }

    public static double GetLightOpacity(bool isOn) => isOn ? 1.0 : 0.5;

    public static Visibility HasRoomName(string? roomName)
    {
        return string.IsNullOrEmpty(roomName) ? Visibility.Collapsed : Visibility.Visible;
    }

    public Visibility InvertBool(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

    public Visibility ShowEmptyState(bool isLoading, bool hasItems)
    {
        return !isLoading && !hasItems ? Visibility.Visible : Visibility.Collapsed;
    }

    public bool HasError(string? errorMessage) => !string.IsNullOrEmpty(errorMessage);

    public Visibility HasUnassigned(ZoneSectionViewModel? section)
    {
        return section != null ? Visibility.Visible : Visibility.Collapsed;
    }

    public string FormatUnassignedCount(ZoneSectionViewModel? section)
    {
        if (section == null) return "";
        return FormatLightCount(section.LightCount);
    }

    // --- Zone heading tap ---

    private void ZoneHeading_Tapped(object sender, TappedRoutedEventArgs e)
    {
        // Don't navigate when tapping the toggle switch
        if (e.OriginalSource is DependencyObject source)
        {
            DependencyObject? current = source;
            while (current != null)
            {
                if (current is ToggleSwitch) return;
                current = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(current);
            }
        }

        if (sender is FrameworkElement fe && fe.Tag is ZoneSectionViewModel section)
        {
            section.TapZoneCommand.Execute(null);
        }
    }

    // --- Zone toggle ---

    private async void ZoneToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch toggle && toggle.Tag is ZoneSectionViewModel section && section.ZoneId.HasValue)
        {
            var bridgeId = section.BridgeId;
            if (bridgeId == null) return;

            var multiBridge = App.Services.GetRequiredService<IMultiBridgeService>();
            var service = multiBridge.GetBridgeService(bridgeId);
            if (service != null)
            {
                await service.SetZoneOnAsync(section.ZoneId.Value, toggle.IsOn);
            }
        }
    }

    // --- Light item interaction ---

    private void LightItem_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Grid grid)
            grid.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(15, 255, 255, 255));
    }

    private void LightItem_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Grid grid)
            grid.Background = new SolidColorBrush(Colors.Transparent);
    }

    private void LightItem_Tapped(object sender, TappedRoutedEventArgs e)
    {
        // Don't navigate in edit mode
        if (ViewModel.IsEditMode) return;

        if (sender is FrameworkElement fe && fe.Tag is ZoneLightItemViewModel light)
        {
            var navigationService = App.Services.GetRequiredService<INavigationService>();
            navigationService.NavigateTo<LightDetailPage>(light.LightId);
        }
    }

    // --- Drag and Drop ---

    private void LightItem_DragStarting(UIElement sender, DragStartingEventArgs args)
    {
        if (sender is FrameworkElement fe && fe.Tag is ZoneLightItemViewModel light)
        {
            args.Data.SetText(light.LightId.ToString());
            args.Data.Properties[DragLightIdFormat] = light.LightId;

            // Find the source zone by walking up the visual tree
            var parent = fe.Parent;
            while (parent != null)
            {
                if (parent is FrameworkElement parentFe && parentFe.Tag is ZoneSectionViewModel zone)
                {
                    args.Data.Properties[DragSourceZoneIdFormat] = zone.ZoneId;
                    break;
                }
                parent = (parent as FrameworkElement)?.Parent as DependencyObject
                         ?? Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(parent);
            }

            args.Data.RequestedOperation = DataPackageOperation.Move;
        }
    }

    private void ZoneSection_DragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Properties.ContainsKey(DragLightIdFormat))
        {
            var targetZone = (sender as FrameworkElement)?.Tag as ZoneSectionViewModel;
            var sourceZoneId = e.DataView.Properties.ContainsKey(DragSourceZoneIdFormat)
                ? e.DataView.Properties[DragSourceZoneIdFormat] as Guid?
                : null;

            // Don't allow dropping on the same zone
            if (targetZone?.ZoneId == sourceZoneId)
            {
                e.AcceptedOperation = DataPackageOperation.None;
                return;
            }

            e.AcceptedOperation = DataPackageOperation.Move;
        }
    }

    private async void ZoneSection_Drop(object sender, DragEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is ZoneSectionViewModel targetZone
            && e.DataView.Properties.ContainsKey(DragLightIdFormat))
        {
            var lightId = (Guid)e.DataView.Properties[DragLightIdFormat];
            var sourceZoneId = e.DataView.Properties.ContainsKey(DragSourceZoneIdFormat)
                ? e.DataView.Properties[DragSourceZoneIdFormat] as Guid?
                : null;

            if (targetZone.ZoneId == sourceZoneId) return;

            // Get bridge ID from target zone (or source zone for context)
            var bridgeId = targetZone.BridgeId;
            if (bridgeId == null)
            {
                // Try to find bridge from source zone
                foreach (var section in ViewModel.ZoneSections)
                {
                    if (section.ZoneId == sourceZoneId && section.BridgeId != null)
                    {
                        bridgeId = section.BridgeId;
                        break;
                    }
                }
            }
            if (bridgeId == null) return;

            // Remove from source zone
            if (sourceZoneId.HasValue)
            {
                await ViewModel.RemoveLightFromZoneAsync(bridgeId, sourceZoneId.Value, lightId);
            }

            // Add to target zone
            if (targetZone.ZoneId.HasValue)
            {
                await ViewModel.AddLightToZoneAsync(bridgeId, targetZone.ZoneId.Value, lightId);
            }

            // Reload will be triggered by RoomsOrZonesChanged event
        }
    }

    private void UnassignedSection_DragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Properties.ContainsKey(DragLightIdFormat))
        {
            var sourceZoneId = e.DataView.Properties.ContainsKey(DragSourceZoneIdFormat)
                ? e.DataView.Properties[DragSourceZoneIdFormat] as Guid?
                : null;

            // Only accept if coming from an actual zone (not from unassigned)
            if (sourceZoneId.HasValue)
                e.AcceptedOperation = DataPackageOperation.Move;
            else
                e.AcceptedOperation = DataPackageOperation.None;
        }
    }

    private async void UnassignedSection_Drop(object sender, DragEventArgs e)
    {
        if (e.DataView.Properties.ContainsKey(DragLightIdFormat))
        {
            var lightId = (Guid)e.DataView.Properties[DragLightIdFormat];
            var sourceZoneId = e.DataView.Properties.ContainsKey(DragSourceZoneIdFormat)
                ? e.DataView.Properties[DragSourceZoneIdFormat] as Guid?
                : null;

            if (!sourceZoneId.HasValue) return;

            // Find bridge from source zone
            string? bridgeId = null;
            foreach (var section in ViewModel.ZoneSections)
            {
                if (section.ZoneId == sourceZoneId && section.BridgeId != null)
                {
                    bridgeId = section.BridgeId;
                    break;
                }
            }
            if (bridgeId == null) return;

            await ViewModel.RemoveLightFromZoneAsync(bridgeId, sourceZoneId.Value, lightId);
        }
    }

    // --- Remove button ---

    private async void RemoveFromZone_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is ZoneLightItemViewModel light)
        {
            // Walk up to find the parent zone section
            var parent = fe.Parent;
            while (parent != null)
            {
                if (parent is FrameworkElement parentFe && parentFe.Tag is ZoneSectionViewModel zone
                    && zone.ZoneId.HasValue && zone.BridgeId != null)
                {
                    await ViewModel.RemoveLightFromZoneAsync(zone.BridgeId, zone.ZoneId.Value, light.LightId);
                    return;
                }
                parent = (parent as FrameworkElement)?.Parent as DependencyObject
                         ?? Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(parent);
            }
        }
    }

    // --- Other ---

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
