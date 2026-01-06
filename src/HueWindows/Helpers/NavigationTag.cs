using HueWindows.Core.Models;

namespace HueWindows.Helpers;

/// <summary>
/// Navigation parameter for room/zone navigation items.
/// </summary>
public record NavigationTag(LightGroupType Type, Guid Id);
