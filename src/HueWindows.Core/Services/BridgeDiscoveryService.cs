using HueApi.BridgeLocator;
using HueWindows.Core.Services.Interfaces;

namespace HueWindows.Core.Services;

/// <summary>
/// Service for discovering Hue bridges on the local network and registering with them.
/// </summary>
public class BridgeDiscoveryService : IBridgeDiscoveryService
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<DiscoveredBridge>> DiscoverBridgesAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var results = new List<DiscoveredBridge>();
        var foundBridges = new HashSet<string>();

        try
        {
            // Use HTTP-based discovery (discovery.meethue.com)
            var httpLocator = new HttpBridgeLocator();
            var httpBridges = await httpLocator.LocateBridgesAsync(timeout);

            foreach (var bridge in httpBridges)
            {
                if (foundBridges.Add(bridge.BridgeId))
                {
                    results.Add(new DiscoveredBridge(bridge.BridgeId, bridge.IpAddress));
                }
            }
        }
        catch
        {
            // HTTP discovery failed, continue with other methods
        }

        try
        {
            // Also try mDNS discovery for local-only networks
            var mdnsLocator = new MdnsBridgeLocator();
            var mdnsBridges = await mdnsLocator.LocateBridgesAsync(timeout);

            foreach (var bridge in mdnsBridges)
            {
                if (foundBridges.Add(bridge.BridgeId))
                {
                    results.Add(new DiscoveredBridge(bridge.BridgeId, bridge.IpAddress));
                }
            }
        }
        catch
        {
            // mDNS discovery failed
        }

        return results;
    }

    /// <inheritdoc/>
    public async Task<BridgeRegistrationResult> RegisterAsync(
        string ipAddress,
        string appName,
        string deviceName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Attempt to register with the bridge
            // This will throw if the link button hasn't been pressed
            var result = await HueApi.LocalHueApi.RegisterAsync(
                ipAddress,
                appName,
                deviceName);

            if (!string.IsNullOrEmpty(result?.Username))
            {
                return new BridgeRegistrationResult(true, result.Username, null);
            }

            return new BridgeRegistrationResult(false, null, "Registration failed - no username returned");
        }
        catch (HueApi.Models.Exceptions.LinkButtonNotPressedException)
        {
            return new BridgeRegistrationResult(false, null, "Please press the link button on the bridge");
        }
        catch (Exception ex)
        {
            return new BridgeRegistrationResult(false, null, ex.Message);
        }
    }
}
