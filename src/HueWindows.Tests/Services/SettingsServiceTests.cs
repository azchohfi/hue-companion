using FluentAssertions;
using HueWindows.Core.Models;
using HueWindows.Core.Services;
using Xunit;

namespace HueWindows.Tests.Services;

/// <summary>
/// Unit tests for SettingsService
/// </summary>
public class SettingsServiceTests
{
    [Fact]
    public void GetSettings_ShouldReturnDefaultSettings_WhenNoSettingsExist()
    {
        // Arrange
        var service = new SettingsService();

        // Act
        var settings = service.GetSettings();

        // Assert
        settings.Should().NotBeNull();
        settings.BridgeIpAddress.Should().BeNullOrEmpty();
        settings.BridgeAppKey.Should().BeNullOrEmpty();
    }

    [Fact]
    public void SaveSettings_ShouldPersistSettings()
    {
        // Arrange
        var service = new SettingsService();
        var newSettings = new AppSettings
        {
            BridgeIpAddress = "192.168.1.100",
            BridgeAppKey = "test-app-key-12345",
            Theme = "Dark"
        };

        // Act
        service.SaveSettings(newSettings);
        var retrievedSettings = service.GetSettings();

        // Assert
        retrievedSettings.Should().NotBeNull();
        retrievedSettings.BridgeIpAddress.Should().Be("192.168.1.100");
        retrievedSettings.BridgeAppKey.Should().Be("test-app-key-12345");
        retrievedSettings.Theme.Should().Be("Dark");
    }

    [Fact]
    public void ClearSettings_ShouldResetToDefaults()
    {
        // Arrange
        var service = new SettingsService();
        var settings = new AppSettings
        {
            BridgeIpAddress = "192.168.1.100",
            BridgeAppKey = "test-key"
        };
        service.SaveSettings(settings);

        // Act
        service.ClearSettings();
        var clearedSettings = service.GetSettings();

        // Assert
        clearedSettings.Should().NotBeNull();
        clearedSettings.BridgeIpAddress.Should().BeNullOrEmpty();
        clearedSettings.BridgeAppKey.Should().BeNullOrEmpty();
    }
}
