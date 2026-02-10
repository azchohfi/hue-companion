using FluentAssertions;
using HueCompanion.Core.Services;
using Xunit;

namespace HueCompanion.Tests.Services;

public class RoomSceneAssignmentServiceTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _testFilePath;
    private readonly RoomSceneAssignmentService _service;

    public RoomSceneAssignmentServiceTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"HueCompanionTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
        _testFilePath = Path.Combine(_testDirectory, "test-assignments.json");
        _service = new RoomSceneAssignmentService(_testFilePath);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }
        catch { }
    }

    [Fact]
    public async Task AssignAndRetrieve_RoundTrips()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var sceneId = "scene_123";

        // Act
        await _service.AssignSceneToRoomAsync(roomId, sceneId);
        var scenes = await _service.GetAssignedScenesAsync(roomId);

        // Assert
        scenes.Should().ContainSingle().Which.Should().Be(sceneId);
    }

    [Fact]
    public async Task GetAssignedScenes_EmptyRoom_ReturnsEmpty()
    {
        var scenes = await _service.GetAssignedScenesAsync(Guid.NewGuid());
        scenes.Should().BeEmpty();
    }

    [Fact]
    public async Task AssignScene_DuplicateIgnored()
    {
        var roomId = Guid.NewGuid();
        var sceneId = "scene_abc";

        await _service.AssignSceneToRoomAsync(roomId, sceneId);
        await _service.AssignSceneToRoomAsync(roomId, sceneId); // duplicate

        var scenes = await _service.GetAssignedScenesAsync(roomId);
        scenes.Should().ContainSingle();
    }

    [Fact]
    public async Task RemoveScene_RemovesCorrectly()
    {
        var roomId = Guid.NewGuid();
        await _service.AssignSceneToRoomAsync(roomId, "scene_1");
        await _service.AssignSceneToRoomAsync(roomId, "scene_2");

        await _service.RemoveSceneFromRoomAsync(roomId, "scene_1");

        var scenes = await _service.GetAssignedScenesAsync(roomId);
        scenes.Should().ContainSingle().Which.Should().Be("scene_2");
    }

    [Fact]
    public async Task PersistsAcrossInstances()
    {
        var roomId = Guid.NewGuid();
        await _service.AssignSceneToRoomAsync(roomId, "persistent_scene");

        // Create a new instance pointing at the same file
        var service2 = new RoomSceneAssignmentService(_testFilePath);
        var scenes = await service2.GetAssignedScenesAsync(roomId);

        scenes.Should().ContainSingle().Which.Should().Be("persistent_scene");
    }

    [Fact]
    public async Task ConcurrentAccess_DoesNotThrow()
    {
        var roomId = Guid.NewGuid();

        // Run many concurrent assign/read operations
        var tasks = Enumerable.Range(0, 20).Select(async i =>
        {
            await _service.AssignSceneToRoomAsync(roomId, $"scene_{i}");
            await _service.GetAssignedScenesAsync(roomId);
        });

        var act = () => Task.WhenAll(tasks);
        await act.Should().NotThrowAsync();

        var scenes = await _service.GetAssignedScenesAsync(roomId);
        scenes.Should().HaveCount(20);
    }
}
