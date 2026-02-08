using FluentAssertions;
using HueWindows.Core.Models;
using HueWindows.Core.Services;
using HueWindows.Core.Services.Interfaces;
using Moq;
using Xunit;

namespace HueWindows.Tests.Services;

public class AnimationEngineTests : IDisposable
{
    private readonly Mock<IHueBridgeService> _bridgeMock = new();
    private readonly List<Guid> _lights;
    private AnimationEngine? _engine;

    public AnimationEngineTests()
    {
        _lights = new List<Guid>
        {
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid()
        };
    }

    public void Dispose()
    {
        _engine?.Dispose();
    }

    #region FindSurroundingKeyframes

    [Fact]
    public void FindSurroundingKeyframes_MidwayBetweenTwo_ReturnsCorrectPair()
    {
        var scene = CreateKeyframeScene(new AnimationKeyframe[]
        {
            new() { TimeSeconds = 0, Color = HueColor.WarmWhite, Brightness = 1.0 },
            new() { TimeSeconds = 2, Color = new HueColor(0.15, 0.06), Brightness = 0.5 },
            new() { TimeSeconds = 4, Color = HueColor.WarmWhite, Brightness = 1.0 },
        });

        _engine = new AnimationEngine(_bridgeMock.Object, scene, _lights);

        var (prev, next) = _engine.FindSurroundingKeyframes(scene.Animations[0].Keyframes, 1.0);

        prev!.TimeSeconds.Should().Be(0);
        next!.TimeSeconds.Should().Be(2);
    }

    [Fact]
    public void FindSurroundingKeyframes_AtExactKeyframeTime_ReturnsSameForBoth()
    {
        var scene = CreateKeyframeScene(new AnimationKeyframe[]
        {
            new() { TimeSeconds = 0, Color = HueColor.WarmWhite, Brightness = 1.0 },
            new() { TimeSeconds = 2, Color = new HueColor(0.15, 0.06), Brightness = 0.5 },
            new() { TimeSeconds = 4, Color = HueColor.WarmWhite, Brightness = 1.0 },
        });

        _engine = new AnimationEngine(_bridgeMock.Object, scene, _lights);

        var (prev, next) = _engine.FindSurroundingKeyframes(scene.Animations[0].Keyframes, 2.0);

        prev!.TimeSeconds.Should().Be(2);
        next!.TimeSeconds.Should().Be(2);
    }

    [Fact]
    public void FindSurroundingKeyframes_BeforeFirstKeyframe_PrevIsNull()
    {
        var keyframes = new List<AnimationKeyframe>
        {
            new() { TimeSeconds = 1.0, Color = HueColor.WarmWhite, Brightness = 1.0 },
            new() { TimeSeconds = 3.0, Color = new HueColor(0.15, 0.06), Brightness = 0.5 },
        };
        var scene = CreateKeyframeScene(keyframes.ToArray());
        _engine = new AnimationEngine(_bridgeMock.Object, scene, _lights);

        var (prev, next) = _engine.FindSurroundingKeyframes(keyframes, 0.5);

        prev.Should().BeNull();
        next!.TimeSeconds.Should().Be(1.0);
    }

    [Fact]
    public void FindSurroundingKeyframes_AfterLastKeyframe_NextIsNull()
    {
        var keyframes = new List<AnimationKeyframe>
        {
            new() { TimeSeconds = 0, Color = HueColor.WarmWhite, Brightness = 1.0 },
            new() { TimeSeconds = 2, Color = new HueColor(0.15, 0.06), Brightness = 0.5 },
        };
        var scene = CreateKeyframeScene(keyframes.ToArray());
        _engine = new AnimationEngine(_bridgeMock.Object, scene, _lights);

        var (prev, next) = _engine.FindSurroundingKeyframes(keyframes, 5.0);

        prev!.TimeSeconds.Should().Be(2);
        next.Should().BeNull();
    }

    [Fact]
    public void FindSurroundingKeyframes_SingleKeyframe_ReturnsSameForBoth()
    {
        var keyframes = new List<AnimationKeyframe>
        {
            new() { TimeSeconds = 1.0, Color = HueColor.WarmWhite, Brightness = 1.0 },
        };
        var scene = CreateKeyframeScene(keyframes.ToArray());
        _engine = new AnimationEngine(_bridgeMock.Object, scene, _lights);

        var (prev, next) = _engine.FindSurroundingKeyframes(keyframes, 1.0);

        prev!.TimeSeconds.Should().Be(1.0);
        next!.TimeSeconds.Should().Be(1.0);
    }

    #endregion

    #region Light Assignment

    [Fact]
    public async Task Start_WithLightAssignmentAll_TargetsAllLights()
    {
        var scene = CreateKeyframeScene(
            new AnimationKeyframe[]
            {
                new() { TimeSeconds = 0, Color = HueColor.WarmWhite, Brightness = 1.0 },
                new() { TimeSeconds = 0.1, Color = new HueColor(0.15, 0.06), Brightness = 0.5 },
            },
            lightAssignment: LightAssignment.All,
            durationSeconds: 0.1,
            repeatMode: RepeatMode.Once);

        _engine = new AnimationEngine(_bridgeMock.Object, scene, _lights);
        _engine.Start();

        // Wait for the short animation to complete plus buffer
        await Task.Delay(300);

        // All 4 lights should have received at least one color call
        foreach (var lightId in _lights)
        {
            _bridgeMock.Verify(b =>
                b.SetLightColorAndBrightnessAsync(lightId, It.IsAny<HueColor>(), It.IsAny<double>()),
                Times.AtLeastOnce());
        }
    }

    [Fact]
    public async Task Start_WithLightAssignmentSubset_TargetsOnlySpecifiedLights()
    {
        var scene = CreateKeyframeScene(
            new AnimationKeyframe[]
            {
                new() { TimeSeconds = 0, Color = HueColor.WarmWhite, Brightness = 1.0 },
                new() { TimeSeconds = 0.1, Color = new HueColor(0.15, 0.06), Brightness = 0.5 },
            },
            lightAssignment: LightAssignment.Subset,
            targetIndices: new List<int> { 0, 2 },
            durationSeconds: 0.1,
            repeatMode: RepeatMode.Once);

        _engine = new AnimationEngine(_bridgeMock.Object, scene, _lights);
        _engine.Start();

        await Task.Delay(300);

        // Lights at index 0 and 2 should be targeted
        _bridgeMock.Verify(b =>
            b.SetLightColorAndBrightnessAsync(_lights[0], It.IsAny<HueColor>(), It.IsAny<double>()),
            Times.AtLeastOnce());
        _bridgeMock.Verify(b =>
            b.SetLightColorAndBrightnessAsync(_lights[2], It.IsAny<HueColor>(), It.IsAny<double>()),
            Times.AtLeastOnce());

        // Lights at index 1 and 3 should NOT be targeted
        _bridgeMock.Verify(b =>
            b.SetLightColorAndBrightnessAsync(_lights[1], It.IsAny<HueColor>(), It.IsAny<double>()),
            Times.Never());
        _bridgeMock.Verify(b =>
            b.SetLightColorAndBrightnessAsync(_lights[3], It.IsAny<HueColor>(), It.IsAny<double>()),
            Times.Never());
    }

    [Fact]
    public async Task Start_WithLightAssignmentAlternating_TargetsEvenIndexedLights()
    {
        var scene = CreateKeyframeScene(
            new AnimationKeyframe[]
            {
                new() { TimeSeconds = 0, Color = HueColor.WarmWhite, Brightness = 1.0 },
                new() { TimeSeconds = 0.1, Color = new HueColor(0.15, 0.06), Brightness = 0.5 },
            },
            lightAssignment: LightAssignment.Alternating,
            durationSeconds: 0.1,
            repeatMode: RepeatMode.Once);

        _engine = new AnimationEngine(_bridgeMock.Object, scene, _lights);
        _engine.Start();

        await Task.Delay(300);

        // Even-indexed lights (0, 2) should be targeted
        _bridgeMock.Verify(b =>
            b.SetLightColorAndBrightnessAsync(_lights[0], It.IsAny<HueColor>(), It.IsAny<double>()),
            Times.AtLeastOnce());
        _bridgeMock.Verify(b =>
            b.SetLightColorAndBrightnessAsync(_lights[2], It.IsAny<HueColor>(), It.IsAny<double>()),
            Times.AtLeastOnce());

        // Odd-indexed lights (1, 3) should NOT be targeted
        _bridgeMock.Verify(b =>
            b.SetLightColorAndBrightnessAsync(_lights[1], It.IsAny<HueColor>(), It.IsAny<double>()),
            Times.Never());
        _bridgeMock.Verify(b =>
            b.SetLightColorAndBrightnessAsync(_lights[3], It.IsAny<HueColor>(), It.IsAny<double>()),
            Times.Never());
    }

    #endregion

    #region Keyframe Animation Behavior

    [Fact]
    public async Task Start_KeyframeAnimation_InterpolatesColorAndBrightness()
    {
        var scene = CreateKeyframeScene(
            new AnimationKeyframe[]
            {
                new() { TimeSeconds = 0, Color = HueColor.WarmWhite, Brightness = 1.0 },
                new() { TimeSeconds = 0.15, Color = new HueColor(0.15, 0.06), Brightness = 0.2 },
            },
            durationSeconds: 0.15,
            repeatMode: RepeatMode.Once);

        // Only use 1 light to simplify verification
        var singleLight = new List<Guid> { _lights[0] };
        _engine = new AnimationEngine(_bridgeMock.Object, scene, singleLight);

        var capturedColors = new List<HueColor>();
        var capturedBrightness = new List<double>();
        _bridgeMock.Setup(b => b.SetLightColorAndBrightnessAsync(
                _lights[0], It.IsAny<HueColor>(), It.IsAny<double>()))
            .Callback<Guid, HueColor, double>((_, color, brightness) =>
            {
                capturedColors.Add(color);
                capturedBrightness.Add(brightness);
            })
            .Returns(Task.CompletedTask);

        _engine.Start();
        await Task.Delay(400);

        // Should have made multiple interpolation calls
        capturedColors.Should().HaveCountGreaterThan(1);

        // Brightness should generally decrease from 1.0 toward 0.2
        capturedBrightness.First().Should().BeGreaterThan(capturedBrightness.Last());
    }

    [Fact]
    public async Task Start_RepeatModeOnce_StopsAfterOneCycle()
    {
        var scene = CreateKeyframeScene(
            new AnimationKeyframe[]
            {
                new() { TimeSeconds = 0, Color = HueColor.WarmWhite, Brightness = 1.0 },
                new() { TimeSeconds = 0.1, Color = new HueColor(0.15, 0.06), Brightness = 0.5 },
            },
            durationSeconds: 0.1,
            repeatMode: RepeatMode.Once);

        var singleLight = new List<Guid> { _lights[0] };
        _engine = new AnimationEngine(_bridgeMock.Object, scene, singleLight);

        _engine.Start();

        // Wait for animation to complete
        await Task.Delay(300);

        var callCountAfterComplete = _bridgeMock.Invocations.Count;

        // Wait more — no additional calls should happen
        await Task.Delay(200);

        _bridgeMock.Invocations.Count.Should().Be(callCountAfterComplete);
    }

    #endregion

    #region Native Effect

    [Fact]
    public async Task Start_NativeEffect_CallsApplyEffectForEachLight()
    {
        var scene = new AnimatedSceneModel
        {
            Id = "test",
            Name = "Test",
            Animations = new List<AnimationDefinition>
            {
                new()
                {
                    Type = AnimationType.NativeEffect,
                    Name = "fire",
                    LightAssignment = LightAssignment.All,
                }
            }
        };

        _engine = new AnimationEngine(_bridgeMock.Object, scene, _lights);
        _engine.Start();

        // Give time for effects to be applied
        await Task.Delay(200);

        foreach (var lightId in _lights)
        {
            _bridgeMock.Verify(b =>
                b.ApplyEffectAsync(lightId, "fire", null, null),
                Times.Once());
        }

        await _engine.StopAsync();
    }

    #endregion

    #region Start/Stop/Dispose Lifecycle

    [Fact]
    public async Task StopAsync_CancelsRunningAnimations()
    {
        var scene = CreateKeyframeScene(
            new AnimationKeyframe[]
            {
                new() { TimeSeconds = 0, Color = HueColor.WarmWhite, Brightness = 1.0 },
                new() { TimeSeconds = 10, Color = new HueColor(0.15, 0.06), Brightness = 0.5 },
            },
            durationSeconds: 10,
            repeatMode: RepeatMode.Loop);

        var singleLight = new List<Guid> { _lights[0] };
        _engine = new AnimationEngine(_bridgeMock.Object, scene, singleLight);

        _engine.Start();
        await Task.Delay(100); // Let it run briefly

        await _engine.StopAsync(); // Should not hang

        var callCountAfterStop = _bridgeMock.Invocations.Count;
        await Task.Delay(200);

        // No new calls after stop
        _bridgeMock.Invocations.Count.Should().Be(callCountAfterStop);
    }

    [Fact]
    public void Start_AfterDispose_ThrowsObjectDisposedException()
    {
        var scene = CreateKeyframeScene(new AnimationKeyframe[]
        {
            new() { TimeSeconds = 0, Color = HueColor.WarmWhite, Brightness = 1.0 },
        });

        _engine = new AnimationEngine(_bridgeMock.Object, scene, _lights);
        _engine.Dispose();

        var act = () => _engine.Start();

        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Start_WithNoAnimations_DoesNotThrow()
    {
        var scene = new AnimatedSceneModel
        {
            Id = "empty",
            Name = "Empty",
            Animations = new List<AnimationDefinition>()
        };

        _engine = new AnimationEngine(_bridgeMock.Object, scene, _lights);

        var act = () => _engine.Start();

        act.Should().NotThrow();
    }

    [Fact]
    public void Start_WithEmptyLightList_SkipsAnimation()
    {
        var scene = CreateKeyframeScene(new AnimationKeyframe[]
        {
            new() { TimeSeconds = 0, Color = HueColor.WarmWhite, Brightness = 1.0 },
            new() { TimeSeconds = 1, Color = new HueColor(0.15, 0.06), Brightness = 0.5 },
        });

        _engine = new AnimationEngine(_bridgeMock.Object, scene, new List<Guid>());
        _engine.Start();

        // No bridge calls should be made
        _bridgeMock.VerifyNoOtherCalls();
    }

    #endregion

    #region Helpers

    private static AnimatedSceneModel CreateKeyframeScene(
        AnimationKeyframe[] keyframes,
        LightAssignment lightAssignment = LightAssignment.All,
        List<int>? targetIndices = null,
        double durationSeconds = 4.0,
        RepeatMode repeatMode = RepeatMode.Loop)
    {
        return new AnimatedSceneModel
        {
            Id = "test-scene",
            Name = "Test Scene",
            Animations = new List<AnimationDefinition>
            {
                new()
                {
                    Id = "anim-1",
                    Name = "Test Animation",
                    Type = AnimationType.Keyframe,
                    LightAssignment = lightAssignment,
                    TargetLightIndices = targetIndices ?? new List<int>(),
                    Keyframes = keyframes.ToList(),
                    DurationSeconds = durationSeconds,
                    RepeatMode = repeatMode,
                }
            }
        };
    }

    #endregion
}
