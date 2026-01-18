# HueWindows Scene Generator

Generate animated scene JSON files for the HueWindows application from natural language descriptions.

## Your Task

Given a description of a lighting scene, generate a valid JSON file that conforms to the AnimatedSceneModel schema. The scene should be creative, visually appealing, and technically correct.

---

## Schema Reference

### AnimatedSceneModel (Root Object)

```typescript
{
  "id": string,           // Unique identifier (lowercase_snake_case)
  "name": string,         // Display name
  "description": string,  // Brief description of the scene effect
  "category": string,     // Category: "Nature", "Ambient", "Party", "Seasonal", "Mood", "Productivity"
  "previewColor": HueColor,      // Representative color for UI preview
  "paletteColors": HueColor[],   // 2-4 colors that represent the scene palette
  "defaultTargeting": "room" | "zone" | "lights",  // How scene targets lights
  "targetId": string?,           // Optional: specific room/zone ID
  "targetLights": string[]?,     // Optional: specific light IDs
  "animations": AnimationDefinition[],  // One or more animations
  "author": string,       // "HueWindows" for built-in scenes
  "version": string       // Semantic version, e.g., "1.0"
}
```

### HueColor

Colors use CIE xy color space coordinates. Reference values:

| Color | x | y |
|-------|-----|-----|
| Red | 0.67 | 0.32 |
| Orange | 0.60 | 0.38 |
| Yellow | 0.50 | 0.45 |
| Warm White | 0.45 | 0.41 |
| Green | 0.30 | 0.60 |
| Cyan | 0.20 | 0.35 |
| Blue | 0.15 | 0.10 |
| Purple | 0.25 | 0.10 |
| Pink | 0.45 | 0.22 |
| White | 0.31 | 0.32 |
| Cool White | 0.28 | 0.29 |

```typescript
{
  "x": number,  // 0.0 to 1.0
  "y": number   // 0.0 to 1.0
}
```

### AnimationDefinition

```typescript
{
  "id": string,           // Unique identifier within scene
  "name": string,         // Display name or native effect name
  "type": "keyframe" | "event" | "nativeEffect",
  "lightAssignment": "all" | "subset" | "random" | "alternating",
  "targetLightIndices": number[]?,  // For "subset" assignment
  "randomPercentage": number?,      // For "random" assignment (0.0-1.0)
  "keyframes": AnimationKeyframe[], // For "keyframe" type
  "eventPattern": EventPattern?,    // For "event" type
  "durationSeconds": number,        // Total duration (0 for infinite/events)
  "repeatMode": "once" | "loop" | "pingPong",
  "priority": number,               // Higher = plays on top (1-10)
  "effectSpeed": number?,           // For nativeEffect (0.0-1.0)
  "effectBrightness": number?       // For nativeEffect (0.0-1.0)
}
```

### AnimationKeyframe

```typescript
{
  "timeSeconds": number,           // Time position in animation
  "brightness": number?,           // 0.0 to 1.0
  "color": HueColor?,              // Target color
  "colorTemperature": number?,     // Mirek value (153-500, lower=cooler)
  "isOn": boolean?,                // Light on/off state
  "transitionStyle": "linear" | "easeIn" | "easeOut" | "easeInOut" | "instant"
}
```

### EventPattern (for random effects)

```typescript
{
  "triggers": EventTrigger[],      // Possible events to fire
  "minIntervalSeconds": number,    // Minimum time between events
  "maxIntervalSeconds": number,    // Maximum time between events
  "probability": number?,          // 0.0-1.0, chance event fires
  "allowSimultaneous": boolean?,   // Multiple lights at once?
  "maxSimultaneous": number?       // Max simultaneous triggers
}
```

### EventTrigger

```typescript
{
  "states": EventTriggerState[],   // Sequence of state changes
  "weight": number                 // Relative probability (higher = more likely)
}
```

### EventTriggerState

```typescript
{
  "durationSeconds": number,       // How long this state lasts
  "brightness": number?,           // 0.0 to 1.0
  "color": HueColor?,
  "colorTemperature": number?,
  "isOn": boolean?,
  "transitionStyle": "linear" | "easeIn" | "easeOut" | "easeInOut" | "instant"
}
```

---

## Animation Types

### 1. Keyframe Animations
Smooth transitions between defined states over time. Best for:
- Gradual color flows (aurora, sunset)
- Breathing/pulsing effects
- Ambient mood lighting

**Requirements:**
- At least 2 keyframes
- Keyframes must be in ascending time order
- Last keyframe time should match or be less than `durationSeconds`
- For seamless loops, first and last keyframe should have similar values

### 2. Event Animations
Random triggers that fire at intervals. Best for:
- Lightning flashes
- Sparkles/twinkles
- Candle flickers
- Fireflies

**Requirements:**
- At least 1 trigger with at least 1 state
- `minIntervalSeconds` must be >= 0
- `maxIntervalSeconds` must be >= `minIntervalSeconds`
- Use `instant` transitions for sudden effects
- Use `easeOut` to fade back to ambient

### 3. Native Effects
Built-in Hue effects. Use `name` field for effect type:
- `"fire"` - Flickering fire effect
- `"candle"` - Candle simulation
- `"sparkle"` - Twinkling sparkle

**Requirements:**
- `keyframes` should be empty array `[]`
- `durationSeconds` should be `0`

---

## Best Practices

1. **Color Palettes**: Keep palette colors visually cohesive. Use 2-4 colors that work well together.

2. **Timing**:
   - Ambient/relaxing: 15-30 second cycles with `easeInOut` transitions
   - Energetic/party: 3-10 second cycles with faster transitions
   - Events: Short durations (0.05-0.5s) for flashes

3. **Brightness**:
   - Ambient scenes: 0.2-0.6 brightness
   - Dramatic contrast: Mix 0.1-0.2 with 0.8-1.0
   - Never leave lights at 0 brightness (use `isOn: false` instead)

4. **Layering**: Combine keyframe (ambient) with event (accents) animations using priority:
   - Priority 1: Base ambient animation
   - Priority 2+: Event overlays

5. **Light Assignment**:
   - `all`: Every light follows the same pattern
   - `random` with `randomPercentage`: Good for events (e.g., 30% of lights flash)
   - `alternating`: Creates wave/chase effects

---

## Examples

### Example 1: Simple Keyframe Animation (Northern Lights)

```json
{
  "id": "northern_lights",
  "name": "Northern Lights",
  "description": "Ethereal aurora borealis with flowing green and blue colors",
  "category": "Nature",
  "previewColor": { "x": 0.25, "y": 0.55 },
  "paletteColors": [
    { "x": 0.25, "y": 0.55 },
    { "x": 0.20, "y": 0.40 },
    { "x": 0.28, "y": 0.50 },
    { "x": 0.22, "y": 0.45 }
  ],
  "defaultTargeting": "room",
  "animations": [
    {
      "id": "aurora_flow",
      "name": "Aurora Flow",
      "type": "keyframe",
      "lightAssignment": "all",
      "keyframes": [
        { "timeSeconds": 0, "brightness": 0.30, "color": { "x": 0.25, "y": 0.55 }, "isOn": true, "transitionStyle": "easeInOut" },
        { "timeSeconds": 5, "brightness": 0.50, "color": { "x": 0.20, "y": 0.40 }, "isOn": true, "transitionStyle": "easeInOut" },
        { "timeSeconds": 10, "brightness": 0.45, "color": { "x": 0.28, "y": 0.50 }, "isOn": true, "transitionStyle": "easeInOut" },
        { "timeSeconds": 15, "brightness": 0.55, "color": { "x": 0.22, "y": 0.45 }, "isOn": true, "transitionStyle": "easeInOut" },
        { "timeSeconds": 20, "brightness": 0.40, "color": { "x": 0.26, "y": 0.52 }, "isOn": true, "transitionStyle": "easeInOut" },
        { "timeSeconds": 25, "brightness": 0.30, "color": { "x": 0.25, "y": 0.55 }, "isOn": true, "transitionStyle": "easeInOut" }
      ],
      "durationSeconds": 25,
      "repeatMode": "loop",
      "priority": 1
    }
  ],
  "author": "HueWindows",
  "version": "1.0"
}
```

### Example 2: Keyframe + Event Animation (Thunderstorm)

```json
{
  "id": "thunderstorm",
  "name": "Thunderstorm",
  "description": "Dramatic thunderstorm with random lightning flashes",
  "category": "Nature",
  "previewColor": { "x": 0.28, "y": 0.29 },
  "paletteColors": [
    { "x": 0.28, "y": 0.29 },
    { "x": 0.31, "y": 0.32 },
    { "x": 0.33, "y": 0.33 }
  ],
  "defaultTargeting": "room",
  "animations": [
    {
      "id": "ambient_storm",
      "name": "Storm Ambient",
      "type": "keyframe",
      "lightAssignment": "all",
      "keyframes": [
        { "timeSeconds": 0, "brightness": 0.15, "color": { "x": 0.28, "y": 0.29 }, "isOn": true, "transitionStyle": "linear" },
        { "timeSeconds": 5, "brightness": 0.18, "color": { "x": 0.30, "y": 0.31 }, "isOn": true, "transitionStyle": "easeInOut" },
        { "timeSeconds": 10, "brightness": 0.15, "color": { "x": 0.28, "y": 0.29 }, "isOn": true, "transitionStyle": "easeInOut" }
      ],
      "durationSeconds": 10,
      "repeatMode": "loop",
      "priority": 1
    },
    {
      "id": "lightning_flashes",
      "name": "Lightning",
      "type": "event",
      "lightAssignment": "random",
      "randomPercentage": 0.3,
      "eventPattern": {
        "triggers": [
          {
            "weight": 3.0,
            "states": [
              { "durationSeconds": 0.1, "brightness": 1.0, "color": { "x": 0.31, "y": 0.32 }, "isOn": true, "transitionStyle": "instant" },
              { "durationSeconds": 0.05, "brightness": 0.1, "isOn": true, "transitionStyle": "instant" },
              { "durationSeconds": 0.08, "brightness": 0.95, "color": { "x": 0.31, "y": 0.32 }, "isOn": true, "transitionStyle": "instant" },
              { "durationSeconds": 0.5, "brightness": 0.15, "color": { "x": 0.28, "y": 0.29 }, "isOn": true, "transitionStyle": "easeOut" }
            ]
          },
          {
            "weight": 1.0,
            "states": [
              { "durationSeconds": 0.15, "brightness": 0.85, "color": { "x": 0.31, "y": 0.32 }, "isOn": true, "transitionStyle": "instant" },
              { "durationSeconds": 0.6, "brightness": 0.15, "color": { "x": 0.28, "y": 0.29 }, "isOn": true, "transitionStyle": "easeOut" }
            ]
          }
        ],
        "minIntervalSeconds": 3.0,
        "maxIntervalSeconds": 12.0,
        "probability": 0.8,
        "allowSimultaneous": true,
        "maxSimultaneous": 2
      },
      "durationSeconds": 0,
      "repeatMode": "loop",
      "priority": 2
    }
  ],
  "author": "HueWindows",
  "version": "1.0"
}
```

### Example 3: Native Effect (Cozy Fire)

```json
{
  "id": "cozy_fire",
  "name": "Cozy Fire",
  "description": "Warm flickering fireplace ambiance using native fire effect",
  "category": "Ambient",
  "previewColor": { "x": 0.6, "y": 0.38 },
  "paletteColors": [
    { "x": 0.65, "y": 0.33 },
    { "x": 0.58, "y": 0.39 },
    { "x": 0.55, "y": 0.42 }
  ],
  "defaultTargeting": "room",
  "animations": [
    {
      "id": "fire_effect",
      "name": "fire",
      "type": "nativeEffect",
      "lightAssignment": "all",
      "keyframes": [],
      "durationSeconds": 0,
      "repeatMode": "loop",
      "priority": 1
    }
  ],
  "author": "HueWindows",
  "version": "1.0"
}
```

---

## Output Instructions

1. Output ONLY valid JSON - no markdown code blocks, no explanations
2. Use lowercase_snake_case for `id` fields
3. Ensure all required fields are present
4. Validate that keyframes are in ascending time order
5. Ensure colors are within valid CIE xy ranges (0.0-1.0)
6. Set `author` to "HueWindows" and `version` to "1.0"

---

## Scene Request

Please generate a scene based on the following description:

[INSERT YOUR SCENE DESCRIPTION HERE]
