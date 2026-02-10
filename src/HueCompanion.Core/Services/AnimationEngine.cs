using System.Net.Http;
using HueCompanion.Core.Models;
using HueCompanion.Core.Services.Interfaces;

namespace HueCompanion.Core.Services;

/// <summary>
/// Core animation engine that executes keyframe and event-based animations.
/// </summary>
public class AnimationEngine : IDisposable
{
    private readonly IHueBridgeService _bridgeService;
    private readonly AnimatedSceneModel _scene;
    private readonly List<Guid> _targetLights;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly List<Task> _animationTasks = new();
    private bool _isDisposed;

    public AnimationEngine(
        IHueBridgeService bridgeService,
        AnimatedSceneModel scene,
        List<Guid> targetLights)
    {
        _bridgeService = bridgeService ?? throw new ArgumentNullException(nameof(bridgeService));
        _scene = scene ?? throw new ArgumentNullException(nameof(scene));
        _targetLights = targetLights ?? throw new ArgumentNullException(nameof(targetLights));
        _cancellationTokenSource = new CancellationTokenSource();
    }

    /// <summary>
    /// Starts all animations in the scene.
    /// </summary>
    public void Start()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(AnimationEngine));

        foreach (var animation in _scene.Animations)
        {
            var lightsForAnimation = SelectLightsForAnimation(animation);
            if (lightsForAnimation.Count == 0)
                continue;

            Task animationTask = animation.Type switch
            {
                AnimationType.Keyframe => RunKeyframeAnimationAsync(animation, lightsForAnimation, _cancellationTokenSource.Token),
                AnimationType.Event => RunEventAnimationAsync(animation, lightsForAnimation, _cancellationTokenSource.Token),
                AnimationType.NativeEffect => RunNativeEffectAsync(animation, lightsForAnimation, _cancellationTokenSource.Token),
                _ => Task.CompletedTask
            };

            _animationTasks.Add(animationTask);
        }
    }

    /// <summary>
    /// Stops all running animations.
    /// </summary>
    public async Task StopAsync()
    {
        if (_isDisposed)
            return;

        _cancellationTokenSource.Cancel();

        try
        {
            await Task.WhenAll(_animationTasks);
        }
        catch (OperationCanceledException)
        {
            // Expected when animations are cancelled
        }
    }

    private List<Guid> SelectLightsForAnimation(AnimationDefinition animation)
    {
        return animation.LightAssignment switch
        {
            LightAssignment.All => _targetLights.ToList(),
            LightAssignment.Subset => SelectSubsetLights(animation),
            LightAssignment.Random => SelectRandomLights(animation),
            LightAssignment.Alternating => SelectAlternatingLights(),
            _ => _targetLights.ToList()
        };
    }

    private List<Guid> SelectSubsetLights(AnimationDefinition animation)
    {
        var selected = new List<Guid>();
        foreach (var index in animation.TargetLightIndices)
        {
            if (index >= 0 && index < _targetLights.Count)
            {
                selected.Add(_targetLights[index]);
            }
        }
        return selected;
    }

    private List<Guid> SelectRandomLights(AnimationDefinition animation)
    {
        var percentage = animation.RandomPercentage ?? 0.5;
        var count = Math.Max(1, (int)(_targetLights.Count * percentage));
        return _targetLights.OrderBy(_ => Random.Shared.Next()).Take(count).ToList();
    }

    private List<Guid> SelectAlternatingLights()
    {
        return _targetLights.Where((_, index) => index % 2 == 0).ToList();
    }

    private async Task RunKeyframeAnimationAsync(
        AnimationDefinition animation,
        List<Guid> lights,
        CancellationToken cancellationToken)
    {
        try
        {
            var startTime = DateTimeOffset.UtcNow;
            var isReversed = false; // Track direction for PingPong mode

            while (!cancellationToken.IsCancellationRequested)
            {
                var elapsed = (DateTimeOffset.UtcNow - startTime).TotalSeconds;
                var progress = elapsed / animation.DurationSeconds;

                // Handle repeat modes
                if (progress >= 1.0)
                {
                    if (animation.RepeatMode == RepeatMode.Once)
                    {
                        // Apply final keyframe and stop
                        await ApplyKeyframe(animation.Keyframes[^1], lights, cancellationToken);
                        break;
                    }
                    else if (animation.RepeatMode == RepeatMode.Loop)
                    {
                        startTime = DateTimeOffset.UtcNow;
                        progress = 0;
                    }
                    else if (animation.RepeatMode == RepeatMode.PingPong)
                    {
                        // Toggle direction for next iteration
                        isReversed = !isReversed;
                        startTime = DateTimeOffset.UtcNow;
                        progress = 0;
                    }
                }

                // For PingPong in reverse direction, invert the progress
                var effectiveProgress = isReversed ? (1.0 - progress) : progress;

                // Find the two keyframes we're between
                var currentTime = effectiveProgress * animation.DurationSeconds;
                var (prevFrame, nextFrame) = FindSurroundingKeyframes(animation.Keyframes, currentTime);

                if (prevFrame != null && nextFrame != null)
                {
                    // Interpolate between keyframes
                    var frameProgress = (currentTime - prevFrame.TimeSeconds) / (nextFrame.TimeSeconds - prevFrame.TimeSeconds);
                    await InterpolateAndApplyKeyframes(prevFrame, nextFrame, frameProgress, lights, cancellationToken);
                }

                // Wait before next frame (target ~30 FPS for smooth animations)
                await Task.Delay(33, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Animation was stopped
        }
    }

    internal (AnimationKeyframe? prev, AnimationKeyframe? next) FindSurroundingKeyframes(
        List<AnimationKeyframe> keyframes,
        double currentTime)
    {
        AnimationKeyframe? prev = null;
        AnimationKeyframe? next = null;

        for (int i = 0; i < keyframes.Count; i++)
        {
            if (keyframes[i].TimeSeconds <= currentTime)
            {
                prev = keyframes[i];
            }
            if (keyframes[i].TimeSeconds >= currentTime && next == null)
            {
                next = keyframes[i];
                break;
            }
        }

        return (prev, next);
    }

    private async Task InterpolateAndApplyKeyframes(
        AnimationKeyframe prev,
        AnimationKeyframe next,
        double progress,
        List<Guid> lights,
        CancellationToken cancellationToken)
    {
        // Apply easing based on transition style
        progress = Easing.Apply(progress, next.TransitionStyle);

        foreach (var lightId in lights)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                // Interpolate color and brightness together
                if (prev.Color != null && next.Color != null &&
                    prev.Brightness.HasValue && next.Brightness.HasValue)
                {
                    var color = ColorConverter.InterpolateXy(prev.Color, next.Color, progress);
                    var brightness = prev.Brightness.Value + (next.Brightness.Value - prev.Brightness.Value) * progress;
                    // Send color and brightness atomically to avoid white flash
                    await _bridgeService.SetLightColorAndBrightnessAsync(lightId, color, brightness);
                }
                else if (prev.Brightness.HasValue && next.Brightness.HasValue)
                {
                    var brightness = prev.Brightness.Value + (next.Brightness.Value - prev.Brightness.Value) * progress;
                    await _bridgeService.SetLightBrightnessAsync(lightId, brightness);
                }
                else if (prev.Color != null && next.Color != null)
                {
                    var color = ColorConverter.InterpolateXy(prev.Color, next.Color, progress);
                    await _bridgeService.SetLightColorAsync(lightId, color);
                }

                // Interpolate color temperature
                if (prev.ColorTemperature.HasValue && next.ColorTemperature.HasValue)
                {
                    var mirek = (int)(prev.ColorTemperature.Value + (next.ColorTemperature.Value - prev.ColorTemperature.Value) * progress);
                    await _bridgeService.SetLightTemperatureAsync(lightId, mirek);
                }

                // Handle on/off state changes (instant, not interpolated)
                if (progress > 0.5 && next.IsOn.HasValue)
                {
                    await _bridgeService.SetLightOnAsync(lightId, next.IsOn.Value);
                }
            }
            catch (HttpRequestException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AnimationEngine] HTTP error: {ex.Message}");
            }
            catch (TaskCanceledException)
            {
                // Expected during shutdown/cancellation
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AnimationEngine] Unexpected error: {ex.Message}");
            }
        }
    }

    private async Task ApplyKeyframe(AnimationKeyframe keyframe, List<Guid> lights, CancellationToken cancellationToken)
    {
        foreach (var lightId in lights)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                if (keyframe.IsOn.HasValue)
                {
                    await _bridgeService.SetLightOnAsync(lightId, keyframe.IsOn.Value);
                }

                if (keyframe.Brightness.HasValue)
                {
                    await _bridgeService.SetLightBrightnessAsync(lightId, keyframe.Brightness.Value);
                }

                if (keyframe.Color != null)
                {
                    await _bridgeService.SetLightColorAsync(lightId, keyframe.Color);
                }

                if (keyframe.ColorTemperature.HasValue)
                {
                    await _bridgeService.SetLightTemperatureAsync(lightId, keyframe.ColorTemperature.Value);
                }
            }
            catch (HttpRequestException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AnimationEngine.ApplyKeyframe] HTTP error: {ex.Message}");
            }
            catch (TaskCanceledException)
            {
                // Expected during shutdown/cancellation
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AnimationEngine.ApplyKeyframe] Unexpected error: {ex.Message}");
            }
        }
    }

    private async Task RunEventAnimationAsync(
        AnimationDefinition animation,
        List<Guid> lights,
        CancellationToken cancellationToken)
    {
        if (animation.EventPattern == null)
            return;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Calculate next event interval
                var intervalMs = Random.Shared.NextDouble() *
                    (animation.EventPattern.MaxIntervalSeconds - animation.EventPattern.MinIntervalSeconds) * 1000 +
                    animation.EventPattern.MinIntervalSeconds * 1000;

                await Task.Delay((int)intervalMs, cancellationToken);

                // Check probability
                if (animation.EventPattern.Probability.HasValue &&
                    Random.Shared.NextDouble() > animation.EventPattern.Probability.Value)
                {
                    continue;
                }

                // Select trigger(s)
                var triggers = SelectEventTriggers(animation.EventPattern);

                // Execute triggers
                foreach (var trigger in triggers)
                {
                    var targetLight = SelectRandomLight(lights);
                    _ = ExecuteTriggerAsync(trigger, targetLight, cancellationToken);

                    if (!animation.EventPattern.AllowSimultaneous)
                        break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Animation was stopped
        }
    }

    private List<EventTrigger> SelectEventTriggers(EventPattern pattern)
    {
        var triggers = new List<EventTrigger>();
        var count = 1;

        if (pattern.AllowSimultaneous && pattern.MaxSimultaneous.HasValue)
        {
            count = Random.Shared.Next(1, pattern.MaxSimultaneous.Value + 1);
        }

        // Weighted random selection
        var totalWeight = pattern.Triggers.Sum(t => t.Weight);

        for (int i = 0; i < count && pattern.Triggers.Count > 0; i++)
        {
            var randomValue = Random.Shared.NextDouble() * totalWeight;
            var cumulativeWeight = 0.0;

            foreach (var trigger in pattern.Triggers)
            {
                cumulativeWeight += trigger.Weight;
                if (randomValue <= cumulativeWeight)
                {
                    triggers.Add(trigger);
                    break;
                }
            }
        }

        return triggers;
    }

    private Guid SelectRandomLight(List<Guid> lights)
    {
        return lights[Random.Shared.Next(lights.Count)];
    }

    private async Task ExecuteTriggerAsync(EventTrigger trigger, Guid lightId, CancellationToken cancellationToken)
    {
        try
        {
            foreach (var state in trigger.States)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                // Apply the state
                if (state.IsOn.HasValue)
                {
                    await _bridgeService.SetLightOnAsync(lightId, state.IsOn.Value);
                }

                if (state.Brightness.HasValue)
                {
                    await _bridgeService.SetLightBrightnessAsync(lightId, state.Brightness.Value);
                }

                if (state.Color != null)
                {
                    await _bridgeService.SetLightColorAsync(lightId, state.Color);
                }

                if (state.ColorTemperature.HasValue)
                {
                    await _bridgeService.SetLightTemperatureAsync(lightId, state.ColorTemperature.Value);
                }

                // Wait for the state duration
                await Task.Delay((int)(state.DurationSeconds * 1000), cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Trigger was cancelled
        }
    }

    private async Task RunNativeEffectAsync(
        AnimationDefinition animation,
        List<Guid> lights,
        CancellationToken cancellationToken)
    {
        try
        {
            // For native effects, we just apply them once to all target lights
            // The effect name should be in the animation name or ID
            var effectName = animation.Name.ToLowerInvariant();

            foreach (var lightId in lights)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                await _bridgeService.ApplyEffectAsync(lightId, effectName);
            }

            // Keep the task alive until cancelled
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Animation was stopped
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
        _isDisposed = true;
    }
}
