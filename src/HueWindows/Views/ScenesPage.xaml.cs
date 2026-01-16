using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using HueWindows.Core.ViewModels;
using HueWindows.Core.Models;

namespace HueWindows.Views;

/// <summary>
/// Main scenes page showing both static and animated scenes.
/// </summary>
public sealed partial class ScenesPage : Page
{
    public ScenesViewModel ViewModel { get; }

    public ScenesPage()
    {
        this.InitializeComponent();
        ViewModel = App.Services.GetRequiredService<ScenesViewModel>();
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.InitializeAsync();
    }

    private void BrowseAnimatedScenes_Click(object sender, RoutedEventArgs e)
    {
        // Navigate to Scene Library page
        Frame.Navigate(typeof(SceneLibraryPage));
    }

    private async void AnimatedScene_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is AnimatedSceneModel scene)
        {
            await ViewModel.StartAnimatedSceneCommand.ExecuteAsync(scene);
        }
    }

    public bool InvertBool(bool value) => !value;

    public bool HasError(string error) => !string.IsNullOrWhiteSpace(error);
}
