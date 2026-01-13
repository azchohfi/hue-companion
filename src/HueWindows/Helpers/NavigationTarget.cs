using HueWindows.Core.Models;
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
    /// Resolves command-line arguments to a navigation target.
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
}
