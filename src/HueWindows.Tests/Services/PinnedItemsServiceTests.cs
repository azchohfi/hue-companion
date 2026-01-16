using FluentAssertions;
using HueWindows.Core.Models;
using HueWindows.Core.Services;
using Xunit;

namespace HueWindows.Tests.Services;

/// <summary>
/// Unit tests for PinnedItemsService
/// </summary>
public class PinnedItemsServiceTests
{
    [Fact]
    public void GetPinnedItems_ShouldReturnEmptyList_WhenNoItemsPinned()
    {
        // Arrange
        var service = new PinnedItemsService();

        // Act
        var items = service.GetPinnedItems();

        // Assert
        items.Should().NotBeNull();
        items.Should().BeEmpty();
    }

    [Fact]
    public void AddPinnedItem_ShouldAddItemToList()
    {
        // Arrange
        var service = new PinnedItemsService();
        var item = new PinnedItem
        {
            Id = "room-123",
            Name = "Living Room",
            Type = "Room"
        };

        // Act
        service.AddPinnedItem(item);
        var items = service.GetPinnedItems();

        // Assert
        items.Should().ContainSingle();
        items[0].Id.Should().Be("room-123");
        items[0].Name.Should().Be("Living Room");
        items[0].Type.Should().Be("Room");
    }

    [Fact]
    public void RemovePinnedItem_ShouldRemoveItemFromList()
    {
        // Arrange
        var service = new PinnedItemsService();
        var item = new PinnedItem { Id = "room-123", Name = "Living Room", Type = "Room" };
        service.AddPinnedItem(item);

        // Act
        service.RemovePinnedItem("room-123");
        var items = service.GetPinnedItems();

        // Assert
        items.Should().BeEmpty();
    }

    [Fact]
    public void IsPinned_ShouldReturnTrue_WhenItemIsPinned()
    {
        // Arrange
        var service = new PinnedItemsService();
        var item = new PinnedItem { Id = "room-123", Name = "Living Room", Type = "Room" };
        service.AddPinnedItem(item);

        // Act
        var isPinned = service.IsPinned("room-123");

        // Assert
        isPinned.Should().BeTrue();
    }

    [Fact]
    public void IsPinned_ShouldReturnFalse_WhenItemIsNotPinned()
    {
        // Arrange
        var service = new PinnedItemsService();

        // Act
        var isPinned = service.IsPinned("room-456");

        // Assert
        isPinned.Should().BeFalse();
    }
}
