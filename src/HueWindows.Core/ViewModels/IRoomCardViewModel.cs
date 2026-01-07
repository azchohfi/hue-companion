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
    List<(byte R, byte G, byte B)> LightColors { get; }
    (byte R, byte G, byte B)? BackgroundColorRgb { get; }
    bool UseBlackText { get; }
    ICommand TapRoomCommand { get; }
    ICommand SetBrightnessCommand { get; }
}
