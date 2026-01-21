# Philips Hue Color Gamut Research

**Date:** 2026-01-21
**Confidence:** HIGH (verified with official Philips SDK documentation and multiple implementations)

## Executive Summary

Philips Hue uses the CIE 1931 xy color space for color representation. Different bulb generations support different color gamuts (A, B, C), each defined as a triangle in xy space. A gamut-aware color picker must:

1. Convert user input (HSV wheel) to CIE xy coordinates
2. Check if the xy point falls within the target bulb's gamut triangle
3. If outside, project the point to the nearest edge of the gamut triangle
4. Send the (potentially clipped) xy coordinates to the Hue API

---

## 1. Hue API Color Model

The Hue CLIP v2 API accepts colors in three formats:

| Parameter | Description | When to Use |
|-----------|-------------|-------------|
| `xy` | CIE 1931 chromaticity coordinates | Full color control |
| `ct` (mirek) | Color temperature (153-500 mirek) | White/ambiance bulbs |
| `hue` + `sat` | Hue (0-65535) + Saturation (0-254) | Legacy, less precise |

**Recommendation:** Use `xy` for all color operations. It provides precise control and is what the bridge uses internally. The existing `HueColor` class already uses xy coordinates correctly.

### API Request Example

```json
{
  "color": {
    "xy": {
      "x": 0.4573,
      "y": 0.4100
    }
  }
}
```

---

## 2. Color Gamut Triangles

Hue lights have three defined gamuts based on LED technology. Each gamut is a triangle in CIE xy space.

### Gamut A (Legacy Lights)
**Products:** LivingColors Iris, Bloom, Aura, Original LightStrips

| Corner | x | y |
|--------|---|---|
| Red | 0.704 | 0.296 |
| Green | 0.2151 | 0.7106 |
| Blue | 0.138 | 0.08 |

**Characteristics:** Wider green range, weaker in reds.

### Gamut B (Gen 1-2 Hue Bulbs)
**Products:** Original Hue bulbs (LCT001, LCT002, LCT003)

| Corner | x | y |
|--------|---|---|
| Red | 0.675 | 0.322 |
| Green | 0.4091 | 0.518 |
| Blue | 0.167 | 0.04 |

**Characteristics:** Good whites, narrower green range.

### Gamut C (Current Generation)
**Products:** A19 Gen 3+, BR30, Hue Go, LightStrips Plus, Play bars, modern bulbs

| Corner | x | y |
|--------|---|---|
| Red | 0.6915 | 0.3083 |
| Green | 0.17 | 0.7 |
| Blue | 0.1532 | 0.0475 |

**Characteristics:** Best overall coverage, improved greens and blues.

### Visual Comparison

```
          y
        0.8 |
            |     * Green (C)
        0.7 |    * Green (A)
            |
        0.6 |
            |            * Green (B)
        0.5 |
            |
        0.4 |                    * Red (A)
            |          D65       * Red (C)
        0.3 |           *        * Red (B)
            |
        0.2 |
            |
        0.1 |  * Blue (A)
            | * Blue (C)
            | * Blue (B)
        0.0 +---------------------------- x
            0.0  0.2  0.4  0.6  0.8
```

### Gamut Detection

The Hue API v2 returns gamut type in the light resource:

```json
{
  "color": {
    "gamut_type": "C",
    "gamut": {
      "red": { "x": 0.6915, "y": 0.3083 },
      "green": { "x": 0.17, "y": 0.7 },
      "blue": { "x": 0.1532, "y": 0.0475 }
    }
  }
}
```

**Recommendation:** Always read the actual gamut from the light resource rather than assuming based on product type. Some lights may have custom gamuts.

---

## 3. Conversion Algorithms

### 3.1 RGB to CIE xy

The existing `HueColor.FromRgb()` implementation is correct. Here's the algorithm:

```csharp
public static HueColor FromRgb(byte r, byte g, byte b)
{
    // 1. Normalize to 0-1 range
    double red = r / 255.0;
    double green = g / 255.0;
    double blue = b / 255.0;

    // 2. Apply gamma correction (sRGB to linear)
    red = (red > 0.04045) ? Math.Pow((red + 0.055) / 1.055, 2.4) : red / 12.92;
    green = (green > 0.04045) ? Math.Pow((green + 0.055) / 1.055, 2.4) : green / 12.92;
    blue = (blue > 0.04045) ? Math.Pow((blue + 0.055) / 1.055, 2.4) : blue / 12.92;

    // 3. Convert to XYZ using sRGB matrix (D65 white point)
    double X = red * 0.4124564 + green * 0.3575761 + blue * 0.1804375;
    double Y = red * 0.2126729 + green * 0.7151522 + blue * 0.0721750;
    double Z = red * 0.0193339 + green * 0.1191920 + blue * 0.9503041;

    // 4. Calculate xy chromaticity
    double sum = X + Y + Z;
    if (sum == 0) return new HueColor(0.3127, 0.3290); // D65 white

    return new HueColor(X / sum, Y / sum);
}
```

### 3.2 HSV to CIE xy

For a color picker, convert HSV to RGB first, then to xy:

```csharp
public static HueColor FromHsv(double hue, double saturation, double value)
{
    // hue: 0-360, saturation: 0-1, value: 0-1

    double c = value * saturation;
    double x = c * (1 - Math.Abs((hue / 60.0) % 2 - 1));
    double m = value - c;

    double r, g, b;

    if (hue < 60)       { r = c; g = x; b = 0; }
    else if (hue < 120) { r = x; g = c; b = 0; }
    else if (hue < 180) { r = 0; g = c; b = x; }
    else if (hue < 240) { r = 0; g = x; b = c; }
    else if (hue < 300) { r = x; g = 0; b = c; }
    else                { r = c; g = 0; b = x; }

    return FromRgb(
        (byte)((r + m) * 255),
        (byte)((g + m) * 255),
        (byte)((b + m) * 255)
    );
}
```

### 3.3 CIE xy to RGB

The existing `HueColor.ToRgb()` implementation is correct. Key points:
- Handle out-of-gamut colors by clamping negative values
- Scale all channels proportionally if any exceeds 1.0 (preserves hue)
- Apply inverse gamma correction

---

## 4. Gamut Clipping Algorithm

When a color is outside the bulb's gamut, it must be projected to the nearest point inside the triangle.

### 4.1 Point-in-Triangle Test

Using cross products (sign of area method):

```csharp
public class GamutTriangle
{
    public (double x, double y) Red { get; }
    public (double x, double y) Green { get; }
    public (double x, double y) Blue { get; }

    // Predefined gamuts
    public static readonly GamutTriangle GamutA = new(
        (0.704, 0.296), (0.2151, 0.7106), (0.138, 0.08));
    public static readonly GamutTriangle GamutB = new(
        (0.675, 0.322), (0.4091, 0.518), (0.167, 0.04));
    public static readonly GamutTriangle GamutC = new(
        (0.6915, 0.3083), (0.17, 0.7), (0.1532, 0.0475));

    private double CrossProduct((double x, double y) p1, (double x, double y) p2)
    {
        return p1.x * p2.y - p1.y * p2.x;
    }

    public bool ContainsPoint(double x, double y)
    {
        // Vectors from point to each vertex
        var v1 = (Red.x - x, Red.y - y);
        var v2 = (Green.x - x, Green.y - y);
        var v3 = (Blue.x - x, Blue.y - y);

        // Cross products
        double c1 = CrossProduct(v1, v2);
        double c2 = CrossProduct(v2, v3);
        double c3 = CrossProduct(v3, v1);

        // All same sign = inside triangle
        return (c1 >= 0 && c2 >= 0 && c3 >= 0) ||
               (c1 <= 0 && c2 <= 0 && c3 <= 0);
    }
}
```

### 4.2 Closest Point on Line Segment

```csharp
private (double x, double y) ClosestPointOnSegment(
    double px, double py,
    double ax, double ay,
    double bx, double by)
{
    // Vector from A to B
    double abx = bx - ax;
    double aby = by - ay;

    // Vector from A to P
    double apx = px - ax;
    double apy = py - ay;

    // Project P onto AB, computing parameterized position
    double t = (apx * abx + apy * aby) / (abx * abx + aby * aby);

    // Clamp t to [0, 1] to stay on segment
    t = Math.Clamp(t, 0.0, 1.0);

    // Return closest point
    return (ax + t * abx, ay + t * aby);
}
```

### 4.3 Complete Gamut Clipping

```csharp
public (double x, double y) ClipToGamut(double x, double y)
{
    if (ContainsPoint(x, y))
        return (x, y); // Already in gamut

    // Find closest point on each edge
    var closestRG = ClosestPointOnSegment(x, y, Red.x, Red.y, Green.x, Green.y);
    var closestGB = ClosestPointOnSegment(x, y, Green.x, Green.y, Blue.x, Blue.y);
    var closestBR = ClosestPointOnSegment(x, y, Blue.x, Blue.y, Red.x, Red.y);

    // Calculate distances
    double distRG = Distance(x, y, closestRG.x, closestRG.y);
    double distGB = Distance(x, y, closestGB.x, closestGB.y);
    double distBR = Distance(x, y, closestBR.x, closestBR.y);

    // Return closest
    if (distRG <= distGB && distRG <= distBR) return closestRG;
    if (distGB <= distBR) return closestGB;
    return closestBR;

    double Distance(double x1, double y1, double x2, double y2)
        => Math.Sqrt((x2-x1)*(x2-x1) + (y2-y1)*(y2-y1));
}
```

---

## 5. Color Picker Design Recommendations

### 5.1 Approach 1: Clipped HSV Wheel (Recommended)

Display a full HSV color wheel but clip colors to gamut when sending to lights.

**Pros:**
- Familiar UX (standard color wheel)
- Users can see what they're selecting
- Visual feedback shows the actual color vs. reproduced color

**Cons:**
- Some areas produce same result (saturated cyan, for example)
- May confuse users when different selections produce same light color

**Implementation:**

```csharp
// In color picker selection handler
void OnColorSelected(double hue, double saturation)
{
    // Convert HSV to xy
    var xyColor = HueColor.FromHsv(hue, saturation, 1.0);

    // Get light's gamut (read from API or default to C)
    var gamut = GetLightGamut(currentLight);

    // Clip to gamut
    var (clippedX, clippedY) = gamut.ClipToGamut(xyColor.X, xyColor.Y);

    // Show preview of clipped color
    var clippedColor = new HueColor(clippedX, clippedY);
    ShowPreviewIndicator(clippedColor);

    // Send to light
    await SetLightColorAsync(lightId, clippedColor);
}
```

### 5.2 Approach 2: Gamut-Shaped Picker

Display only the achievable gamut as the selection area.

**Pros:**
- Every selectable color is achievable
- No confusion about clipping

**Cons:**
- Non-standard UI shape (triangle)
- Changes based on bulb type
- Harder to select specific hues

**Implementation:**
- Render gamut triangle in CIE xy space
- Transform touch/mouse coordinates to xy
- No clipping needed

### 5.3 Approach 3: Adaptive Saturation Limit (Best UX)

Display a hue ring with dynamically limited saturation based on achievable gamut.

**Pros:**
- Intuitive (hue wheel, saturation slider)
- Each hue shows max achievable saturation
- No impossible selections

**Implementation:**

```csharp
// For each hue angle, calculate max achievable saturation
double GetMaxSaturationForHue(double hue, GamutTriangle gamut)
{
    // Start at center (white point), ray-cast toward edge
    // Find intersection with gamut boundary

    double centerX = 0.3127; // D65 white
    double centerY = 0.3290;

    // Direction for this hue (in xy space, not HSV)
    // This requires converting hue to xy direction
    var edgeColor = HueColor.FromHsv(hue, 1.0, 1.0);

    double dx = edgeColor.X - centerX;
    double dy = edgeColor.Y - centerY;

    // Find where this ray intersects gamut edge
    double maxT = FindGamutIntersection(centerX, centerY, dx, dy, gamut);

    // Convert distance to saturation (0-1)
    return Math.Min(1.0, maxT);
}
```

### 5.4 UI Feedback Recommendations

1. **Preview Dot:** Show a small indicator on the wheel showing where the actual reproduced color will be (if different from selection).

2. **Gamut Overlay:** Optionally show the gamut triangle as a faint overlay on the color wheel.

3. **Saturation Dimming:** Dim or gray out areas of the wheel that are outside the gamut.

4. **Color Swatch:** Show side-by-side comparison of "selected" vs. "reproduced" color.

---

## 6. Code Integration for HueColor Class

### Recommended Additions to HueColor.cs

```csharp
/// <summary>
/// Represents a Hue color gamut triangle in CIE xy space.
/// </summary>
public class ColorGamut
{
    public (double X, double Y) Red { get; }
    public (double X, double Y) Green { get; }
    public (double X, double Y) Blue { get; }

    public static readonly ColorGamut GamutA = new(
        (0.704, 0.296), (0.2151, 0.7106), (0.138, 0.08));
    public static readonly ColorGamut GamutB = new(
        (0.675, 0.322), (0.4091, 0.518), (0.167, 0.04));
    public static readonly ColorGamut GamutC = new(
        (0.6915, 0.3083), (0.17, 0.7), (0.1532, 0.0475));

    public ColorGamut((double, double) red, (double, double) green, (double, double) blue)
    {
        Red = red;
        Green = green;
        Blue = blue;
    }

    public bool Contains(double x, double y) { /* point-in-triangle */ }
    public (double X, double Y) Clip(double x, double y) { /* nearest point */ }
}

// Add to HueColor class:
public static HueColor FromHsv(double hue, double saturation, double value) { /* ... */ }
public HueColor ClipToGamut(ColorGamut gamut)
{
    var (x, y) = gamut.Clip(X, Y);
    return new HueColor(x, y);
}
public bool IsInGamut(ColorGamut gamut) => gamut.Contains(X, Y);
```

---

## 7. Special Cases

### White Point
D65 white: (0.3127, 0.3290) - always in gamut for all bulbs.

### Color Temperature Integration
When showing a color temperature slider alongside the color picker, note that color temperature colors form a curve (Planckian locus) through the gamut, not a straight line.

### Brightness Independence
CIE xy represents chromaticity only. Brightness is handled separately via the `dimming` API. The gamut does not change with brightness.

---

## Sources

- [Philips Hue SDK - RGB to xy Conversion](https://github.com/johnciech/PhilipsHueSDK/blob/master/ApplicationDesignNotes/RGB%20to%20xy%20Color%20conversion.md) (HIGH confidence)
- [hue-python-rgb-converter](https://github.com/benknight/hue-python-rgb-converter) (HIGH confidence)
- [viereck.ch RGB/xy Converter](https://viereck.ch/hue-xy-rgb/) (MEDIUM confidence)
- [Bjorn Ottosson - Gamut Clipping](https://bottosson.github.io/posts/gamutclipping/) (HIGH confidence)
- [Bjorn Ottosson - Okhsv/Okhsl Color Pickers](https://bottosson.github.io/posts/colorpicker/) (HIGH confidence)
- [CIE 1931 Color Space - Wikipedia](https://en.wikipedia.org/wiki/CIE_1931_color_space) (MEDIUM confidence)
- [Barycentric Coordinates](https://en.wikipedia.org/wiki/Barycentric_coordinate_system) (HIGH confidence)
