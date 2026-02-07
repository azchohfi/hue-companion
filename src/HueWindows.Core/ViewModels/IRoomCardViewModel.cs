using System.ComponentModel;
using System.Windows.Input;
using HueWindows.Core.Models;

namespace HueWindows.Core.ViewModels;

/// <summary>
/// Interface for ViewModels that can be displayed in a RoomCard control.
/// </summary>
public interface IRoomCardViewModel : INotifyPropertyChanged
{
    string RoomName { get; }
    bool IsOn { get; set; }
    double Brightness { get; }
    string RoomIcon { get; }
    int LightCount { get; }
    int BrightnessPercent { get; }
    IReadOnlyList<(byte R, byte G, byte B)> LightColors { get; }
    (byte R, byte G, byte B)? BackgroundColorRgb { get; }
    bool UseBlackText { get; }
    bool SupportsColor { get; }
    ICommand TapRoomCommand { get; }
    ICommand SetBrightnessCommand { get; }
    ICommand? SetColorCommand { get; }
}
