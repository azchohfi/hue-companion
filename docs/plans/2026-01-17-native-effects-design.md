# Native Hue Effects Integration

## Overview

Add full support for native Hue bridge effects (fire, candle, sparkle, etc.) with a streamlined UI for quick application and optional saving.

Native effects run on the Hue bridge itself - no app-driven animation loops required. This provides smoother animations with less network traffic.

## User Flow

### Room/Zone Detail Page

1. Native effects appear as cards in the existing Scenes section
2. Cards distinguished by sparkle icon and subtle visual treatment
3. Tap card → flyout opens with Speed/Brightness sliders
4. Adjust parameters → "Apply" to activate immediately
5. Optionally "Save as Scene" to keep the configuration

### Scenes Page

1. New "Hue Effects" section between "My Scenes" and "Preset Scenes"
2. Shows all 10 native effects as cards
3. Shows user-saved effect configurations
4. Same flyout interaction for configuration
5. Saved configurations also appear in "My Scenes" with sparkle icon

## Supported Effects

| Effect | Description |
|--------|-------------|
| fire | Warm flickering flames |
| candle | Soft candle flicker |
| sparkle | Twinkling sparkle |
| glisten | Gentle shimmer |
| opal | Soft opalescent flow |
| prism | Color-shifting prism |
| underwater | Blue-green underwater |
| cosmos | Space/galaxy drift |
| sunbeam | Warm sunlight rays |
| enchant | Magical transitions |

## UI Design

### Effect Card

Cards appear in the Scenes section with visual differentiation:

- Sparkle icon prefix before effect name
- Subtle background treatment (warmer tint or slight glow)
- Same card size/layout as other scene cards for consistency

### Effect Flyout

Opens when user taps an effect card:

```
┌─────────────────────────────────┐
│  ✨ Fire                        │
│  Warm flickering flames         │
├─────────────────────────────────┤
│                                 │
│  Speed                          │
│  ████████░░░░░░░░░░  40%       │
│                                 │
│  Brightness                     │
│  ██████████████░░░░  70%       │
│                                 │
├─────────────────────────────────┤
│  [Save as Scene]    [Apply]     │
└─────────────────────────────────┘
```

**Behavior:**
- Speed slider: 0-100% (maps to 0.0-1.0 API value)
- Brightness slider: 0-100%
- "Apply" → immediately applies effect to room, closes flyout
- "Save as Scene" → prompts for name, saves configuration, then applies

## Technical Implementation

### Model Changes

Extend `AnimationDefinition` with effect parameters:

```csharp
public double? EffectSpeed { get; set; }      // 0.0-1.0
public double? EffectBrightness { get; set; } // 0.0-1.0
```

### Service Changes

**HueBridgeService.cs:**
- Add missing effects to mapping (underwater, cosmos, sunbeam, enchant)
- Extend `ApplyEffectAsync` signature:

```csharp
Task ApplyEffectAsync(Guid lightId, string effect, double? speed = null, double? brightness = null);
```

**AnimationService.cs:**
- Add method to get available native effects:

```csharp
IReadOnlyList<NativeEffectInfo> GetAvailableNativeEffects();
```

### New Components

**NativeEffectInfo.cs** (model):
- Id (string)
- Name (string)
- Description (string)
- DefaultSpeed (double)
- DefaultBrightness (double)

**NativeEffectFlyout.xaml** (reusable control):
- Effect name/description header
- Speed slider
- Brightness slider
- Apply and Save as Scene buttons

**NativeEffectFlyoutViewModel.cs**:
- SelectedEffect property
- Speed property (0-100)
- Brightness property (0-100)
- ApplyCommand
- SaveAsSceneCommand

### View Changes

**RoomDetailPage.xaml:**
- Add native effect cards to Scenes section
- Integrate NativeEffectFlyout control

**ScenesPage.xaml:**
- Add "Hue Effects" section
- Same NativeEffectFlyout control

## Edge Cases

### Stopping Effects

- Applying a different scene stops the current effect
- Applying a different effect replaces the current one
- "Stop" button stops native effects
- Turning room off stops the effect

### Unsupported Lights

- Some older lights don't support native effects
- Apply to supported lights, ignore others (graceful degradation)
- No error shown - matches Hue app behavior

### Saved Configurations

- Stored in `%LOCALAPPDATA%/HueWindows/Scenes/`
- Uses existing `AnimatedSceneModel` JSON format with `AnimationType.NativeEffect`
- Appear in "My Scenes" with sparkle icon
- Can be deleted like any user scene

### Default Values

- Speed: 50%
- Brightness: 100%

## Implementation Tasks

1. Add missing effects to HueBridgeService effect mapping
2. Extend ApplyEffectAsync to accept speed/brightness parameters
3. Create NativeEffectInfo model and effect catalog
4. Create NativeEffectFlyout control and ViewModel
5. Add "Hue Effects" section to ScenesPage
6. Add native effect cards to RoomDetailPage Scenes section
7. Implement Save as Scene flow
8. Add visual treatment for effect cards (sparkle icon, background)
9. Test with real Hue bridge
