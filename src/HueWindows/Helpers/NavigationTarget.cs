using HueWindows.Core.Models;
using HueWindows.Core.Services.Interfaces;
using HueWindows.Views;

namespace HueWindows.Helpers;

/// <summary>
/// Represents a navigation target resolved from command-line arguments.
/// </summary>
public record NavigationTarget
{
    /// <summary>
    /// The page type to navigate to.
    /// </summary>
    public required Type PageType { get; init; }

    /// <summary>
    /// Optional parameter to pass to the page.
    /// </summary>
    public object? Parameter { get; init; }

    /// <summary>
    /// Resolves command-line arguments to a navigation target (synchronous, for pages without ID requirements).
    /// </summary>
    /// <param name="args">Parsed command-line arguments.</param>
    /// <returns>Navigation target, or null if no page was specified.</returns>
    public static NavigationTarget? FromCommandLineArgs(CommandLineArgs args)
    {
        if (args.Page == null) return null;

        return args.Page.ToLowerInvariant() switch
        {
            "dashboard" => new NavigationTarget { PageType = typeof(DashboardPage) },
            "mydashboard" => new NavigationTarget { PageType = typeof(MyDashboardPage) },
            "rooms" => new NavigationTarget { PageType = typeof(RoomsPage) },
            "zones" => new NavigationTarget { PageType = typeof(ZonesPage) },
            "room" => new NavigationTarget
            {
                PageType = typeof(RoomDetailPage),
                Parameter = new NavigationTag(LightGroupType.Room, args.Id!.Value)
            },
            "zone" => new NavigationTarget
            {
                PageType = typeof(RoomDetailPage),
                Parameter = new NavigationTag(LightGroupType.Zone, args.Id!.Value)
            },
            "light" => new NavigationTarget
            {
                PageType = typeof(LightDetailPage),
                Parameter = args.Id!.Value
            },
            "settings" => new NavigationTarget { PageType = typeof(SettingsPage) },
            "setup" => new NavigationTarget { PageType = typeof(SetupPage) },
            _ => null
        };
    }

    /// <summary>
    /// Resolves command-line arguments to a navigation target, looking up names via bridge service.
    /// </summary>
    /// <param name="args">Parsed command-line arguments.</param>
    /// <param name="bridgeService">Bridge service for name resolution.</param>
    /// <returns>Navigation target, or null if resolution failed.</returns>
    public static async Task<NavigationTarget?> ResolveAsync(CommandLineArgs args, IHueBridgeService bridgeService)
    {
        if (args.Page == null) return null;

        var page = args.Page.ToLowerInvariant();

        // For pages that don't require ID, use sync method
        if (!RequiresId(page))
        {
            return FromCommandLineArgs(args);
        }

        // Resolve ID from name if provided
        Guid? resolvedId = args.Id;

        if (!resolvedId.HasValue && !string.IsNullOrEmpty(args.Name))
        {
            resolvedId = await ResolveNameToIdAsync(page, args.Name, bridgeService);
            if (!resolvedId.HasValue)
            {
                System.Diagnostics.Debug.WriteLine($"Could not resolve name '{args.Name}' for page '{page}'");
                return null;
            }
        }

        if (!resolvedId.HasValue) return null;

        return page switch
        {
            "room" => new NavigationTarget
            {
                PageType = typeof(RoomDetailPage),
                Parameter = new NavigationTag(LightGroupType.Room, resolvedId.Value)
            },
            "zone" => new NavigationTarget
            {
                PageType = typeof(RoomDetailPage),
                Parameter = new NavigationTag(LightGroupType.Zone, resolvedId.Value)
            },
            "light" => new NavigationTarget
            {
                PageType = typeof(LightDetailPage),
                Parameter = resolvedId.Value
            },
            _ => null
        };
    }

    private static bool RequiresId(string page) =>
        page is "room" or "zone" or "light";

    private static async Task<Guid?> ResolveNameToIdAsync(string page, string name, IHueBridgeService bridgeService)
    {
        var normalizedName = NormalizeName(name);

        return page switch
        {
            "room" => await FindRoomByNameAsync(normalizedName, bridgeService),
            "zone" => await FindZoneByNameAsync(normalizedName, bridgeService),
            "light" => await FindLightByNameAsync(normalizedName, bridgeService),
            _ => null
        };
    }

    private static string NormalizeName(string name) =>
        name.ToLowerInvariant().Replace("-", " ").Replace("_", " ").Trim();

    private static async Task<Guid?> FindRoomByNameAsync(string name, IHueBridgeService bridgeService)
    {
        var result = await bridgeService.GetRoomsAsync();
        if (!result.IsSuccess) return null;

        var room = result.Value.FirstOrDefault(r =>
            NormalizeName(r.Name ?? "") == name ||
            (r.Name ?? "").Equals(name, StringComparison.OrdinalIgnoreCase));
        return room?.Id;
    }

    private static async Task<Guid?> FindZoneByNameAsync(string name, IHueBridgeService bridgeService)
    {
        var result = await bridgeService.GetZonesAsync();
        if (!result.IsSuccess) return null;

        var zone = result.Value.FirstOrDefault(z =>
            NormalizeName(z.Name ?? "") == name ||
            (z.Name ?? "").Equals(name, StringComparison.OrdinalIgnoreCase));
        return zone?.Id;
    }

    private static async Task<Guid?> FindLightByNameAsync(string name, IHueBridgeService bridgeService)
    {
        // Get all lights from all rooms
        var result = await bridgeService.GetRoomsAsync();
        if (!result.IsSuccess) return null;

        foreach (var room in result.Value)
        {
            var light = room.Lights.FirstOrDefault(l =>
                NormalizeName(l.Name ?? "") == name ||
                (l.Name ?? "").Equals(name, StringComparison.OrdinalIgnoreCase));
            if (light != null) return light.Id;
        }
        return null;
    }
}
