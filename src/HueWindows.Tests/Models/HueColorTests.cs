using FluentAssertions;
using HueWindows.Core.Models;
using Xunit;

namespace HueWindows.Tests.Models;

public class HueColorTests
{
    [Fact]
    public void Constructor_ClampsXToValidRange()
    {
        // Arrange & Act
        var colorTooHigh = new HueColor(1.5, 0.5);
        var colorTooLow = new HueColor(-0.5, 0.5);

        // Assert
        colorTooHigh.X.Should().Be(1.0);
        colorTooLow.X.Should().Be(0.0);
    }

    [Fact]
    public void Constructor_ClampsYToValidRange()
    {
        // Arrange & Act
        var colorTooHigh = new HueColor(0.5, 1.5);
        var colorTooLow = new HueColor(0.5, -0.5);

        // Assert
        colorTooHigh.Y.Should().Be(1.0);
        colorTooLow.Y.Should().Be(0.0);
    }

    [Fact]
    public void White_ReturnsD65WhitePoint()
    {
        // Act
        var white = HueColor.White;

        // Assert
        white.X.Should().BeApproximately(0.3127, 0.0001);
        white.Y.Should().BeApproximately(0.3290, 0.0001);
    }

    [Fact]
    public void WarmWhite_ReturnsWarmCoordinates()
    {
        // Act
        var warmWhite = HueColor.WarmWhite;

        // Assert
        warmWhite.X.Should().BeApproximately(0.4596, 0.0001);
        warmWhite.Y.Should().BeApproximately(0.4105, 0.0001);
    }

    [Theory]
    [InlineData(255, 0, 0)]   // Red
    [InlineData(0, 255, 0)]   // Green
    [InlineData(0, 0, 255)]   // Blue
    [InlineData(255, 255, 255)] // White
    [InlineData(0, 0, 0)]     // Black -> should return white point
    public void FromRgb_RoundTripsApproximately(byte r, byte g, byte b)
    {
        // Arrange & Act
        var hueColor = HueColor.FromRgb(r, g, b);
        var (resultR, resultG, resultB) = hueColor.ToRgb(1.0);

        // Assert - RGB round-trip should be close (color space conversion has some loss)
        // Black is a special case that returns white
        if (r == 0 && g == 0 && b == 0)
        {
            resultR.Should().Be(255);
            resultG.Should().Be(255);
            resultB.Should().Be(255);
        }
        else
        {
            resultR.Should().BeCloseTo(r, 30); // Allow some tolerance for conversion
            resultG.Should().BeCloseTo(g, 30);
            resultB.Should().BeCloseTo(b, 30);
        }
    }

    [Fact]
    public void ToRgb_WithZeroY_ReturnsWhite()
    {
        // Arrange
        var color = new HueColor(0.5, 0);

        // Act
        var (r, g, b) = color.ToRgb();

        // Assert
        r.Should().Be(255);
        g.Should().Be(255);
        b.Should().Be(255);
    }

    [Theory]
    [InlineData(153, 0.31, 0.33)]  // 6500K cool white - approximate
    [InlineData(500, 0.52, 0.41)]  // 2000K warm - approximate
    public void FromMirek_ReturnsExpectedCoordinates(int mirek, double expectedX, double expectedY)
    {
        // Act
        var color = HueColor.FromMirek(mirek);

        // Assert
        color.X.Should().BeApproximately(expectedX, 0.02);
        color.Y.Should().BeApproximately(expectedY, 0.02);
    }

    [Fact]
    public void FromMirek_ClampsTooLow()
    {
        // Act
        var color = HueColor.FromMirek(100); // Below min 153

        // Assert - should clamp to 153 (6500K)
        color.X.Should().BeApproximately(0.3127, 0.02);
    }

    [Fact]
    public void FromMirek_ClampsTooHigh()
    {
        // Act
        var color = HueColor.FromMirek(600); // Above max 500

        // Assert - should clamp to 500 (2000K)
        color.X.Should().BeApproximately(0.5267, 0.02);
    }

    [Theory]
    [InlineData(0.9, 0.1, true)]  // Very bright yellow -> black text
    [InlineData(0.15, 0.06, false)] // Blue -> white text
    public void ShouldUseBlackText_ReturnsExpectedValue(double x, double y, bool expected)
    {
        // Arrange
        var color = new HueColor(x, y);

        // Act
        var result = color.ShouldUseBlackText(1.0);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void GetRelativeLuminance_WhiteIsHigh()
    {
        // Arrange
        var white = HueColor.White;

        // Act
        var luminance = white.GetRelativeLuminance(1.0);

        // Assert
        luminance.Should().BeGreaterThan(0.5);
    }
}
