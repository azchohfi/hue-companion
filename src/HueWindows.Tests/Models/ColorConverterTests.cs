using FluentAssertions;
using HueWindows.Core.Models;
using Xunit;

namespace HueWindows.Tests.Models;

public class ColorConverterTests
{
    [Fact]
    public void XyToRgb_WithZeroY_ReturnsWhite()
    {
        // Arrange - a degenerate color with Y = 0 should not produce NaN
        var color = new HueColor(0.5, 0.0);

        // Act
        var (r, g, b) = ColorConverter.XyToRgb(color, 1.0);

        // Assert - should return white fallback
        r.Should().Be(1.0);
        g.Should().Be(1.0);
        b.Should().Be(1.0);
    }

    [Fact]
    public void XyToRgb_WithNegativeY_ReturnsWhite()
    {
        var color = new HueColor(0.3, 0.0); // Y gets clamped to 0 by HueColor constructor
        var (r, g, b) = ColorConverter.XyToRgb(color, 1.0);

        r.Should().Be(1.0);
        g.Should().Be(1.0);
        b.Should().Be(1.0);
    }

    [Fact]
    public void XyToRgb_WithValidInput_ReturnsValidRgb()
    {
        // Arrange - warm white color
        var color = new HueColor(0.4596, 0.4105);

        // Act
        var (r, g, b) = ColorConverter.XyToRgb(color, 1.0);

        // Assert - values should be in [0, 1] range, not NaN/Infinity
        r.Should().BeInRange(0.0, 1.0);
        g.Should().BeInRange(0.0, 1.0);
        b.Should().BeInRange(0.0, 1.0);
    }

    [Fact]
    public void HsvRoundTrip_PreservesApproximateColor()
    {
        // Arrange
        var originalColor = new HueColor(0.3, 0.3);

        // Act - convert XY -> HSV -> XY
        var (h, s, v) = ColorConverter.XyToHsv(originalColor, 1.0);
        var (roundTripped, _) = ColorConverter.HsvToXy(h, s, v);

        // Assert - should be approximately the same
        roundTripped.X.Should().BeApproximately(originalColor.X, 0.05);
        roundTripped.Y.Should().BeApproximately(originalColor.Y, 0.05);
    }
}
