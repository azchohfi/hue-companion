using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
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
