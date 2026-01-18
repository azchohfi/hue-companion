using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using HueWindows.Controls;
using HueWindows.Core.Models;
using HueWindows.Core.ViewModels;

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
        PopulateHueEffectsGrid();
    }

    private void PopulateHueEffectsGrid()
    {
        HueEffectsGrid.Children.Clear();
        HueEffectsGrid.RowDefinitions.Clear();

        var effects = ViewModel.NativeEffects;
        int rowCount = (effects.Count + 1) / 2; // 2 columns

        // Add row definitions
        for (int i = 0; i < rowCount; i++)
        {
            HueEffectsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        // Add buttons
        for (int i = 0; i < effects.Count; i++)
        {
            var effect = effects[i];
            var button = CreateEffectButton(effect);
            Grid.SetRow(button, i / 2);
            Grid.SetColumn(button, i % 2);
            HueEffectsGrid.Children.Add(button);
        }
    }

    private Button CreateEffectButton(NativeEffectInfo effect)
    {
        var icon = new FontIcon
        {
            Glyph = "\uE7B1",
            FontSize = 16
        };
        // Try to get accent color from theme resources
        if (Application.Current.Resources.TryGetValue("AccentTextFillColorPrimaryBrush", out var accentBrush))
        {
            icon.Foreground = accentBrush as Microsoft.UI.Xaml.Media.Brush;
        }

        var nameText = new TextBlock
        {
            Text = effect.Name,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        };

        var headerPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };
        headerPanel.Children.Add(icon);
        headerPanel.Children.Add(nameText);

        var descText = new TextBlock
        {
            Text = effect.Description,
            FontSize = 12
        };
        // Try to get secondary text color from theme resources
        if (Application.Current.Resources.TryGetValue("TextFillColorSecondaryBrush", out var secondaryBrush))
        {
            descText.Foreground = secondaryBrush as Microsoft.UI.Xaml.Media.Brush;
        }

        var contentPanel = new StackPanel { Spacing = 4 };
        contentPanel.Children.Add(headerPanel);
        contentPanel.Children.Add(descText);

        var button = new Button
        {
            Content = contentPanel,
            Tag = effect,
            Padding = new Thickness(16),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinHeight = 80
        };
        button.Click += NativeEffect_Click;

        return button;
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
        var dialog = new Dialogs.SaveEffectDialog();
        dialog.XamlRoot = this.XamlRoot;
        dialog.SetEffect(args.Effect, args.Speed, args.Brightness);

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            var scene = new AnimatedSceneModel
            {
                Id = $"user_{Guid.NewGuid():N}",
                Name = dialog.SceneName,
                Description = $"{args.Effect.Name} effect",
                Category = "effect",
                Animations = new List<AnimationDefinition>
                {
                    new AnimationDefinition
                    {
                        Id = $"{args.Effect.Id}_effect",
                        Name = args.Effect.Id,
                        Type = AnimationType.NativeEffect,
                        LightAssignment = LightAssignment.All,
                        RepeatMode = RepeatMode.Loop,
                        EffectSpeed = args.Speed,
                        EffectBrightness = args.Brightness
                    }
                }
            };

            var errorBefore = ViewModel.ErrorMessage;
            await ViewModel.SaveUserSceneAsync(scene);

            // Only apply effect if save succeeded (no new error)
            if (ViewModel.ErrorMessage == errorBefore)
            {
                await ApplyNativeEffectAsync(args);
            }
        }
    }

    public Visibility InvertBool(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

    public bool HasError(string? error) => !string.IsNullOrWhiteSpace(error);
}
