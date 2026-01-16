using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using HueWindows.Core.ViewModels;
using HueWindows.Core.Models;

namespace HueWindows.Views;

/// <summary>
/// Scene library page for browsing animated scenes by category.
/// </summary>
public sealed partial class SceneLibraryPage : Page
{
    public SceneLibraryViewModel ViewModel { get; }

    public SceneLibraryPage()
    {
        this.InitializeComponent();
        ViewModel = App.Services.GetRequiredService<SceneLibraryViewModel>();
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.InitializeAsync();
    }

    private void CategoryButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton button && button.Tag is string category)
        {
            ViewModel.SelectCategoryCommand.Execute(category);
        }
    }

    private async void Scene_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is AnimatedSceneModel scene)
        {
            await ViewModel.StartSceneCommand.ExecuteAsync(scene);
        }
    }

    public bool InvertBool(bool value) => !value;

    public bool HasError(string error) => !string.IsNullOrWhiteSpace(error);

    public bool ShowEmptyState(int count) => count == 0;

    public string GetSceneCountText(int count)
    {
        return count == 1 ? "1 scene" : $"{count} scenes";
    }

    public string GetAnimationCountText(int count)
    {
        return count == 1 ? "1 animation" : $"{count} animations";
    }
}
