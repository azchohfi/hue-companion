using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Extensions.DependencyInjection;
using HueCompanion.Core.ViewModels;
using HueCompanion.Core.Models;

namespace HueCompanion.Views;

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

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        (ViewModel as IDisposable)?.Dispose();
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

    public Visibility InvertBool(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

    public bool HasError(string? error) => !string.IsNullOrWhiteSpace(error);

    public Visibility ShowEmptyState(int count) => count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public string GetSceneCountText(int count)
    {
        return count == 1 ? "1 scene" : $"{count} scenes";
    }

    public string GetAnimationCountText(int count)
    {
        return count == 1 ? "1 animation" : $"{count} animations";
    }
}
