using HueCompanion.Core.Models;

namespace HueCompanion.Helpers;

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

/// <summary>
/// Navigation parameter for navigating to ScenesPage with room context.
/// When provided, ScenesPage pre-selects the room and shows "Add to Room" flow.
/// </summary>
public record ScenesNavigationParams(Guid RoomId, string RoomName, LightGroupType GroupType);
