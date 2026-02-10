using System.ComponentModel;
using System.Text.Json;
using HueWindows.Core.Services.Interfaces;
using ModelContextProtocol.Server;

namespace HueCompanion.Mcp.Tools;

[McpServerToolType]
public class BridgeTools
{
    private readonly IMultiBridgeService _multiBridge;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    public BridgeTools(IMultiBridgeService multiBridge)
    {
        _multiBridge = multiBridge;
    }

    [McpServerTool(Name = "hue_list_bridges"), Description("List all configured Hue bridges and their connection status.")]
    public string ListBridges()
    {
        var bridges = _multiBridge.ConfiguredBridges.Select(b => new
        {
            b.BridgeId,
            Name = b.DisplayName,
            Ip = b.IpAddress,
            Connected = _multiBridge.ConnectionStatus.TryGetValue(b.BridgeId, out var c) && c,
            b.LastConnected
        });

        return JsonSerializer.Serialize(bridges, JsonOpts);
    }
}
