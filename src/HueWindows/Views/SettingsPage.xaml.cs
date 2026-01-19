using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using HueWindows.Core.Models;
using HueWindows.Core.Services.Interfaces;
using HueWindows.Core.ViewModels;

namespace HueWindows.Views;

/// <summary>
/// Settings page for app configuration.
/// </summary>
public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; }

    public SettingsPage()
    {
        ViewModel = App.Services.GetRequiredService<SettingsViewModel>();
        ViewModel.RepairBridgeRequested += OnRepairBridgeRequested;
        ViewModel.BridgeSetupRequested += OnBridgeSetupRequested;

        this.InitializeComponent();
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        ViewModel.LoadSettings();
    }

    private void OnRepairBridgeRequested(object? sender, EventArgs e)
    {
        var navigationService = App.Services.GetRequiredService<INavigationService>();
        navigationService.NavigateTo<SetupPage>();
    }

    private StackPanel? _bridgeManagementPanel;
    private TextBlock? _statusMessageText;
    private StackPanel? _discoveredBridgesPanel;
    private StackPanel? _configuredBridgesPanel;
    private ProgressRing? _discoveryProgress;

    private async void OnBridgeSetupRequested(object? sender, EventArgs e)
    {
        var vm = ViewModel.BridgeManagement;
        if (vm == null) return;

        // Subscribe to changes
        vm.PropertyChanged += OnBridgeManagementPropertyChanged;
        vm.Bridges.CollectionChanged += OnBridgesCollectionChanged;
        vm.DiscoveredBridges.CollectionChanged += OnDiscoveredBridgesCollectionChanged;

        var dialog = new ContentDialog
        {
            Title = "Manage Bridges",
            CloseButtonText = "Close",
            XamlRoot = this.XamlRoot,
            Content = CreateBridgeManagementContent()
        };

        await dialog.ShowAsync();

        // Unsubscribe from changes
        vm.PropertyChanged -= OnBridgeManagementPropertyChanged;
        vm.Bridges.CollectionChanged -= OnBridgesCollectionChanged;
        vm.DiscoveredBridges.CollectionChanged -= OnDiscoveredBridgesCollectionChanged;

        // Clear references
        _bridgeManagementPanel = null;
        _statusMessageText = null;
        _discoveredBridgesPanel = null;
        _configuredBridgesPanel = null;
        _discoveryProgress = null;

        // Refresh settings after dialog closes
        ViewModel.LoadSettings();
    }

    private void OnBridgeManagementPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        var vm = ViewModel.BridgeManagement;
        if (vm == null) return;

        DispatcherQueue.TryEnqueue(() =>
        {
            if (e.PropertyName == nameof(vm.StatusMessage) && _statusMessageText != null)
            {
                _statusMessageText.Text = vm.StatusMessage ?? "";
                _statusMessageText.Visibility = string.IsNullOrEmpty(vm.StatusMessage)
                    ? Visibility.Collapsed : Visibility.Visible;
            }
            else if (e.PropertyName == nameof(vm.IsDiscovering) && _discoveryProgress != null)
            {
                _discoveryProgress.IsActive = vm.IsDiscovering;
                _discoveryProgress.Visibility = vm.IsDiscovering ? Visibility.Visible : Visibility.Collapsed;
            }
        });
    }

    private void OnBridgesCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(RefreshConfiguredBridges);
    }

    private void OnDiscoveredBridgesCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(RefreshDiscoveredBridges);
    }

    private void RefreshConfiguredBridges()
    {
        var vm = ViewModel.BridgeManagement;
        if (vm == null || _configuredBridgesPanel == null) return;

        _configuredBridgesPanel.Children.Clear();

        if (vm.Bridges.Count == 0)
        {
            _configuredBridgesPanel.Children.Add(new TextBlock
            {
                Text = "No bridges configured",
                Foreground = (SolidColorBrush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            });
        }
        else
        {
            foreach (var bridge in vm.Bridges)
            {
                _configuredBridgesPanel.Children.Add(CreateBridgeCard(bridge));
            }
        }
    }

    private void RefreshDiscoveredBridges()
    {
        var vm = ViewModel.BridgeManagement;
        if (vm == null || _discoveredBridgesPanel == null) return;

        _discoveredBridgesPanel.Children.Clear();

        foreach (var discovered in vm.DiscoveredBridges)
        {
            _discoveredBridgesPanel.Children.Add(CreateDiscoveredBridgeCard(discovered));
        }
    }

    private StackPanel CreateBridgeManagementContent()
    {
        var vm = ViewModel.BridgeManagement;
        if (vm == null) return new StackPanel();

        _bridgeManagementPanel = new StackPanel { Spacing = 16, MinWidth = 400 };

        // Configured bridges section
        var bridgesHeader = new TextBlock
        {
            Text = "Configured Bridges",
            Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"]
        };
        _bridgeManagementPanel.Children.Add(bridgesHeader);

        _configuredBridgesPanel = new StackPanel { Spacing = 4 };
        if (vm.Bridges.Count == 0)
        {
            _configuredBridgesPanel.Children.Add(new TextBlock
            {
                Text = "No bridges configured",
                Foreground = (SolidColorBrush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            });
        }
        else
        {
            foreach (var bridge in vm.Bridges)
            {
                _configuredBridgesPanel.Children.Add(CreateBridgeCard(bridge));
            }
        }
        _bridgeManagementPanel.Children.Add(_configuredBridgesPanel);

        // Discovery section
        var discoverHeader = new TextBlock
        {
            Text = "Add New Bridge",
            Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
            Margin = new Thickness(0, 16, 0, 0)
        };
        _bridgeManagementPanel.Children.Add(discoverHeader);

        var discoverRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        discoverRow.Children.Add(new Button
        {
            Content = "Discover Bridges",
            Command = vm.DiscoverBridgesCommand
        });

        _discoveryProgress = new ProgressRing
        {
            IsActive = vm.IsDiscovering,
            Width = 20,
            Height = 20,
            Visibility = vm.IsDiscovering ? Visibility.Visible : Visibility.Collapsed
        };
        discoverRow.Children.Add(_discoveryProgress);
        _bridgeManagementPanel.Children.Add(discoverRow);

        // Status message
        _statusMessageText = new TextBlock
        {
            Text = vm.StatusMessage ?? "",
            TextWrapping = TextWrapping.Wrap,
            Foreground = (SolidColorBrush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            Visibility = string.IsNullOrEmpty(vm.StatusMessage) ? Visibility.Collapsed : Visibility.Visible
        };
        _bridgeManagementPanel.Children.Add(_statusMessageText);

        // Discovered bridges container
        _discoveredBridgesPanel = new StackPanel { Spacing = 4 };
        foreach (var discovered in vm.DiscoveredBridges)
        {
            _discoveredBridgesPanel.Children.Add(CreateDiscoveredBridgeCard(discovered));
        }
        _bridgeManagementPanel.Children.Add(_discoveredBridgesPanel);

        return _bridgeManagementPanel;
    }

    private Border CreateBridgeCard(Core.ViewModels.BridgeInfoViewModel bridge)
    {
        var card = new Border
        {
            Background = (SolidColorBrush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (SolidColorBrush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 4, 0, 4)
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var info = new StackPanel { Spacing = 2 };
        info.Children.Add(new TextBlock
        {
            Text = bridge.DisplayName,
            Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"]
        });

        var statusPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        statusPanel.Children.Add(new Ellipse
        {
            Width = 8,
            Height = 8,
            Fill = new SolidColorBrush(bridge.IsConnected ? Colors.Green : Colors.Red),
            VerticalAlignment = VerticalAlignment.Center
        });
        statusPanel.Children.Add(new TextBlock
        {
            Text = bridge.IsConnected ? "Connected" : "Disconnected",
            Foreground = (SolidColorBrush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            FontSize = 12
        });
        info.Children.Add(statusPanel);

        info.Children.Add(new TextBlock
        {
            Text = bridge.IpAddress,
            Foreground = (SolidColorBrush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            FontSize = 12
        });

        Grid.SetColumn(info, 0);
        grid.Children.Add(info);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        if (!bridge.IsConnected)
        {
            buttons.Children.Add(new Button
            {
                Content = "Reconnect",
                Command = bridge.ReconnectCommand
            });
        }
        buttons.Children.Add(new Button
        {
            Content = "Remove",
            Command = bridge.RemoveCommand
        });

        Grid.SetColumn(buttons, 1);
        grid.Children.Add(buttons);

        card.Child = grid;
        return card;
    }

    private Border CreateDiscoveredBridgeCard(Core.ViewModels.DiscoveredBridgeViewModel discovered)
    {
        var card = new Border
        {
            Background = (SolidColorBrush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (SolidColorBrush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 4, 0, 4)
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var info = new StackPanel { Spacing = 2 };
        info.Children.Add(new TextBlock
        {
            Text = $"Bridge {discovered.BridgeId[..8]}...",
            Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"]
        });
        info.Children.Add(new TextBlock
        {
            Text = discovered.IpAddress,
            Foreground = (SolidColorBrush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            FontSize = 12
        });

        if (!string.IsNullOrEmpty(discovered.StatusMessage))
        {
            info.Children.Add(new TextBlock
            {
                Text = discovered.StatusMessage,
                Foreground = (SolidColorBrush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"],
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            });
        }

        Grid.SetColumn(info, 0);
        grid.Children.Add(info);

        var button = discovered.IsWaitingForLinkButton
            ? new Button { Content = "Register", Command = discovered.RegisterCommand }
            : new Button { Content = "Add", Command = discovered.AddCommand };

        Grid.SetColumn(button, 1);
        grid.Children.Add(button);

        card.Child = grid;
        return card;
    }

    private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox comboBox)
        {
            var theme = comboBox.SelectedIndex switch
            {
                0 => AppTheme.System,
                1 => AppTheme.Light,
                2 => AppTheme.Dark,
                _ => AppTheme.System
            };

            ViewModel.SelectedTheme = theme;
            ApplyTheme(theme);
        }
    }

    private void ApplyTheme(AppTheme theme)
    {
        var rootElement = App.MainWindow.Content as FrameworkElement;
        if (rootElement == null) return;

        rootElement.RequestedTheme = theme switch
        {
            AppTheme.Light => ElementTheme.Light,
            AppTheme.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default
        };
    }

    /// <summary>
    /// Helper to get theme combo box index.
    /// </summary>
    public int GetThemeIndex(AppTheme theme)
    {
        return theme switch
        {
            AppTheme.System => 0,
            AppTheme.Light => 1,
            AppTheme.Dark => 2,
            _ => 0
        };
    }

    /// <summary>
    /// Helper to get multi-bridge connection status text.
    /// </summary>
    public string GetMultiBridgeConnectionStatus(int totalCount, int connectedCount)
    {
        if (totalCount == 0) return "No bridges configured";
        if (connectedCount == 0) return "Disconnected";
        if (connectedCount == totalCount) return $"Connected ({connectedCount})";
        return $"Partial ({connectedCount}/{totalCount})";
    }

    /// <summary>
    /// Helper to get multi-bridge connection indicator color.
    /// </summary>
    public SolidColorBrush GetMultiBridgeConnectionColor(int connectedCount)
    {
        return new SolidColorBrush(connectedCount > 0 ? Colors.Green : Colors.Red);
    }

    /// <summary>
    /// Helper to get bridge count text.
    /// </summary>
    public string GetBridgeCountText(int count)
    {
        return count switch
        {
            0 => "No bridges configured",
            1 => "1 bridge configured",
            _ => $"{count} bridges configured"
        };
    }
}
