using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HueWindows.Core.Models;
using HueWindows.Core.Services.Interfaces;
using static HueWindows.Core.Services.Interfaces.UIDispatcher;

namespace HueWindows.Core.ViewModels;

/// <summary>
/// ViewModel for the custom dashboard page showing pinned items.
/// </summary>
public partial class CustomDashboardViewModel : ObservableObject
{
    private readonly IHueBridgeService _bridgeService;
    private readonly IPinnedItemsService _pinnedItemsService;

    [ObservableProperty]
    private ObservableCollection<DashboardCardViewModel> _pinnedCards = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _showEmptyState;

    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>
    /// Event raised when a card is selected for detail view.
    /// </summary>
    public event EventHandler<(Guid Id, PinnedItemType Type)>? CardSelected;

    public CustomDashboardViewModel(
        IHueBridgeService bridgeService,
        IPinnedItemsService pinnedItemsService)
    {
        _bridgeService = bridgeService;
        _pinnedItemsService = pinnedItemsService;

        // Subscribe to light state changes
        _bridgeService.LightStateChanged += OnLightStateChanged;

        // Subscribe to pinned items changes to auto-refresh
        _pinnedItemsService.PinnedItemsChanged += OnPinnedItemsChanged;
    }

    [RelayCommand]
    public async Task LoadPinnedItemsAsync()
    {
        if (IsLoading) return;

        IsLoading = true;
        ErrorMessage = null;
        ShowEmptyState = false;

        var pinnedItems = _pinnedItemsService.PinnedItems;

        if (pinnedItems.Count == 0)
        {
            PinnedCards.Clear();
            ShowEmptyState = true;
            IsLoading = false;
            return;
        }

        // Fetch all rooms and zones for lookup
        var roomsResult = await _bridgeService.GetRoomsAsync();
        var zonesResult = await _bridgeService.GetZonesAsync();

        if (roomsResult.IsFailure && zonesResult.IsFailure)
        {
            ErrorMessage = roomsResult.Error;
            ShowEmptyState = true;
            IsLoading = false;
            return;
        }

        var rooms = roomsResult.GetValueOrDefault(Array.Empty<RoomModel>())!;
        var zones = zonesResult.GetValueOrDefault(Array.Empty<RoomModel>())!;
        var allGroups = rooms.Concat(zones).ToDictionary(r => r.Id);

        // Fetch all lights for lookup (lights can be in any room)
        var allLights = rooms.SelectMany(r => r.Lights)
            .Concat(zones.SelectMany(z => z.Lights))
            .GroupBy(l => l.Id)
            .Select(g => g.First())
            .ToDictionary(l => l.Id);

        PinnedCards.Clear();

        foreach (var pinned in pinnedItems.OrderBy(p => p.Order))
        {
            DashboardCardViewModel? cardVm = null;

            if (pinned.Type == PinnedItemType.Room || pinned.Type == PinnedItemType.Zone)
            {
                if (allGroups.TryGetValue(pinned.Id, out var group))
                {
                    cardVm = new DashboardCardViewModel(group, _bridgeService, _pinnedItemsService);
                }
            }
            else if (pinned.Type == PinnedItemType.Light)
            {
                if (allLights.TryGetValue(pinned.Id, out var light))
                {
                    cardVm = new DashboardCardViewModel(light, _bridgeService, _pinnedItemsService);
                }
            }

            if (cardVm != null)
            {
                cardVm.CardTapped += OnCardTapped;
                PinnedCards.Add(cardVm);
            }
        }

        ShowEmptyState = PinnedCards.Count == 0;
        IsLoading = false;
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadPinnedItemsAsync();
    }

    /// <summary>
    /// Called when items are reordered via drag-and-drop.
    /// </summary>
    public async Task OnItemsReorderedAsync()
    {
        // Build new order from current collection
        var newOrder = PinnedCards.Select((card, index) => new PinnedItem
        {
            Id = card.ItemId,
            Type = card.ItemType,
            Order = index
        }).ToList();

        await _pinnedItemsService.ReorderAsync(newOrder);
    }

    private void OnCardTapped(object? sender, (Guid Id, PinnedItemType Type) args)
    {
        CardSelected?.Invoke(this, args);
    }

    private void OnLightStateChanged(object? sender, LightStateChangedEventArgs e)
    {
        // Marshal to UI thread since event comes from background thread
        UIDispatcher.RunOnUIThread(() =>
        {
            foreach (var card in PinnedCards)
            {
                card.OnLightStateChanged(e);
            }
        });
    }

    private async void OnPinnedItemsChanged(object? sender, EventArgs e)
    {
        // Reload when pinned items change
        await LoadPinnedItemsAsync();
    }
}
