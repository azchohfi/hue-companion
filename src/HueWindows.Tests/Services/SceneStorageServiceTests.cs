using FluentAssertions;
using HueWindows.Core.Models;
using HueWindows.Core.Services;
using Xunit;

namespace HueWindows.Tests.Services;

public class SceneStorageServiceTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly SceneStorageServiceTestable _service;

    public SceneStorageServiceTests()
    {
        // Use a unique temp directory for each test
        _testDirectory = Path.Combine(Path.GetTempPath(), $"HueWindowsSceneTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
        _service = new SceneStorageServiceTestable(_testDirectory);
    }

    public void Dispose()
    {
        // Clean up test directory
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task SaveSceneAsync_CreatesJsonFile()
    {
        // Arrange
        var scene = CreateValidScene("test-scene-1", "Test Scene");

        // Act
        var result = await _service.SaveSceneAsync(scene);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var expectedPath = Path.Combine(_testDirectory, "test-scene-1.json");
        File.Exists(expectedPath).Should().BeTrue();
    }

    [Fact]
    public async Task SaveSceneAsync_ThenLoadSceneAsync_RoundTripsData()
    {
        // Arrange
        var scene = CreateValidScene("round-trip-scene", "Round Trip Test");
        scene.Description = "A test description";
        scene.Category = "Test Category";
        scene.Version = "2.0";
        scene.Author = "Test Author";
        scene.DefaultTargeting = LightTargeting.Room;
        scene.TargetId = Guid.NewGuid();

        // Act - Save
        var saveResult = await _service.SaveSceneAsync(scene);
        saveResult.IsSuccess.Should().BeTrue();

        // Act - Load
        var filePath = Path.Combine(_testDirectory, "round-trip-scene.json");
        var loadResult = await _service.LoadSceneAsync(filePath);

        // Assert
        loadResult.IsSuccess.Should().BeTrue();
        loadResult.Value.Should().NotBeNull();
        loadResult.Value!.Id.Should().Be("round-trip-scene");
        loadResult.Value.Name.Should().Be("Round Trip Test");
        loadResult.Value.Description.Should().Be("A test description");
        loadResult.Value.Category.Should().Be("Test Category");
        loadResult.Value.Version.Should().Be("2.0");
        loadResult.Value.Author.Should().Be("Test Author");
        loadResult.Value.DefaultTargeting.Should().Be(LightTargeting.Room);
        loadResult.Value.TargetId.Should().Be(scene.TargetId);
        loadResult.Value.Animations.Should().HaveCount(1);
    }

    [Fact]
    public async Task LoadSceneAsync_WithNonExistentFile_ReturnsFailure()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_testDirectory, "non-existent-scene.json");

        // Act
        var result = await _service.LoadSceneAsync(nonExistentPath);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task LoadUserScenesAsync_ReturnsAllSavedScenes()
    {
        // Arrange - Save multiple scenes
        var scene1 = CreateValidScene("scene-1", "Scene One");
        var scene2 = CreateValidScene("scene-2", "Scene Two");
        var scene3 = CreateValidScene("scene-3", "Scene Three");

        await _service.SaveSceneAsync(scene1);
        await _service.SaveSceneAsync(scene2);
        await _service.SaveSceneAsync(scene3);

        // Act
        var result = await _service.LoadUserScenesAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Should().HaveCount(3);
        result.Value!.Select(s => s.Id).Should().BeEquivalentTo(["scene-1", "scene-2", "scene-3"]);
    }

    [Fact]
    public async Task LoadUserScenesAsync_WithEmptyDirectory_ReturnsEmptyList()
    {
        // Act
        var result = await _service.LoadUserScenesAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteSceneAsync_RemovesFile()
    {
        // Arrange
        var scene = CreateValidScene("delete-me", "Delete Me Scene");
        await _service.SaveSceneAsync(scene);

        var filePath = Path.Combine(_testDirectory, "delete-me.json");
        File.Exists(filePath).Should().BeTrue();

        // Act
        var result = await _service.DeleteSceneAsync("delete-me");

        // Assert
        result.IsSuccess.Should().BeTrue();
        File.Exists(filePath).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteSceneAsync_WithNonExistentId_ReturnsFailure()
    {
        // Act
        var result = await _service.DeleteSceneAsync("non-existent-id");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public void ValidateScene_WithEmptyId_ReturnsFailure()
    {
        // Arrange
        var scene = CreateValidScene("", "Valid Name");

        // Act
        var result = _service.ValidateScene(scene);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("ID");
    }

    [Fact]
    public void ValidateScene_WithEmptyName_ReturnsFailure()
    {
        // Arrange
        var scene = CreateValidScene("valid-id", "");

        // Act
        var result = _service.ValidateScene(scene);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("name");
    }

    [Fact]
    public void ValidateScene_WithNoAnimations_ReturnsFailure()
    {
        // Arrange
        var scene = new AnimatedSceneModel
        {
            Id = "valid-id",
            Name = "Valid Name",
            Animations = new List<AnimationDefinition>() // Empty
        };

        // Act
        var result = _service.ValidateScene(scene);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("at least one animation");
    }

    [Fact]
    public async Task SaveSceneAsync_RejectsBuiltInScenes()
    {
        // Arrange
        var scene = CreateValidScene("builtin-scene", "Built-In Scene");
        scene.IsBuiltIn = true;

        // Act
        var result = await _service.SaveSceneAsync(scene);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("built-in");
    }

    [Fact]
    public void ValidateScene_WithValidScene_ReturnsSuccess()
    {
        // Arrange
        var scene = CreateValidScene("valid-scene", "Valid Scene");

        // Act
        var result = _service.ValidateScene(scene);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task SaveSceneAsync_WithInvalidScene_ReturnsFailure()
    {
        // Arrange
        var scene = new AnimatedSceneModel
        {
            Id = "invalid-scene",
            Name = "", // Invalid - empty name
            Animations = new List<AnimationDefinition>
            {
                CreateValidKeyframeAnimation("anim-1")
            }
        };

        // Act
        var result = await _service.SaveSceneAsync(scene);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("name");
    }

    [Fact]
    public async Task LoadSceneAsync_WithInvalidJson_ReturnsFailure()
    {
        // Arrange - Write invalid JSON
        var invalidPath = Path.Combine(_testDirectory, "invalid.json");
        await File.WriteAllTextAsync(invalidPath, "{ not valid json }");

        // Act
        var result = await _service.LoadSceneAsync(invalidPath);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Invalid JSON");
    }

    [Fact]
    public void ValidateScene_WithKeyframeAnimationMissingKeyframes_ReturnsFailure()
    {
        // Arrange
        var scene = new AnimatedSceneModel
        {
            Id = "test-scene",
            Name = "Test Scene",
            Animations = new List<AnimationDefinition>
            {
                new AnimationDefinition
                {
                    Id = "anim-1",
                    Type = AnimationType.Keyframe,
                    DurationSeconds = 10,
                    Keyframes = new List<AnimationKeyframe>() // Empty - invalid
                }
            }
        };

        // Act
        var result = _service.ValidateScene(scene);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("at least 2 keyframes");
    }

    [Fact]
    public void ValidateScene_WithEventAnimationMissingPattern_ReturnsFailure()
    {
        // Arrange
        var scene = new AnimatedSceneModel
        {
            Id = "test-scene",
            Name = "Test Scene",
            Animations = new List<AnimationDefinition>
            {
                new AnimationDefinition
                {
                    Id = "anim-1",
                    Type = AnimationType.Event,
                    EventPattern = null // Invalid
                }
            }
        };

        // Act
        var result = _service.ValidateScene(scene);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("EventPattern");
    }

    [Fact]
    public async Task SaveSceneAsync_OverwritesExistingScene()
    {
        // Arrange
        var scene = CreateValidScene("overwrite-test", "Original Name");
        await _service.SaveSceneAsync(scene);

        // Modify and save again
        scene.Name = "Updated Name";
        scene.Description = "Updated description";

        // Act
        var result = await _service.SaveSceneAsync(scene);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify the file was updated
        var filePath = Path.Combine(_testDirectory, "overwrite-test.json");
        var loadResult = await _service.LoadSceneAsync(filePath);
        loadResult.Value!.Name.Should().Be("Updated Name");
        loadResult.Value.Description.Should().Be("Updated description");
    }

    /// <summary>
    /// Creates a valid scene with minimal required data for testing.
    /// </summary>
    private static AnimatedSceneModel CreateValidScene(string id, string name)
    {
        return new AnimatedSceneModel
        {
            Id = id,
            Name = name,
            Animations = new List<AnimationDefinition>
            {
                CreateValidKeyframeAnimation($"{id}-anim-1")
            }
        };
    }

    /// <summary>
    /// Creates a valid keyframe animation with at least 2 keyframes.
    /// </summary>
    private static AnimationDefinition CreateValidKeyframeAnimation(string id)
    {
        return new AnimationDefinition
        {
            Id = id,
            Name = "Test Animation",
            Type = AnimationType.Keyframe,
            DurationSeconds = 10,
            Keyframes = new List<AnimationKeyframe>
            {
                new AnimationKeyframe
                {
                    TimeSeconds = 0,
                    Brightness = 1.0,
                    Color = new HueColor(0.3127, 0.3290)
                },
                new AnimationKeyframe
                {
                    TimeSeconds = 10,
                    Brightness = 0.5,
                    Color = new HueColor(0.5, 0.4)
                }
            }
        };
    }

    /// <summary>
    /// Testable version of SceneStorageService that allows custom directory.
    /// </summary>
    private class SceneStorageServiceTestable : SceneStorageService
    {
        private readonly string _testUserScenesPath;

        public SceneStorageServiceTestable(string testDirectory)
        {
            _testUserScenesPath = testDirectory;
        }

        public new string UserScenesPath => _testUserScenesPath;

        public new async Task<Result> SaveSceneAsync(AnimatedSceneModel scene)
        {
            // Validate before saving
            var validationResult = ValidateScene(scene);
            if (validationResult.IsFailure)
            {
                return validationResult;
            }

            // Don't allow overwriting built-in scenes
            if (scene.IsBuiltIn)
            {
                return Result.Failure("Cannot overwrite built-in scenes");
            }

            try
            {
                var fileName = $"{scene.Id}.json";
                var filePath = Path.Combine(_testUserScenesPath, fileName);

                var json = System.Text.Json.JsonSerializer.Serialize(scene, new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                    WriteIndented = true,
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase) },
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                });
                await File.WriteAllTextAsync(filePath, json);

                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure($"Failed to save scene: {ex.Message}");
            }
        }

        public new async Task<Result<AnimatedSceneModel>> LoadSceneAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return Result<AnimatedSceneModel>.Failure($"Scene file not found: {filePath}");
                }

                var json = await File.ReadAllTextAsync(filePath);
                var scene = System.Text.Json.JsonSerializer.Deserialize<AnimatedSceneModel>(json, new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase) }
                });

                if (scene == null)
                {
                    return Result<AnimatedSceneModel>.Failure($"Failed to deserialize scene from {filePath}");
                }

                // Validate the scene
                var validationResult = ValidateScene(scene);
                if (validationResult.IsFailure)
                {
                    return Result<AnimatedSceneModel>.Failure($"Scene validation failed: {validationResult.Error}");
                }

                return Result<AnimatedSceneModel>.Success(scene);
            }
            catch (System.Text.Json.JsonException ex)
            {
                return Result<AnimatedSceneModel>.Failure($"Invalid JSON in scene file: {ex.Message}");
            }
            catch (Exception ex)
            {
                return Result<AnimatedSceneModel>.Failure($"Failed to load scene: {ex.Message}");
            }
        }

        public new async Task<Result<IReadOnlyList<AnimatedSceneModel>>> LoadUserScenesAsync()
        {
            try
            {
                var scenes = new List<AnimatedSceneModel>();
                var files = Directory.GetFiles(_testUserScenesPath, "*.json");

                foreach (var file in files)
                {
                    var sceneResult = await LoadSceneAsync(file);
                    if (sceneResult.IsSuccess && sceneResult.Value != null)
                    {
                        sceneResult.Value.IsBuiltIn = false;
                        scenes.Add(sceneResult.Value);
                    }
                }

                return Result<IReadOnlyList<AnimatedSceneModel>>.Success(scenes);
            }
            catch (Exception ex)
            {
                return Result<IReadOnlyList<AnimatedSceneModel>>.Failure($"Failed to load user scenes: {ex.Message}");
            }
        }

        public new async Task<Result> DeleteSceneAsync(string sceneId)
        {
            try
            {
                var fileName = $"{sceneId}.json";
                var filePath = Path.Combine(_testUserScenesPath, fileName);

                if (!File.Exists(filePath))
                {
                    return Result.Failure($"Scene '{sceneId}' not found");
                }

                // Load the scene to check if it's built-in
                var sceneResult = await LoadSceneAsync(filePath);
                if (sceneResult.IsSuccess && sceneResult.Value?.IsBuiltIn == true)
                {
                    return Result.Failure("Cannot delete built-in scenes");
                }

                File.Delete(filePath);
                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure($"Failed to delete scene: {ex.Message}");
            }
        }
    }
}
