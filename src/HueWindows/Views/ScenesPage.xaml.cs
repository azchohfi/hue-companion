using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using HueWindows.Controls;
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

    private void CreateScene_Click(object sender, RoutedEventArgs e)
    {
        // Navigate to Scene Builder page
        Frame.Navigate(typeof(SceneBuilderPage));
    }

    private async void AnimatedScene_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is AnimatedSceneModel scene)
        {
            await ViewModel.StartAnimatedSceneCommand.ExecuteAsync(scene);
        }
    }

    private void EditScene_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is AnimatedSceneModel scene)
        {
            // Navigate to Scene Builder with the scene ID to edit
            Frame.Navigate(typeof(SceneBuilderPage), scene.Id);
        }
    }

    private void NativeEffect_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedRoom == null)
        {
            ViewModel.ErrorMessage = "Select a room first to apply an effect.";
            return;
        }

        if (sender is Button button && button.Tag is NativeEffectInfo effect)
        {
            var flyout = new Flyout
            {
                ShouldConstrainToRootBounds = false,
                FlyoutPresenterStyle = (Style)Resources["EffectFlyoutPresenterStyle"]
            };

            var flyoutContent = new NativeEffectFlyout { Effect = effect };
            flyoutContent.ApplyRequested += async (s, args) =>
            {
                flyout.Hide();
                await ApplyNativeEffectAsync(args);
            };
            flyoutContent.SaveRequested += async (s, args) =>
            {
                flyout.Hide();
                await SaveNativeEffectAsSceneAsync(args);
            };

            flyout.Content = flyoutContent;
            flyout.Opening += (s, args) => flyoutContent.AnimateEntrance();
            flyout.ShowAt(button);
        }
    }

    private async Task ApplyNativeEffectAsync(NativeEffectApplyEventArgs args)
    {
        if (ViewModel.SelectedRoom == null) return;
        await ViewModel.ApplyEffectToRoomAsync(args.Effect.Id, args.Speed, args.Brightness);
    }

    private async Task SaveNativeEffectAsSceneAsync(NativeEffectApplyEventArgs args)
    {
        // TODO: Implement save dialog (Task 7)
        await ApplyNativeEffectAsync(args);
    }

    public Visibility InvertBool(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

    public bool HasError(string? error) => !string.IsNullOrWhiteSpace(error);
}
