using FluentAssertions;
using HueCompanion.Core.Models;
using HueCompanion.Core.Services;
using Xunit;

namespace HueCompanion.Tests.Services;

public class EasingTests
{
    [Theory]
    [InlineData(TransitionStyle.Linear)]
    [InlineData(TransitionStyle.EaseIn)]
    [InlineData(TransitionStyle.EaseOut)]
    [InlineData(TransitionStyle.EaseInOut)]
    public void AllStyles_ReturnZeroAtStart(TransitionStyle style)
    {
        Easing.Apply(0.0, style).Should().Be(0.0);
    }

    [Theory]
    [InlineData(TransitionStyle.Linear)]
    [InlineData(TransitionStyle.EaseIn)]
    [InlineData(TransitionStyle.EaseOut)]
    [InlineData(TransitionStyle.EaseInOut)]
    public void AllStyles_ReturnOneAtEnd(TransitionStyle style)
    {
        Easing.Apply(1.0, style).Should().Be(1.0);
    }

    [Fact]
    public void Linear_ReturnsInput()
    {
        Easing.Apply(0.5, TransitionStyle.Linear).Should().Be(0.5);
        Easing.Apply(0.25, TransitionStyle.Linear).Should().Be(0.25);
        Easing.Apply(0.75, TransitionStyle.Linear).Should().Be(0.75);
    }

    [Fact]
    public void EaseIn_SlowStart()
    {
        // t^2 at 0.5 = 0.25
        Easing.Apply(0.5, TransitionStyle.EaseIn).Should().Be(0.25);
        // First half should be slower: value at 0.5 should be less than linear (0.5)
        Easing.Apply(0.5, TransitionStyle.EaseIn).Should().BeLessThan(0.5);
    }

    [Fact]
    public void EaseOut_SlowEnd()
    {
        // 1 - (1-0.5)^2 = 0.75
        Easing.Apply(0.5, TransitionStyle.EaseOut).Should().Be(0.75);
        // First half should be faster: value at 0.5 should be more than linear
        Easing.Apply(0.5, TransitionStyle.EaseOut).Should().BeGreaterThan(0.5);
    }

    [Fact]
    public void EaseInOut_SymmetricAtMidpoint()
    {
        // At 0.5: 2 * 0.5^2 = 0.5
        Easing.Apply(0.5, TransitionStyle.EaseInOut).Should().Be(0.5);
        // First quarter should be slow (ease-in), last quarter should be slow (ease-out)
        Easing.Apply(0.25, TransitionStyle.EaseInOut).Should().BeLessThan(0.25);
        Easing.Apply(0.75, TransitionStyle.EaseInOut).Should().BeGreaterThan(0.75);
    }

    [Fact]
    public void Instant_ZeroUntilComplete()
    {
        Easing.Apply(0.0, TransitionStyle.Instant).Should().Be(0.0);
        Easing.Apply(0.5, TransitionStyle.Instant).Should().Be(0.0);
        Easing.Apply(0.99, TransitionStyle.Instant).Should().Be(0.0);
        Easing.Apply(1.0, TransitionStyle.Instant).Should().Be(1.0);
    }

    [Theory]
    [InlineData(TransitionStyle.Linear)]
    [InlineData(TransitionStyle.EaseIn)]
    [InlineData(TransitionStyle.EaseOut)]
    [InlineData(TransitionStyle.EaseInOut)]
    public void AllSmooth_Monotonically_Increasing(TransitionStyle style)
    {
        // Sample 10 points and verify monotonically increasing
        var prev = -1.0;
        for (int i = 0; i <= 10; i++)
        {
            var t = i / 10.0;
            var value = Easing.Apply(t, style);
            value.Should().BeGreaterThanOrEqualTo(prev, $"at t={t}");
            prev = value;
        }
    }
}
