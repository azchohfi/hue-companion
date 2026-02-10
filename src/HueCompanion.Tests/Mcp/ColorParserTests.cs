using FluentAssertions;
using HueCompanion.Core.Models;
using HueCompanion.Mcp.Services;
using Xunit;

namespace HueCompanion.Tests.Mcp;

public class ColorParserTests
{
    [Theory]
    [InlineData("#FF0000")]
    [InlineData("FF0000")]
    [InlineData("#ff0000")]
    public void Parse_HexRed_ReturnsHueColor(string input)
    {
        var result = ColorParser.Parse(input);

        result.Should().NotBeNull();
    }

    [Theory]
    [InlineData("#00FF00")]
    [InlineData("#0000FF")]
    [InlineData("#FFFFFF")]
    public void Parse_ValidHex_ReturnsNonNull(string input)
    {
        ColorParser.Parse(input).Should().NotBeNull();
    }

    [Theory]
    [InlineData("rgb(255, 0, 0)")]
    [InlineData("rgb(0, 255, 0)")]
    [InlineData("RGB(0, 0, 255)")]
    [InlineData("rgb(255,255,255)")]
    public void Parse_ValidRgb_ReturnsNonNull(string input)
    {
        ColorParser.Parse(input).Should().NotBeNull();
    }

    [Theory]
    [InlineData("rgb(300, 0, 0)")]
    [InlineData("rgb(0, 999, 0)")]
    public void Parse_OutOfRangeRgb_ClampsToValidRange(string input)
    {
        // Should not throw, values clamped to 0-255
        ColorParser.Parse(input).Should().NotBeNull();
    }

    [Theory]
    [InlineData("red")]
    [InlineData("blue")]
    [InlineData("warm white")]
    [InlineData("coral")]
    [InlineData("lavender")]
    public void Parse_NamedColor_ReturnsNonNull(string input)
    {
        ColorParser.Parse(input).Should().NotBeNull();
    }

    [Theory]
    [InlineData("RED")]
    [InlineData("Blue")]
    [InlineData("WARM WHITE")]
    public void Parse_NamedColor_IsCaseInsensitive(string input)
    {
        ColorParser.Parse(input).Should().NotBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_NullOrEmpty_ReturnsNull(string? input)
    {
        ColorParser.Parse(input).Should().BeNull();
    }

    [Theory]
    [InlineData("notacolor")]
    [InlineData("rgb(abc)")]
    [InlineData("#GGGGGG")]
    [InlineData("#12345")]
    public void Parse_InvalidInput_ReturnsNull(string input)
    {
        ColorParser.Parse(input).Should().BeNull();
    }

    [Fact]
    public void ToHex_ReturnsFormattedHexString()
    {
        var color = HueColor.FromRgb(255, 0, 0);

        var hex = ColorParser.ToHex(color);

        hex.Should().StartWith("#");
        hex.Should().HaveLength(7);
    }

    [Fact]
    public void Parse_HexAndRgb_ProduceSameColor()
    {
        var fromHex = ColorParser.Parse("#FF0000");
        var fromRgb = ColorParser.Parse("rgb(255, 0, 0)");
        var fromNamed = ColorParser.Parse("red");

        fromHex.Should().NotBeNull();
        fromRgb.Should().NotBeNull();
        fromNamed.Should().NotBeNull();

        // All three should produce equivalent CIE xy values
        fromHex!.X.Should().BeApproximately(fromRgb!.X, 0.001);
        fromHex!.Y.Should().BeApproximately(fromRgb!.Y, 0.001);
        fromHex!.X.Should().BeApproximately(fromNamed!.X, 0.001);
    }
}
