using HueCompanion.Core.Models;

namespace HueCompanion.Core.Services.Interfaces;

/// <summary>
/// Service for managing items pinned to the custom dashboard.
/// </summary>
public interface IPinnedItemsService
{
    /// <summary>
    /// Gets the current pinned items in display order.
    /// </summary>
    IReadOnlyList<PinnedItem> PinnedItems { get; }

    /// <summary>
    /// Returns true if there are any pinned items.
    /// </summary>
    bool HasPinnedItems { get; }

    /// <summary>
    /// Checks if an item is currently pinned.
    /// </summary>
    bool IsPinned(Guid id, PinnedItemType type);

    /// <summary>
    /// Pins an item to the dashboard.
    /// </summary>
    Task PinAsync(Guid id, PinnedItemType type);

    /// <summary>
    /// Unpins an item from the dashboard.
    /// </summary>
    Task UnpinAsync(Guid id, PinnedItemType type);

    /// <summary>
    /// Reorders pinned items after drag-drop.
    /// </summary>
    Task ReorderAsync(IList<PinnedItem> newOrder);

    /// <summary>
    /// Event raised when pinned items change (added, removed, or reordered).
    /// </summary>
    event EventHandler? PinnedItemsChanged;
}
