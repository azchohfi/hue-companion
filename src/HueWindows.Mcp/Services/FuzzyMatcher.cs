using HueWindows.Core.Models;

namespace HueWindows.Mcp.Services;

/// <summary>
/// Fuzzy name matching for lights and rooms.
/// </summary>
public static class FuzzyMatcher
{
    /// <summary>
    /// Find a room by name (case-insensitive, partial match).
    /// </summary>
    public static RoomModel? FindRoom(IEnumerable<RoomModel> rooms, string query)
    {
        var q = query.Trim();

        // Exact match first
        var exact = rooms.FirstOrDefault(r =>
            r.Name.Equals(q, StringComparison.OrdinalIgnoreCase) ||
            r.DisplayName.Equals(q, StringComparison.OrdinalIgnoreCase));
        if (exact != null) return exact;

        // Contains match
        var contains = rooms.Where(r =>
            r.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            r.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        if (contains.Count == 1) return contains[0];

        // Try GUID
        if (Guid.TryParse(q, out var guid))
            return rooms.FirstOrDefault(r => r.Id == guid);

        return contains.FirstOrDefault();
    }

    /// <summary>
    /// Find a light by name or ID (case-insensitive, partial match).
    /// </summary>
    public static (LightModel Light, RoomModel Room)? FindLight(
        IEnumerable<RoomModel> rooms, string query)
    {
        var q = query.Trim();

        // Exact name match
        foreach (var room in rooms)
        {
            var exact = room.Lights.FirstOrDefault(l =>
                l.Name.Equals(q, StringComparison.OrdinalIgnoreCase));
            if (exact != null) return (exact, room);
        }

        // Contains match
        var matches = new List<(LightModel Light, RoomModel Room)>();
        foreach (var room in rooms)
        {
            foreach (var light in room.Lights)
            {
                if (light.Name.Contains(q, StringComparison.OrdinalIgnoreCase))
                    matches.Add((light, room));
            }
        }
        if (matches.Count >= 1) return matches[0];

        // Try GUID
        if (Guid.TryParse(q, out var guid))
        {
            foreach (var room in rooms)
            {
                var light = room.Lights.FirstOrDefault(l => l.Id == guid);
                if (light != null) return (light, room);
            }
        }

        return null;
    }
}
