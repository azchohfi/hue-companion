using FluentAssertions;
using HueCompanion.Core.Models;
using HueCompanion.Mcp.Services;
using Xunit;

namespace HueCompanion.Tests.Mcp;

public class FuzzyMatcherTests
{
    private static List<RoomModel> CreateTestRooms()
    {
        return
        [
            new RoomModel
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                Name = "Living Room",
                Lights =
                [
                    new LightModel
                    {
                        Id = Guid.Parse("00000000-0000-0000-0000-000000000010"),
                        Name = "Floor Lamp"
                    },
                    new LightModel
                    {
                        Id = Guid.Parse("00000000-0000-0000-0000-000000000011"),
                        Name = "Ceiling Light"
                    }
                ]
            },
            new RoomModel
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000002"),
                Name = "Bedroom",
                Lights =
                [
                    new LightModel
                    {
                        Id = Guid.Parse("00000000-0000-0000-0000-000000000020"),
                        Name = "Bedside Lamp"
                    }
                ]
            },
            new RoomModel
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000003"),
                Name = "Kitchen",
                Lights =
                [
                    new LightModel
                    {
                        Id = Guid.Parse("00000000-0000-0000-0000-000000000030"),
                        Name = "Kitchen Strip"
                    }
                ]
            }
        ];
    }

    [Theory]
    [InlineData("Living Room")]
    [InlineData("living room")]
    [InlineData("LIVING ROOM")]
    public void FindRoom_ExactMatch_CaseInsensitive(string query)
    {
        var rooms = CreateTestRooms();

        var result = FuzzyMatcher.FindRoom(rooms, query);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Living Room");
    }

    [Theory]
    [InlineData("bed")]
    [InlineData("Bed")]
    [InlineData("bedroom")]
    public void FindRoom_PartialMatch_FindsRoom(string query)
    {
        var rooms = CreateTestRooms();

        var result = FuzzyMatcher.FindRoom(rooms, query);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Bedroom");
    }

    [Fact]
    public void FindRoom_ByGuid_FindsRoom()
    {
        var rooms = CreateTestRooms();

        var result = FuzzyMatcher.FindRoom(rooms, "00000000-0000-0000-0000-000000000003");

        result.Should().NotBeNull();
        result!.Name.Should().Be("Kitchen");
    }

    [Fact]
    public void FindRoom_NoMatch_ReturnsNull()
    {
        var rooms = CreateTestRooms();

        var result = FuzzyMatcher.FindRoom(rooms, "bathroom");

        result.Should().BeNull();
    }

    [Fact]
    public void FindRoom_WhitespaceQuery_HandledGracefully()
    {
        var rooms = CreateTestRooms();

        var result = FuzzyMatcher.FindRoom(rooms, "  Living Room  ");

        result.Should().NotBeNull();
        result!.Name.Should().Be("Living Room");
    }

    [Theory]
    [InlineData("Floor Lamp")]
    [InlineData("floor lamp")]
    [InlineData("FLOOR LAMP")]
    public void FindLight_ExactMatch_CaseInsensitive(string query)
    {
        var rooms = CreateTestRooms();

        var result = FuzzyMatcher.FindLight(rooms, query);

        result.Should().NotBeNull();
        result!.Value.Light.Name.Should().Be("Floor Lamp");
        result!.Value.Room.Name.Should().Be("Living Room");
    }

    [Theory]
    [InlineData("bedside")]
    [InlineData("Bedside")]
    public void FindLight_PartialMatch_FindsLight(string query)
    {
        var rooms = CreateTestRooms();

        var result = FuzzyMatcher.FindLight(rooms, query);

        result.Should().NotBeNull();
        result!.Value.Light.Name.Should().Be("Bedside Lamp");
    }

    [Fact]
    public void FindLight_ByGuid_FindsLight()
    {
        var rooms = CreateTestRooms();

        var result = FuzzyMatcher.FindLight(rooms, "00000000-0000-0000-0000-000000000020");

        result.Should().NotBeNull();
        result!.Value.Light.Name.Should().Be("Bedside Lamp");
    }

    [Fact]
    public void FindLight_NoMatch_ReturnsNull()
    {
        var rooms = CreateTestRooms();

        var result = FuzzyMatcher.FindLight(rooms, "nonexistent");

        result.Should().BeNull();
    }

    [Fact]
    public void FindLight_ReturnsContainingRoom()
    {
        var rooms = CreateTestRooms();

        var result = FuzzyMatcher.FindLight(rooms, "Kitchen Strip");

        result.Should().NotBeNull();
        result!.Value.Room.Name.Should().Be("Kitchen");
    }

    [Fact]
    public void FindRoom_EmptyRoomList_ReturnsNull()
    {
        var result = FuzzyMatcher.FindRoom([], "anything");

        result.Should().BeNull();
    }

    [Fact]
    public void FindLight_EmptyRoomList_ReturnsNull()
    {
        var result = FuzzyMatcher.FindLight([], "anything");

        result.Should().BeNull();
    }
}
