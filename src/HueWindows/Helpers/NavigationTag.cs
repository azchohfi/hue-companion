using HueWindows.Core.Models;

namespace HueWindows.Helpers;

/// <summary>
/// Navigation parameter for room/zone navigation items.
/// </summary>
public record NavigationTag(LightGroupType Type, Guid Id);

/// <summary>
/// Extended navigation parameter that includes initial visual state.
/// Used to preserve color continuity during page transitions.
/// </summary>
public record RoomNavigationParams(
    LightGroupType Type,
    Guid Id,
    bool IsOn,
    List<(byte R, byte G, byte B)> InitialColors);
