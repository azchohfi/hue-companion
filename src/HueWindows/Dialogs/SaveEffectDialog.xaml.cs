using Microsoft.UI.Xaml.Controls;
using HueWindows.Core.Models;

namespace HueWindows.Dialogs;

/// <summary>
/// Dialog for saving a native effect as a user scene.
/// </summary>
public sealed partial class SaveEffectDialog : ContentDialog
{
    /// <summary>
    /// Gets the user-entered scene name.
    /// </summary>
    public string SceneName => SceneNameTextBox.Text;

    /// <summary>
    /// Gets the effect being saved.
    /// </summary>
    public NativeEffectInfo Effect { get; private set; } = null!;

    /// <summary>
    /// Gets the speed value (0.0-1.0).
    /// </summary>
    public double Speed { get; private set; }

    /// <summary>
    /// Gets the brightness value (0.0-1.0).
    /// </summary>
    public double Brightness { get; private set; }

    public SaveEffectDialog()
    {
        this.InitializeComponent();
    }

    /// <summary>
    /// Sets the effect information to display in the dialog.
    /// </summary>
    /// <param name="effect">The native effect.</param>
    /// <param name="speed">Effect speed (0.0-1.0).</param>
    /// <param name="brightness">Brightness level (0.0-1.0).</param>
    public void SetEffect(NativeEffectInfo effect, double speed, double brightness)
    {
        Effect = effect;
        Speed = speed;
        Brightness = brightness;

        SceneNameTextBox.Text = $"My {effect.Name}";
        EffectNameRun.Text = effect.Name;
        SpeedValueRun.Text = $"{(int)(speed * 100)}%";
        BrightnessValueRun.Text = $"{(int)(brightness * 100)}%";
    }

    private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (string.IsNullOrWhiteSpace(SceneNameTextBox.Text))
        {
            args.Cancel = true;
        }
    }
}
