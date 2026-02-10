using HueCompanion.Core.Models;
using HueCompanion.Core.Services.Interfaces;

namespace HueCompanion.Core.Services;

/// <summary>
/// Service for managing items pinned to the custom dashboard.
/// </summary>
public class PinnedItemsService : IPinnedItemsService
{
    private readonly ISettingsService _settingsService;

    public PinnedItemsService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    /// <inheritdoc/>
    public IReadOnlyList<PinnedItem> PinnedItems =>
        _settingsService.Settings.PinnedDashboardItems
            .OrderBy(p => p.Order)
            .ToList()
            .AsReadOnly();

    /// <inheritdoc/>
    public bool HasPinnedItems =>
        _settingsService.Settings.PinnedDashboardItems.Count > 0;

    /// <inheritdoc/>
    public bool IsPinned(Guid id, PinnedItemType type) =>
        _settingsService.Settings.PinnedDashboardItems
            .Any(p => p.Id == id && p.Type == type);

    /// <inheritdoc/>
    public async Task PinAsync(Guid id, PinnedItemType type)
    {
        if (IsPinned(id, type))
            return;

        var items = _settingsService.Settings.PinnedDashboardItems;
        var maxOrder = items.Count > 0 ? items.Max(p => p.Order) : -1;

        items.Add(new PinnedItem
        {
            Id = id,
            Type = type,
            Order = maxOrder + 1
        });

        await _settingsService.SaveAsync();
        PinnedItemsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc/>
    public async Task UnpinAsync(Guid id, PinnedItemType type)
    {
        var items = _settingsService.Settings.PinnedDashboardItems;
        var item = items.FirstOrDefault(p => p.Id == id && p.Type == type);

        if (item == null)
            return;

        items.Remove(item);

        // Reindex remaining items to maintain contiguous order
        var ordered = items.OrderBy(p => p.Order).ToList();
        for (int i = 0; i < ordered.Count; i++)
        {
            ordered[i].Order = i;
        }

        await _settingsService.SaveAsync();
        PinnedItemsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc/>
    public async Task ReorderAsync(IList<PinnedItem> newOrder)
    {
        var items = _settingsService.Settings.PinnedDashboardItems;

        // Update order based on new positions
        for (int i = 0; i < newOrder.Count; i++)
        {
            var item = items.FirstOrDefault(p => p.Id == newOrder[i].Id && p.Type == newOrder[i].Type);
            if (item != null)
            {
                item.Order = i;
            }
        }

        await _settingsService.SaveAsync();
        PinnedItemsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc/>
    public event EventHandler? PinnedItemsChanged;
}
