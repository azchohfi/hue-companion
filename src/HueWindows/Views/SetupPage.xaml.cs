using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using HueWindows.Core.Models;
using HueWindows.Core.Services.Interfaces;
using HueWindows.Core.ViewModels;
using Microsoft.UI.Xaml.Navigation;

namespace HueWindows.Views;

/// <summary>
/// Page for discovering and connecting to a Hue bridge.
/// </summary>
public sealed partial class SetupPage : Page
{
    public SetupViewModel ViewModel { get; }

    public SetupPage()
    {
        ViewModel = App.Services.GetRequiredService<SetupViewModel>();
        ViewModel.SetupCompleted += OnSetupCompleted;

        this.InitializeComponent();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel.SetupCompleted -= OnSetupCompleted;
    }

    private void OnSetupCompleted(object? sender, EventArgs e)
    {
        var navigationService = App.Services.GetRequiredService<INavigationService>();
        navigationService.NavigateTo<DashboardPage>();
    }

    private void ConnectBridge_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is DiscoveredBridge bridge)
        {
            ViewModel.SelectBridgeCommand.Execute(bridge);
        }
    }

    /// <summary>
    /// Helper method to invert a boolean for visibility binding.
    /// </summary>
    public Visibility InvertBool(bool value)
    {
        return value ? Visibility.Collapsed : Visibility.Visible;
    }
}
