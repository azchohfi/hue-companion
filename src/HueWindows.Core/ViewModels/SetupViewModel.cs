using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HueWindows.Core.Models;
using HueWindows.Core.Services.Interfaces;

namespace HueWindows.Core.ViewModels;

/// <summary>
/// ViewModel for the bridge setup/pairing page.
/// </summary>
public partial class SetupViewModel : ObservableObject
{
    private readonly IBridgeDiscoveryService _discoveryService;
    private readonly ISettingsService _settingsService;
    private readonly IMultiBridgeService _multiBridgeService;
    private CancellationTokenSource? _registrationCts;

    [ObservableProperty]
    private bool _isDiscovering;

    [ObservableProperty]
    private bool _isRegistering;

    [ObservableProperty]
    private string _statusMessage = "Search for Hue bridges on your network";

    [ObservableProperty]
    private ObservableCollection<DiscoveredBridge> _discoveredBridges = new();

    [ObservableProperty]
    private DiscoveredBridge? _selectedBridge;

    [ObservableProperty]
    private bool _showLinkButtonPrompt;

    [ObservableProperty]
    private int _linkButtonCountdown = 30;

    [ObservableProperty]
    private bool _registrationSuccess;

    /// <summary>
    /// Event raised when setup is complete and navigation should occur.
    /// </summary>
    public event EventHandler? SetupCompleted;

    public SetupViewModel(
        IBridgeDiscoveryService discoveryService,
        ISettingsService settingsService,
        IMultiBridgeService multiBridgeService)
    {
        _discoveryService = discoveryService;
        _settingsService = settingsService;
        _multiBridgeService = multiBridgeService;
    }

    [RelayCommand]
    private async Task DiscoverBridgesAsync()
    {
        IsDiscovering = true;
        DiscoveredBridges.Clear();
        StatusMessage = "Searching for Hue bridges on your network...";

        try
        {
            var bridges = await _discoveryService.DiscoverBridgesAsync(TimeSpan.FromSeconds(10));

            foreach (var bridge in bridges)
            {
                DiscoveredBridges.Add(bridge);
            }

            StatusMessage = bridges.Count > 0
                ? $"Found {bridges.Count} bridge(s). Select one to connect."
                : "No bridges found. Make sure your bridge is powered on and connected to the network.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Discovery failed: {ex.Message}";
        }
        finally
        {
            IsDiscovering = false;
        }
    }

    [RelayCommand]
    private void SelectBridge(DiscoveredBridge bridge)
    {
        SelectedBridge = bridge;
        ShowLinkButtonPrompt = true;
        StatusMessage = "Press the link button on your Hue bridge, then click Connect.";
    }

    [RelayCommand]
    private async Task RegisterWithBridgeAsync()
    {
        if (SelectedBridge == null) return;

        IsRegistering = true;
        ShowLinkButtonPrompt = false;
        StatusMessage = "Waiting for link button press...";

        _registrationCts = new CancellationTokenSource();
        var token = _registrationCts.Token;

        // Start countdown
        _ = RunCountdownAsync(token);

        // Retry registration for 30 seconds
        var deadline = DateTime.Now.AddSeconds(30);
        BridgeRegistrationResult? result = null;

        while (DateTime.Now < deadline && !token.IsCancellationRequested)
        {
            result = await _discoveryService.RegisterAsync(
                SelectedBridge.IpAddress,
                "HueWindows",
                Environment.MachineName,
                token);

            if (result.Success)
            {
                break;
            }

            // Wait before retrying
            try
            {
                await Task.Delay(2000, token);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }

        if (result?.Success == true && result.AppKey != null)
        {
            // Create bridge model
            var bridge = new BridgeModel
            {
                BridgeId = SelectedBridge.BridgeId,
                IpAddress = SelectedBridge.IpAddress,
                AppKey = result.AppKey,
                LastConnected = DateTime.UtcNow
            };

            // Add bridge to multi-bridge service
            var addResult = await _multiBridgeService.AddBridgeAsync(bridge);

            if (addResult.IsSuccess)
            {
                StatusMessage = "Connected successfully!";
                RegistrationSuccess = true;

                // Wait briefly then navigate
                await Task.Delay(1500);
                SetupCompleted?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                StatusMessage = addResult.Error ?? "Failed to connect to bridge after registration.";
                ShowLinkButtonPrompt = true;
            }
        }
        else
        {
            StatusMessage = result?.Error ?? "Registration timed out. Please try again.";
            ShowLinkButtonPrompt = true;
        }

        IsRegistering = false;
        _registrationCts?.Dispose();
        _registrationCts = null;
    }

    [RelayCommand]
    private void CancelRegistration()
    {
        _registrationCts?.Cancel();
        ShowLinkButtonPrompt = true;
        IsRegistering = false;
        StatusMessage = "Registration cancelled. Press the link button and try again.";
    }

    private async Task RunCountdownAsync(CancellationToken token)
    {
        LinkButtonCountdown = 30;

        while (LinkButtonCountdown > 0 && !token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(1000, token);
                LinkButtonCountdown--;
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }
}
