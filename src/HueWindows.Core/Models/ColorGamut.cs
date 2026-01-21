namespace HueWindows.Core.Models;

/// <summary>
/// Represents a Philips Hue color gamut triangle in CIE xy color space.
/// Different Hue light generations support different gamuts (A, B, C).
/// </summary>
public class ColorGamut
{
    /// <summary>Red vertex of the gamut triangle in CIE xy coordinates.</summary>
    public (double X, double Y) Red { get; }

    /// <summary>Green vertex of the gamut triangle in CIE xy coordinates.</summary>
    public (double X, double Y) Green { get; }

    /// <summary>Blue vertex of the gamut triangle in CIE xy coordinates.</summary>
    public (double X, double Y) Blue { get; }

    /// <summary>D65 white point (center of saturation).</summary>
    public static readonly (double X, double Y) WhitePoint = (0.3127, 0.3290);

    /// <summary>
    /// Gamut A: Legacy lights (LivingColors Iris, Bloom, Aura, original LightStrips).
    /// Wider green range, weaker reds.
    /// </summary>
    public static readonly ColorGamut GamutA = new(
        (0.704, 0.296), (0.2151, 0.7106), (0.138, 0.08));

    /// <summary>
    /// Gamut B: Gen 1-2 Hue bulbs (LCT001, LCT002, LCT003).
    /// Good whites, narrower green range.
    /// </summary>
    public static readonly ColorGamut GamutB = new(
        (0.675, 0.322), (0.4091, 0.518), (0.167, 0.04));

    /// <summary>
    /// Gamut C: Current generation (A19 Gen 3+, BR30, Hue Go, LightStrips Plus, Play bars).
    /// Best overall coverage, improved greens and blues.
    /// </summary>
    public static readonly ColorGamut GamutC = new(
        (0.6915, 0.3083), (0.17, 0.7), (0.1532, 0.0475));

    /// <summary>
    /// Creates a ColorGamut from red, green, and blue vertex coordinates.
    /// </summary>
    public ColorGamut((double X, double Y) red, (double X, double Y) green, (double X, double Y) blue)
    {
        Red = red;
        Green = green;
        Blue = blue;
    }

    /// <summary>
    /// Checks if a point in CIE xy space is inside this gamut triangle.
    /// Uses cross product sign method (barycentric-like).
    /// </summary>
    public bool Contains(double x, double y)
    {
        // Cross product helper
        static double Cross((double x, double y) a, (double x, double y) b)
            => a.x * b.y - a.y * b.x;

        // Vectors from point to each vertex
        var v1 = (Red.X - x, Red.Y - y);
        var v2 = (Green.X - x, Green.Y - y);
        var v3 = (Blue.X - x, Blue.Y - y);

        // Cross products of adjacent vectors
        double c1 = Cross(v1, v2);
        double c2 = Cross(v2, v3);
        double c3 = Cross(v3, v1);

        // All same sign = inside triangle
        return (c1 >= 0 && c2 >= 0 && c3 >= 0) ||
               (c1 <= 0 && c2 <= 0 && c3 <= 0);
    }

    /// <summary>
    /// Projects a point to the nearest location inside the gamut triangle.
    /// Returns the original point if already inside.
    /// </summary>
    public (double X, double Y) Clip(double x, double y)
    {
        if (Contains(x, y))
            return (x, y);

        // Find closest point on each edge
        var closestRG = ClosestPointOnSegment(x, y, Red.X, Red.Y, Green.X, Green.Y);
        var closestGB = ClosestPointOnSegment(x, y, Green.X, Green.Y, Blue.X, Blue.Y);
        var closestBR = ClosestPointOnSegment(x, y, Blue.X, Blue.Y, Red.X, Red.Y);

        // Find minimum distance
        double distRG = DistanceSquared(x, y, closestRG.X, closestRG.Y);
        double distGB = DistanceSquared(x, y, closestGB.X, closestGB.Y);
        double distBR = DistanceSquared(x, y, closestBR.X, closestBR.Y);

        if (distRG <= distGB && distRG <= distBR)
            return closestRG;
        if (distGB <= distBR)
            return closestGB;
        return closestBR;
    }

    /// <summary>
    /// Finds the closest point on a line segment to a given point.
    /// </summary>
    private static (double X, double Y) ClosestPointOnSegment(
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
        double abLenSq = abx * abx + aby * aby;
        if (abLenSq == 0)
            return (ax, ay); // A and B are same point

        double t = (apx * abx + apy * aby) / abLenSq;

        // Clamp t to [0, 1] to stay on segment
        t = Math.Clamp(t, 0.0, 1.0);

        return (ax + t * abx, ay + t * aby);
    }

    private static double DistanceSquared(double x1, double y1, double x2, double y2)
        => (x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1);
}
