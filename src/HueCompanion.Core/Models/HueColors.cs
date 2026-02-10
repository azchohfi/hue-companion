namespace HueCompanion.Core.Models;

/// <summary>
/// Common Hue color constants for frequently used colors.
/// </summary>
public static class HueColors
{
    /// <summary>
    /// Warm white (2700K equivalent, cozy indoor lighting).
    /// </summary>
    public static readonly HueColor WarmWhite = HueColor.WarmWhite;

    /// <summary>
    /// Cool white (4000K equivalent, bright daylight).
    /// </summary>
    public static readonly HueColor CoolWhite = new(0.31, 0.32);

    /// <summary>
    /// Soft white (3000K equivalent, neutral comfortable lighting).
    /// </summary>
    public static readonly HueColor SoftWhite = new(0.41, 0.38);

    /// <summary>
    /// Warm orange (candle-like glow).
    /// </summary>
    public static readonly HueColor WarmOrange = new(0.57, 0.41);

    /// <summary>
    /// Pure red.
    /// </summary>
    public static readonly HueColor Red = new(0.68, 0.32);

    /// <summary>
    /// Pure green.
    /// </summary>
    public static readonly HueColor Green = new(0.17, 0.70);

    /// <summary>
    /// Pure blue.
    /// </summary>
    public static readonly HueColor Blue = new(0.15, 0.06);

    /// <summary>
    /// Yellow.
    /// </summary>
    public static readonly HueColor Yellow = new(0.48, 0.50);

    /// <summary>
    /// Purple/Magenta.
    /// </summary>
    public static readonly HueColor Purple = new(0.38, 0.16);

    /// <summary>
    /// Cyan.
    /// </summary>
    public static readonly HueColor Cyan = new(0.15, 0.28);
}
