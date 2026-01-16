# Scene Builder Event Triggers Design

## Overview

Add event-based triggers (lightning flashes, sparkles, candle flicker) to the Scene Builder. Events fire randomly during playback, targeting random lights in the room.

## Design Decisions

- **Event Track approach**: Special track type in timeline for random events
- **Random light targeting**: Each event randomly picks a light to flash
- **Preset library**: Built-in effects (Lightning, Sparkle, Flicker) - no custom editor
- **Simple frequency slider**: "Rare" to "Frequent" instead of min/max interval inputs

## Presets

| Preset | Effect | Rare Interval | Frequent Interval |
|--------|--------|---------------|-------------------|
| Lightning Flash | Bright white, instant on, 100ms hold, 200ms fade | 8-15s | 0.5-2s |
| Sparkle | 100% brightness pulse, 50ms on, 150ms fade | 8-15s | 0.5-2s |
| Candle Flicker | Warm orange, subtle brightness dip, 300ms | 8-15s | 0.5-2s |

## Implementation

### New ViewModel

```csharp
public class EventTrackViewModel : ObservableObject
{
    public string Id { get; set; }
    public string DisplayName { get; set; }
    public EventPreset Preset { get; set; }
    public double Frequency { get; set; }  // 0.0 (rare) to 1.0 (frequent)
}

public enum EventPreset
{
    LightningFlash,
    Sparkle,
    CandleFlicker
}
```

### Frequency Mapping

```
Frequency 0.0-0.33 (Rare):     min 8s,   max 15s
Frequency 0.33-0.66 (Medium):  min 3s,   max 6s
Frequency 0.66-1.0 (Frequent): min 0.5s, max 2s
```

### UI Changes

1. Add `EventTracks` collection to `SceneBuilderViewModel`
2. Add "Add Event Track" button to toolbar with preset picker flyout
3. Render event tracks below light tracks (different visual style)
4. Reuse side panel for event track properties (preset dropdown + frequency slider)
5. During playback, show visual pulses on track when events trigger

### Save/Load

`EventTrackViewModel` converts to `AnimationDefinition` with:
- `Type = AnimationType.Event`
- `LightAssignment = LightAssignment.Random`
- `EventPattern` populated with preset trigger states and calculated intervals

## Files to Modify

- `SceneBuilderViewModel.cs` - Add EventTracks collection, preset definitions
- `SceneBuilderPage.xaml` - Add event track button, event track template
- `SceneBuilderPage.xaml.cs` - Render event tracks, handle event track selection
