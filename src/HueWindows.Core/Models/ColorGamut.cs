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

    /// <summary>
    /// Calculates the maximum achievable saturation for a given hue angle.
    /// Returns a value from 0.0 to 1.0 representing how far from white (center)
    /// the color can go while staying inside the gamut.
    /// </summary>
    /// <param name="hueDegrees">Hue angle in degrees (0-360).</param>
    /// <returns>Maximum saturation (0.0-1.0) achievable for this hue.</returns>
    public double GetMaxSaturationForHue(double hueDegrees)
    {
        // Convert hue to radians
        double hueRadians = hueDegrees * Math.PI / 180.0;

        // Calculate direction vector from white point toward edge
        // In HSV, hue 0 is red (positive x direction in standard orientation)
        // But CIE xy has different orientation, so we need to convert
        // Using HSV->RGB->XY to get the edge direction for this hue
        var edgeXy = HsvToXyDirection(hueDegrees);

        double dx = edgeXy.X - WhitePoint.X;
        double dy = edgeXy.Y - WhitePoint.Y;

        // Normalize the direction
        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 0.0001)
            return 1.0; // Edge case: at white point

        dx /= len;
        dy /= len;

        // Find where ray from white point intersects gamut boundary
        double maxT = FindGamutIntersection(WhitePoint.X, WhitePoint.Y, dx, dy);

        // The saturation is the ratio of gamut intersection distance to full saturation distance
        // Full saturation distance is the distance from white to the HSV edge (len)
        double saturation = maxT / len;

        return Math.Clamp(saturation, 0.0, 1.0);
    }

    /// <summary>
    /// Converts HSV hue (at full saturation and value) to CIE xy coordinates.
    /// Used to determine the edge direction for saturation limiting.
    /// </summary>
    private static (double X, double Y) HsvToXyDirection(double hueDegrees)
    {
        // HSV to RGB at saturation=1, value=1
        double c = 1.0;
        double x = c * (1 - Math.Abs((hueDegrees / 60.0) % 2 - 1));

        double r, g, b;
        if (hueDegrees < 60) { r = c; g = x; b = 0; }
        else if (hueDegrees < 120) { r = x; g = c; b = 0; }
        else if (hueDegrees < 180) { r = 0; g = c; b = x; }
        else if (hueDegrees < 240) { r = 0; g = x; b = c; }
        else if (hueDegrees < 300) { r = x; g = 0; b = c; }
        else { r = c; g = 0; b = x; }

        // RGB to XYZ (sRGB D65) - simplified, no gamma for direction calc
        double X = r * 0.4124564 + g * 0.3575761 + b * 0.1804375;
        double Y = r * 0.2126729 + g * 0.7151522 + b * 0.0721750;
        double Z = r * 0.0193339 + g * 0.1191920 + b * 0.9503041;

        // XYZ to xy
        double sum = X + Y + Z;
        if (sum == 0) return WhitePoint;

        return (X / sum, Y / sum);
    }

    /// <summary>
    /// Finds the intersection distance of a ray with the gamut triangle boundary.
    /// </summary>
    private double FindGamutIntersection(double ox, double oy, double dx, double dy)
    {
        // Check intersection with each edge, return minimum positive t
        double minT = double.MaxValue;

        // Red-Green edge
        double t = RaySegmentIntersection(ox, oy, dx, dy, Red.X, Red.Y, Green.X, Green.Y);
        if (t > 0 && t < minT) minT = t;

        // Green-Blue edge
        t = RaySegmentIntersection(ox, oy, dx, dy, Green.X, Green.Y, Blue.X, Blue.Y);
        if (t > 0 && t < minT) minT = t;

        // Blue-Red edge
        t = RaySegmentIntersection(ox, oy, dx, dy, Blue.X, Blue.Y, Red.X, Red.Y);
        if (t > 0 && t < minT) minT = t;

        return minT == double.MaxValue ? 1.0 : minT;
    }

    /// <summary>
    /// Calculates ray-segment intersection. Returns t parameter of ray, or -1 if no intersection.
    /// Ray: origin + t * direction
    /// Segment: from (ax,ay) to (bx,by)
    /// </summary>
    private static double RaySegmentIntersection(
        double ox, double oy, double dx, double dy,
        double ax, double ay, double bx, double by)
    {
        double sx = bx - ax;
        double sy = by - ay;

        double denom = dx * sy - dy * sx;
        if (Math.Abs(denom) < 1e-10)
            return -1; // Parallel

        double t = ((ax - ox) * sy - (ay - oy) * sx) / denom;
        double u = ((ax - ox) * dy - (ay - oy) * dx) / denom;

        // t > 0 means intersection is ahead on ray
        // 0 <= u <= 1 means intersection is on segment
        if (t > 0 && u >= 0 && u <= 1)
            return t;

        return -1;
    }
}
