using FluentAssertions;
using HueCompanion.Core.Models;
using HueCompanion.Core.Services;
using Xunit;

namespace HueCompanion.Tests.Services;

public class SettingsServiceTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly SettingsService _service;

    public SettingsServiceTests()
    {
        // Use a unique temp directory for each test
        _testDirectory = Path.Combine(Path.GetTempPath(), $"HueCompanionTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
        _service = new SettingsService(_testDirectory);
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
    public void Constructor_InitializesDefaultSettings()
    {
        // Assert
        _service.Settings.Should().NotBeNull();
    }

    [Fact]
    public async Task LoadAsync_WithNoFile_CreatesDefaultSettings()
    {
        // Act
        await _service.LoadAsync();

        // Assert
        _service.Settings.Should().NotBeNull();
        _service.Settings.ConfiguredBridges.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsSettings()
    {
        // Arrange - use the multi-bridge list (current API)
        _service.Settings.ConfiguredBridges.Add(new BridgeModel
        {
            BridgeId = "test-id",
            IpAddress = "192.168.1.100",
            AppKey = "test-key"
        });

        // Act
        await _service.SaveAsync();

        var newService = new SettingsService(_testDirectory);
        await newService.LoadAsync();

        // Assert
        newService.Settings.ConfiguredBridges.Should().HaveCount(1);
        newService.Settings.ConfiguredBridges[0].BridgeId.Should().Be("test-id");
        newService.Settings.ConfiguredBridges[0].IpAddress.Should().Be("192.168.1.100");
        newService.Settings.ConfiguredBridges[0].AppKey.Should().Be("test-key");
    }

    [Fact]
    public async Task LoadAsync_WithCorruptedFile_CreatesDefaultSettings()
    {
        // Arrange - write invalid JSON
        var settingsPath = Path.Combine(_testDirectory, "settings.json");
        await File.WriteAllTextAsync(settingsPath, "{ invalid json }");

        // Act
        await _service.LoadAsync();

        // Assert
        _service.Settings.Should().NotBeNull();
    }

    [Fact]
    public async Task HasConfiguredBridgeAsync_WithNoBridge_ReturnsFalse()
    {
        // Arrange - empty ConfiguredBridges (default)

        // Act
        var result = await _service.HasConfiguredBridgeAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasConfiguredBridgeAsync_WithValidBridge_ReturnsTrue()
    {
        // Arrange
        _service.Settings.ConfiguredBridges.Add(new BridgeModel
        {
            IpAddress = "192.168.1.100",
            AppKey = "valid-key"
        });

        // Act
        var result = await _service.HasConfiguredBridgeAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasConfiguredBridgeAsync_WithEmptyAppKey_ReturnsFalse()
    {
        // Arrange
        _service.Settings.ConfiguredBridges.Add(new BridgeModel
        {
            IpAddress = "192.168.1.100",
            AppKey = ""
        });

        // Act
        var result = await _service.HasConfiguredBridgeAsync();

        // Assert
        result.Should().BeFalse();
    }
}
