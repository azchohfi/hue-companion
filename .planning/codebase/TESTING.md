# Testing Patterns

**Analysis Date:** 2026-01-20

## Test Framework

**Runner:**
- xUnit 2.9.2
- Config: `src/HueCompanion.Tests/HueCompanion.Tests.csproj`

**Assertion Library:**
- FluentAssertions 6.12.2

**Run Commands:**
```bash
dotnet test src/HueCompanion.Tests/HueCompanion.Tests.csproj           # Run all tests
dotnet test src/HueCompanion.Tests/HueCompanion.Tests.csproj --watch   # Watch mode
dotnet test src/HueCompanion.Tests/HueCompanion.Tests.csproj /p:CollectCoverage=true  # Coverage
```

## Test File Organization

**Location:**
- Co-located in separate `HueCompanion.Tests` project following source structure
- Tests mirror source namespace structure: `HueCompanion.Tests.Models`, `HueCompanion.Tests.Services`

**Naming:**
- Test classes: `[ClassName]Tests` (e.g., `HueColorTests`, `ResultTests`, `SceneStorageServiceTests`)
- Test methods: `[MethodName]_[Scenario]_[ExpectedResult]` pattern observed
  - Example: `Constructor_ClampsXToValidRange`, `SaveSceneAsync_CreatesJsonFile`, `FromRgb_RoundTripsApproximately`

**Structure:**
```
src/HueCompanion.Tests/
├── Models/
│   ├── HueColorTests.cs
│   └── ResultTests.cs
└── Services/
    ├── SceneStorageServiceTests.cs
    └── SettingsServiceTests.cs
```

## Test Structure

**Suite Organization:**
```csharp
// From src/HueCompanion.Tests/Models/ResultTests.cs
namespace HueCompanion.Tests.Models;

public class ResultTests
{
    public class GenericResult
    {
        [Fact]
        public void Success_CreatesSuccessfulResult()
        {
            // Arrange & Act
            var result = Result<int>.Success(42);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Be(42);
        }
    }

    public class NonGenericResult
    {
        [Fact]
        public void Success_CreatesSuccessfulResult()
        {
            // Arrange & Act
            var result = Result.Success();

            // Assert
            result.IsSuccess.Should().BeTrue();
        }
    }
}
```

**Patterns:**
- Setup: Constructor (`public TestClassName()`) initializes test data and services
- Teardown: `IDisposable` with `Dispose()` cleans up resources (temp directories, files)
- Assertions: FluentAssertions method chaining (`Should().Be()`, `Should().BeTrue()`, `Should().HaveCount()`)

**Arrange-Act-Assert (AAA):**
```csharp
// From src/HueCompanion.Tests/Models/HueColorTests.cs:33-42
[Fact]
public void White_ReturnsD65WhitePoint()
{
    // Arrange & Act
    var white = HueColor.White;

    // Assert
    white.X.Should().BeApproximately(0.3127, 0.0001);
    white.Y.Should().BeApproximately(0.3290, 0.0001);
}
```

## Mocking

**Framework:** Moq 4.20.72

**Patterns:**
- Create testable versions of services by subclassing and overriding protected/public methods
- Example from `src/HueCompanion.Tests/Services/SceneStorageServiceTests.cs:395-404`:
```csharp
private class SceneStorageServiceTestable : SceneStorageService
{
    private readonly string _testUserScenesPath;

    public SceneStorageServiceTestable(string testDirectory)
    {
        _testUserScenesPath = testDirectory;
    }

    public new string UserScenesPath => _testUserScenesPath;

    // Override methods to use test directory instead
    public new async Task<Result> SaveSceneAsync(AnimatedSceneModel scene)
    {
        // Test implementation
    }
}
```

**What to Mock:**
- File I/O operations (use temp directories with `Path.GetTempPath()`)
- External API calls (bridge services)
- Long-running operations in unit tests

**What NOT to Mock:**
- Core business logic (Result type, HueColor conversions)
- Data transformation logic
- Validation logic
- Model properties

## Fixtures and Factories

**Test Data:**
Factory methods create test data with defaults:
```csharp
// From src/HueCompanion.Tests/Services/SceneStorageServiceTests.cs:350-361
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
            new AnimationKeyframe { TimeSeconds = 0, Brightness = 1.0, Color = HueColor.White },
            new AnimationKeyframe { TimeSeconds = 10, Brightness = 0.5, Color = new HueColor(0.5, 0.4) }
        }
    };
}
```

**Location:**
- Helper methods as private static in test class (`CreateValidScene`, `CreateValidKeyframeAnimation`)
- Temp directories created per-test using unique GUID: `Path.Combine(Path.GetTempPath(), $"HueCompanionSceneTest_{Guid.NewGuid()}")`

## Coverage

**Requirements:** Not enforced via CI/CD

**View Coverage:**
```bash
dotnet test src/HueCompanion.Tests/HueCompanion.Tests.csproj /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

## Test Types

**Unit Tests:**
- Scope: Single method or small logical unit
- Approach: No external dependencies, all inputs mocked
- Location: Models (HueColorTests, ResultTests) and isolated service methods
- Example: `HueColorTests.FromRgb_RoundTripsApproximately` tests color space conversion without bridge

**Integration Tests:**
- Scope: Multiple components working together
- Approach: Use testable subclasses to swap real I/O for test directories
- Location: `SceneStorageServiceTests`, `SettingsServiceTests`
- Example: `SaveSceneAsync_ThenLoadSceneAsync_RoundTripsData` writes to temp disk, reads back

**E2E Tests:**
- Framework: Not used currently
- Manual testing used for full application flow (bridge connection, scene playback)

## Common Patterns

**Async Testing:**
```csharp
// From src/HueCompanion.Tests/Services/SceneStorageServiceTests.cs:30-43
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
```

**Error Testing:**
```csharp
// From src/HueCompanion.Tests/Services/SceneStorageServiceTests.cs:80-91
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
```

**Theory Testing (Parameterized):**
```csharp
// From src/HueCompanion.Tests/Models/HueColorTests.cs:55-81
[Theory]
[InlineData(255, 0, 0)]   // Red
[InlineData(0, 255, 0)]   // Green
[InlineData(0, 0, 255)]   // Blue
[InlineData(255, 255, 255)] // White
[InlineData(0, 0, 0)]     // Black -> should return white point
public void FromRgb_RoundTripsApproximately(byte r, byte g, byte b)
{
    // Test implementation
}
```

**Approximate/Tolerance Assertions:**
```csharp
// From src/HueCompanion.Tests/Models/HueColorTests.cs:40-41
white.X.Should().BeApproximately(0.3127, 0.0001);
white.Y.Should().BeApproximately(0.3290, 0.0001);

// From src/HueCompanion.Tests/Models/HueColorTests.cs:77-79
resultR.Should().BeCloseTo(r, 30); // Allow 30 unit tolerance for conversion
resultG.Should().BeCloseTo(g, 30);
resultB.Should().BeCloseTo(b, 30);
```

**Test Cleanup:**
```csharp
// From src/HueCompanion.Tests/Services/SceneStorageServiceTests.cs:8-28
public class SceneStorageServiceTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly SceneStorageServiceTestable _service;

    public SceneStorageServiceTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"HueCompanionSceneTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
        _service = new SceneStorageServiceTestable(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }
}
```

## Package Configuration

Dependencies in `src/HueCompanion.Tests/HueCompanion.Tests.csproj`:
- `Microsoft.NET.Test.Sdk` 17.12.0 - Test runner infrastructure
- `xunit` 2.9.2 - Test framework
- `xunit.runner.visualstudio` 3.0.0 - Visual Studio integration
- `Moq` 4.20.72 - Mocking library
- `FluentAssertions` 6.12.2 - Assertion library
- `coverlet.collector` 6.0.4 - Code coverage collection

---

*Testing analysis: 2026-01-20*
