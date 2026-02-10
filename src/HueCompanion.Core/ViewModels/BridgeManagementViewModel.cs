using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HueCompanion.Core.Models;
using HueCompanion.Core.Services.Interfaces;

namespace HueCompanion.Core.ViewModels;

/// <summary>
/// ViewModel for managing multiple Hue bridges.
/// </summary>
public partial class BridgeManagementViewModel : ObservableObject, IDisposable
{
    private readonly IMultiBridgeService _multiBridgeService;
    private readonly IBridgeDiscoveryService _discoveryService;

    [ObservableProperty]
    private ObservableCollection<BridgeInfoViewModel> _bridges = new();

    [ObservableProperty]
    private ObservableCollection<DiscoveredBridgeViewModel> _discoveredBridges = new();

    [ObservableProperty]
    private bool _isDiscovering;

    [ObservableProperty]
    private bool _hasDiscoveredBridges;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _statusMessage;

    public BridgeManagementViewModel(
        IMultiBridgeService multiBridgeService,
        IBridgeDiscoveryService discoveryService)
    {
        _multiBridgeService = multiBridgeService;
        _discoveryService = discoveryService;

        // Subscribe to connection status changes
        _multiBridgeService.BridgeConnectionChanged += OnBridgeConnectionChanged;

        LoadBridges();
    }

    public void LoadBridges()
    {
        Bridges.Clear();

        foreach (var bridge in _multiBridgeService.ConfiguredBridges)
        {
            var isConnected = _multiBridgeService.ConnectionStatus.TryGetValue(bridge.BridgeId, out var connected)
                && connected;

            var vm = new BridgeInfoViewModel
            {
                BridgeId = bridge.BridgeId,
                FriendlyName = bridge.FriendlyName ?? string.Empty,
                IpAddress = bridge.IpAddress,
                IsConnected = isConnected,
                LastConnected = bridge.LastConnected,
                DisplayName = bridge.DisplayName
            };

            vm.RemoveRequested += OnBridgeRemoveRequested;
            vm.RenameRequested += OnBridgeRenameRequested;
            vm.ReconnectRequested += OnBridgeReconnectRequested;

            Bridges.Add(vm);
        }
    }

    [RelayCommand]
    private async Task DiscoverBridgesAsync()
    {
        IsDiscovering = true;
        ErrorMessage = null;
        StatusMessage = "Discovering bridges on local network...";
        DiscoveredBridges.Clear();

        try
        {
            var discovered = await _discoveryService.DiscoverBridgesAsync(
                TimeSpan.FromSeconds(10),
                CancellationToken.None);

            foreach (var bridge in discovered)
            {
                // Skip already configured bridges
                if (_multiBridgeService.ConfiguredBridges.Any(b => b.BridgeId == bridge.BridgeId))
                {
                    continue;
                }

                var vm = new DiscoveredBridgeViewModel
                {
                    BridgeId = bridge.BridgeId,
                    IpAddress = bridge.IpAddress
                };

                vm.RegisterRequested += OnDiscoveredBridgeRegisterRequested;
                DiscoveredBridges.Add(vm);
            }

            HasDiscoveredBridges = DiscoveredBridges.Count > 0;

            if (DiscoveredBridges.Count > 0)
            {
                StatusMessage = $"Found {DiscoveredBridges.Count} bridge(s). Click 'Add' to configure.";
            }
            else
            {
                StatusMessage = "No new bridges found. Make sure they're on the same network.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Discovery failed: {ex.Message}";
            StatusMessage = null;
        }
        finally
        {
            IsDiscovering = false;
        }
    }

    private async void OnDiscoveredBridgeRegisterRequested(object? sender, DiscoveredBridgeViewModel vm)
    {
        try
        {
            await RegisterBridgeAsync(vm);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BridgeManagementViewModel] OnDiscoveredBridgeRegisterRequested failed: {ex.Message}");
        }
    }

    public async Task<Result> RegisterBridgeAsync(DiscoveredBridgeViewModel vm)
    {
        try
        {
            vm.IsRegistering = true;
            vm.StatusMessage = "Registering with bridge...";

            var result = await _discoveryService.RegisterAsync(
                vm.IpAddress,
                "HueCompanion",
                Environment.MachineName,
                CancellationToken.None);

            if (result.Success && result.AppKey != null)
            {
                var bridge = new BridgeModel
                {
                    BridgeId = vm.BridgeId,
                    IpAddress = vm.IpAddress,
                    AppKey = result.AppKey,
                    FriendlyName = null,
                    LastConnected = DateTime.UtcNow
                };

                await _multiBridgeService.AddBridgeAsync(bridge);

                // Remove from discovered list
                DiscoveredBridges.Remove(vm);
                HasDiscoveredBridges = DiscoveredBridges.Count > 0;

                // Reload configured bridges
                LoadBridges();

                StatusMessage = "Bridge added successfully!";
                return Result.Success();
            }
            else
            {
                vm.StatusMessage = null;
                vm.ErrorMessage = result.Error ?? "Registration failed. Make sure you pressed the link button.";
                return Result.Failure(result.Error ?? "Registration failed.");
            }
        }
        catch (Exception ex)
        {
            vm.StatusMessage = null;
            vm.ErrorMessage = $"Error: {ex.Message}";
            return Result.Failure(ex.Message);
        }
        finally
        {
            vm.IsRegistering = false;
            vm.IsWaitingForLinkButton = false;
        }
    }

    private async void OnBridgeRemoveRequested(object? sender, string bridgeId)
    {
        try
        {
            await _multiBridgeService.RemoveBridgeAsync(bridgeId);
            LoadBridges();
            StatusMessage = "Bridge removed.";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BridgeManagementViewModel] OnBridgeRemoveRequested failed: {ex.Message}");
        }
    }

    private async void OnBridgeRenameRequested(object? sender, (string BridgeId, string NewName) args)
    {
        try
        {
            await _multiBridgeService.UpdateBridgeNameAsync(args.BridgeId, args.NewName);
            LoadBridges();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BridgeManagementViewModel] OnBridgeRenameRequested failed: {ex.Message}");
        }
    }

    private async void OnBridgeReconnectRequested(object? sender, string bridgeId)
    {
        try
        {
            var bridge = Bridges.FirstOrDefault(b => b.BridgeId == bridgeId);
            if (bridge != null)
            {
                bridge.IsConnecting = true;
                bridge.StatusMessage = "Reconnecting...";
            }

            await _multiBridgeService.ConnectBridgeAsync(bridgeId);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BridgeManagementViewModel] OnBridgeReconnectRequested failed: {ex.Message}");
        }
    }

    private void OnBridgeConnectionChanged(object? sender, BridgeConnectionEventArgs e)
    {
        var bridge = Bridges.FirstOrDefault(b => b.BridgeId == e.BridgeId);
        if (bridge != null)
        {
            bridge.IsConnected = e.IsConnected;
            bridge.IsConnecting = false;

            if (e.IsConnected)
            {
                bridge.StatusMessage = "Connected";
            }
            else
            {
                bridge.StatusMessage = e.ErrorMessage ?? "Disconnected";
            }
        }
    }

    public void Dispose()
    {
        _multiBridgeService.BridgeConnectionChanged -= OnBridgeConnectionChanged;
    }
}

/// <summary>
/// ViewModel for a configured bridge in the bridge management UI.
/// </summary>
public partial class BridgeInfoViewModel : ObservableObject
{
    [ObservableProperty]
    private string _bridgeId = string.Empty;

    [ObservableProperty]
    private string _friendlyName = string.Empty;

    [ObservableProperty]
    private string _ipAddress = string.Empty;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private bool _isConnecting;

    [ObservableProperty]
    private DateTime _lastConnected;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isRenaming;

    [ObservableProperty]
    private string _newName = string.Empty;

    public event EventHandler<string>? RemoveRequested;
    public event EventHandler<(string BridgeId, string NewName)>? RenameRequested;
    public event EventHandler<string>? ReconnectRequested;

    [RelayCommand]
    private void Remove()
    {
        RemoveRequested?.Invoke(this, BridgeId);
    }

    [RelayCommand]
    private void StartRename()
    {
        IsRenaming = true;
        NewName = FriendlyName;
    }

    [RelayCommand]
    private void ConfirmRename()
    {
        if (!string.IsNullOrWhiteSpace(NewName))
        {
            RenameRequested?.Invoke(this, (BridgeId, NewName));
            FriendlyName = NewName;
        }
        IsRenaming = false;
    }

    [RelayCommand]
    private void CancelRename()
    {
        IsRenaming = false;
        NewName = string.Empty;
    }

    [RelayCommand]
    private void Reconnect()
    {
        ReconnectRequested?.Invoke(this, BridgeId);
    }
}

/// <summary>
/// ViewModel for a discovered bridge that can be added.
/// </summary>
public partial class DiscoveredBridgeViewModel : ObservableObject
{
    [ObservableProperty]
    private string _bridgeId = string.Empty;

    [ObservableProperty]
    private string _ipAddress = string.Empty;

    [ObservableProperty]
    private bool _isRegistering;

    [ObservableProperty]
    private bool _isWaitingForLinkButton;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _errorMessage;

    public event EventHandler<string>? AddRequested;
    public event EventHandler<DiscoveredBridgeViewModel>? RegisterRequested;

    [RelayCommand]
    private void Add()
    {
        // First click shows the "press link button" prompt
        IsWaitingForLinkButton = true;
        StatusMessage = "Press the link button on the bridge, then click 'Register'...";
        ErrorMessage = null;
        AddRequested?.Invoke(this, IpAddress);
    }

    [RelayCommand]
    private void Register()
    {
        // Second click triggers actual registration
        IsRegistering = true;
        StatusMessage = "Registering with bridge...";
        ErrorMessage = null;
        RegisterRequested?.Invoke(this, this);
    }

    [RelayCommand]
    private void Cancel()
    {
        IsWaitingForLinkButton = false;
        IsRegistering = false;
        StatusMessage = null;
        ErrorMessage = null;
    }
}
