# Testing Guide

This document provides comprehensive guidance for testing Hue Windows.

## Test Structure

```
src/HueWindows.Tests/
├── Services/              # Service layer tests
│   ├── SettingsServiceTests.cs
│   └── PinnedItemsServiceTests.cs
├── ViewModels/           # ViewModel tests
│   └── SettingsViewModelTests.cs
├── Models/               # Model tests
│   └── ResultTests.cs
└── Converters/           # Value converter tests
```

## Running Tests

### Command Line

```bash
# Run all tests
dotnet test src/HueWindows.Tests

# Run with detailed output
dotnet test src/HueWindows.Tests --verbosity normal

# Run specific test class
dotnet test src/HueWindows.Tests --filter "FullyQualifiedName~SettingsServiceTests"

# Run specific test method
dotnet test src/HueWindows.Tests --filter "FullyQualifiedName~SettingsServiceTests.SaveSettings_ShouldPersistSettings"
```

### Visual Studio

1. Open `HueWindows.sln` in Visual Studio
2. Open **Test Explorer** (Test → Test Explorer)
3. Click **Run All** to run all tests
4. Right-click individual tests to run or debug specific tests

## Code Coverage

### Generate Coverage Locally

```bash
# Run tests with coverage collection
dotnet test src/HueWindows.Tests --collect:"XPlat Code Coverage" --results-directory ./coverage

# Install report generator (one-time setup)
dotnet tool install -g dotnet-reportgenerator-globaltool

# Generate HTML report
reportgenerator -reports:./coverage/**/coverage.cobertura.xml -targetdir:./coverage-report -reporttypes:HtmlInline

# Open report
start ./coverage-report/index.html
```

### Coverage Goals

- **Overall Target:** 70%+ code coverage
- **Critical Paths:** 90%+ (Services, ViewModels)
- **UI Code:** Best effort (harder to test, focus on logic)

### Viewing Coverage in CI

Coverage reports are automatically uploaded to [Codecov](https://codecov.io/gh/ddrayne/hue-windows) on every PR and main branch push.

## Writing Tests

### Test Naming Convention

Tests follow the pattern: `MethodName_StateUnderTest_ExpectedBehavior`

```csharp
[Fact]
public void SaveSettings_ShouldPersistSettings()
{
    // Arrange
    var service = new SettingsService();
    var settings = new AppSettings { Theme = "Dark" };
    
    // Act
    service.SaveSettings(settings);
    
    // Assert
    var retrieved = service.GetSettings();
    retrieved.Theme.Should().Be("Dark");
}
```

### Using FluentAssertions

FluentAssertions provides expressive assertion syntax:

```csharp
// Instead of this:
Assert.Equal("Dark", settings.Theme);
Assert.NotNull(settings);
Assert.True(settings.IsValid);

// Use this:
settings.Should().NotBeNull();
settings.Theme.Should().Be("Dark");
settings.IsValid.Should().BeTrue();

// Collections:
items.Should().HaveCount(3);
items.Should().Contain(x => x.Id == "room-123");

// Exceptions:
Action act = () => service.InvalidOperation();
act.Should().Throw<InvalidOperationException>()
   .WithMessage("Something went wrong");
```

### Mocking with Moq

Mock dependencies to isolate the unit under test:

```csharp
public class SettingsViewModelTests
{
    private readonly Mock<ISettingsService> _mockSettingsService;
    
    public SettingsViewModelTests()
    {
        _mockSettingsService = new Mock<ISettingsService>();
    }
    
    [Fact]
    public void Constructor_ShouldLoadSettings()
    {
        // Arrange - set up mock behavior
        var settings = new AppSettings { Theme = "Dark" };
        _mockSettingsService.Setup(s => s.GetSettings()).Returns(settings);
        
        // Act
        var viewModel = new SettingsViewModel(_mockSettingsService.Object);
        
        // Assert - verify the mock was called
        _mockSettingsService.Verify(s => s.GetSettings(), Times.Once);
        viewModel.SelectedTheme.Should().Be("Dark");
    }
}
```

## Test Categories

### Unit Tests (Current Focus)

- **Scope:** Individual classes in isolation
- **Dependencies:** Mocked
- **Speed:** Fast (< 100ms per test)
- **Coverage:** Services, ViewModels, Models, Converters

### Integration Tests (Future)

- **Scope:** Multiple components working together
- **Dependencies:** Real (or test doubles for external services)
- **Speed:** Moderate (< 5s per test)
- **Coverage:** API interactions, data persistence, event handling

### UI Tests (Future)

- **Scope:** End-to-end user workflows
- **Dependencies:** Real app running via WinAppDriver
- **Speed:** Slow (5-30s per test)
- **Coverage:** Critical user paths (setup, toggle room, change color)

## Continuous Integration

### PR Validation Workflow

Every pull request runs:
1. ✅ Build verification
2. ✅ Unit tests
3. ✅ Code coverage reporting
4. ✅ Warning-as-error check

PRs must pass all checks before merging.

### Main Branch CI

Pushes to `main` additionally:
1. ✅ Generate coverage HTML report
2. ✅ Upload artifacts
3. ✅ Update Codecov

## Best Practices

### ✅ Do

- Write tests for all new features
- Keep tests fast and focused
- Use descriptive test names
- Follow Arrange-Act-Assert pattern
- Mock external dependencies
- Test edge cases and error conditions
- Maintain 70%+ code coverage

### ❌ Don't

- Test implementation details (focus on behavior)
- Create tests that depend on execution order
- Share state between tests
- Mock the system under test
- Ignore failing tests (fix them!)
- Commit commented-out tests

## Debugging Tests

### Visual Studio

1. Set a breakpoint in your test
2. Right-click the test → **Debug**
3. Step through code like any debugging session

### Command Line with VSCode

1. Open `launch.json` in VSCode
2. Add a .NET test launch configuration:

```json
{
    "name": ".NET Core Test",
    "type": "coreclr",
    "request": "launch",
    "preLaunchTask": "build",
    "program": "dotnet",
    "args": ["test", "src/HueWindows.Tests"],
    "cwd": "${workspaceFolder}",
    "stopAtEntry": false
}
```

3. Set breakpoints and press F5

## Test Data Management

### Minimal Test Data

Create only the data needed for each test:

```csharp
// Good - minimal
var settings = new AppSettings { Theme = "Dark" };

// Avoid - unnecessary properties
var settings = new AppSettings 
{ 
    Theme = "Dark",
    BridgeIpAddress = "192.168.1.100",
    BridgeAppKey = "key123",
    // ... many more properties we don't need
};
```

### Test Fixtures (for Shared Setup)

Use constructor for test class setup:

```csharp
public class HueBridgeServiceTests : IDisposable
{
    private readonly Mock<IHttpClient> _mockHttp;
    
    public HueBridgeServiceTests()
    {
        // Runs before each test
        _mockHttp = new Mock<IHttpClient>();
    }
    
    public void Dispose()
    {
        // Runs after each test (cleanup)
    }
}
```

## Further Reading

- [xUnit Documentation](https://xunit.net/)
- [FluentAssertions Documentation](https://fluentassertions.com/introduction)
- [Moq Quickstart](https://github.com/moq/moq4/wiki/Quickstart)
- [Unit Testing Best Practices](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices)
