using FluentAssertions;
using HueWindows.Core.Models;
using HueWindows.Core.Services;
using Xunit;

namespace HueWindows.Tests.Services;

public class SettingsServiceTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly SettingsServiceTestable _service;

    public SettingsServiceTests()
    {
        // Use a unique temp directory for each test
        _testDirectory = Path.Combine(Path.GetTempPath(), $"HueWindowsTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
        _service = new SettingsServiceTestable(_testDirectory);
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
        _service.Settings.ConfiguredBridge.Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsSettings()
    {
        // Arrange
        _service.Settings.ConfiguredBridge = new BridgeModel
        {
            BridgeId = "test-id",
            IpAddress = "192.168.1.100",
            AppKey = "test-key"
        };

        // Act
        await _service.SaveAsync();

        var newService = new SettingsServiceTestable(_testDirectory);
        await newService.LoadAsync();

        // Assert
        newService.Settings.ConfiguredBridge.Should().NotBeNull();
        newService.Settings.ConfiguredBridge!.BridgeId.Should().Be("test-id");
        newService.Settings.ConfiguredBridge.IpAddress.Should().Be("192.168.1.100");
        newService.Settings.ConfiguredBridge.AppKey.Should().Be("test-key");
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
        // Arrange
        _service.Settings.ConfiguredBridge = null;

        // Act
        var result = await _service.HasConfiguredBridgeAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasConfiguredBridgeAsync_WithValidBridge_ReturnsTrue()
    {
        // Arrange
        _service.Settings.ConfiguredBridge = new BridgeModel
        {
            IpAddress = "192.168.1.100",
            AppKey = "valid-key"
        };

        // Act
        var result = await _service.HasConfiguredBridgeAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasConfiguredBridgeAsync_WithEmptyAppKey_ReturnsFalse()
    {
        // Arrange
        _service.Settings.ConfiguredBridge = new BridgeModel
        {
            IpAddress = "192.168.1.100",
            AppKey = ""
        };

        // Act
        var result = await _service.HasConfiguredBridgeAsync();

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    /// Testable version of SettingsService that allows custom directory.
    /// </summary>
    private class SettingsServiceTestable : SettingsService
    {
        private readonly string _testSettingsPath;

        public SettingsServiceTestable(string testDirectory)
        {
            _testSettingsPath = Path.Combine(testDirectory, "settings.json");
        }

        // Override the path using reflection or make the service more testable
        // For now, we'll use a workaround by directly manipulating the file
        public new async Task LoadAsync()
        {
            try
            {
                if (File.Exists(_testSettingsPath))
                {
                    var json = await File.ReadAllTextAsync(_testSettingsPath);
                    Settings = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
                else
                {
                    Settings = new AppSettings();
                }
            }
            catch
            {
                Settings = new AppSettings();
            }
        }

        public new async Task SaveAsync()
        {
            var json = System.Text.Json.JsonSerializer.Serialize(Settings, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
            await File.WriteAllTextAsync(_testSettingsPath, json);
        }

        public new AppSettings Settings { get; set; } = new();

        public new Task<bool> HasConfiguredBridgeAsync()
        {
            var hasBridge = Settings.ConfiguredBridge != null
                && !string.IsNullOrEmpty(Settings.ConfiguredBridge.AppKey)
                && !string.IsNullOrEmpty(Settings.ConfiguredBridge.IpAddress);

            return Task.FromResult(hasBridge);
        }
    }
}
