using FluentAssertions;
using HueWindows.Core.Models;
using HueWindows.Core.Services.Interfaces;
using HueWindows.Core.ViewModels;
using Moq;
using Xunit;

namespace HueWindows.Tests.ViewModels;

/// <summary>
/// Unit tests for SettingsViewModel
/// </summary>
public class SettingsViewModelTests
{
    private readonly Mock<ISettingsService> _mockSettingsService;
    private readonly Mock<INavigationService> _mockNavigationService;

    public SettingsViewModelTests()
    {
        _mockSettingsService = new Mock<ISettingsService>();
        _mockNavigationService = new Mock<INavigationService>();
    }

    [Fact]
    public void Constructor_ShouldLoadSettings()
    {
        // Arrange
        var settings = new AppSettings
        {
            Theme = "Dark",
            BridgeIpAddress = "192.168.1.100"
        };
        _mockSettingsService.Setup(s => s.GetSettings()).Returns(settings);

        // Act
        var viewModel = new SettingsViewModel(_mockSettingsService.Object, _mockNavigationService.Object);

        // Assert
        viewModel.SelectedTheme.Should().Be("Dark");
        viewModel.BridgeIpAddress.Should().Be("192.168.1.100");
    }

    [Fact]
    public void SelectedTheme_WhenChanged_ShouldUpdateSettings()
    {
        // Arrange
        var settings = new AppSettings { Theme = "Light" };
        _mockSettingsService.Setup(s => s.GetSettings()).Returns(settings);
        var viewModel = new SettingsViewModel(_mockSettingsService.Object, _mockNavigationService.Object);

        // Act
        viewModel.SelectedTheme = "Dark";

        // Assert
        _mockSettingsService.Verify(s => s.SaveSettings(It.Is<AppSettings>(a => a.Theme == "Dark")), Times.Once);
    }

    [Fact]
    public void DisconnectBridgeCommand_ShouldClearSettingsAndNavigate()
    {
        // Arrange
        var settings = new AppSettings
        {
            BridgeIpAddress = "192.168.1.100",
            BridgeAppKey = "test-key"
        };
        _mockSettingsService.Setup(s => s.GetSettings()).Returns(settings);
        var viewModel = new SettingsViewModel(_mockSettingsService.Object, _mockNavigationService.Object);

        // Act
        viewModel.DisconnectBridgeCommand.Execute(null);

        // Assert
        _mockSettingsService.Verify(s => s.ClearSettings(), Times.Once);
        _mockNavigationService.Verify(n => n.NavigateTo(It.IsAny<string>()), Times.Once);
    }
}
