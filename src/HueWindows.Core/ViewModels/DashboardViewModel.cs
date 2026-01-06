using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using HueWindows.Core.Models;
using HueWindows.Core.Services.Interfaces;

namespace HueWindows.Core.ViewModels;

/// <summary>
/// ViewModel for the dashboard/home page showing room cards.
/// </summary>
public partial class DashboardViewModel : ObservableObject
{
    private readonly IHueBridgeService _bridgeService;
    private readonly IMessenger _messenger;

    [ObservableProperty]
    private ObservableCollection<RoomCardViewModel> _roomCards = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _showEmptyState;

    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>
    /// Event raised when a room is selected for detail view.
    /// </summary>
    public event EventHandler<Guid>? RoomSelected;

    public DashboardViewModel(
        IHueBridgeService bridgeService,
        IMessenger messenger)
    {
        _bridgeService = bridgeService;
        _messenger = messenger;

        // Subscribe to light state changes
        _bridgeService.LightStateChanged += OnLightStateChanged;
    }

    [RelayCommand]
    public async Task LoadRoomsAsync()
    {
        if (IsLoading) return;

        IsLoading = true;
        ErrorMessage = null;
        ShowEmptyState = false;

        try
        {
            var rooms = await _bridgeService.GetRoomsAsync();

            RoomCards.Clear();

            foreach (var room in rooms)
            {
                var cardVm = new RoomCardViewModel(room, _bridgeService);
                cardVm.RoomTapped += OnRoomTapped;
                RoomCards.Add(cardVm);
            }

            ShowEmptyState = RoomCards.Count == 0;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load rooms: {ex.Message}";
            ShowEmptyState = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadRoomsAsync();
    }

    private void OnRoomTapped(object? sender, Guid roomId)
    {
        RoomSelected?.Invoke(this, roomId);
    }

    private void OnLightStateChanged(object? sender, LightStateChangedEventArgs e)
    {
        // Update room cards when light states change
        foreach (var roomCard in RoomCards)
        {
            roomCard.OnLightStateChanged(e);
        }
    }
}

/// <summary>
/// ViewModel for an individual room card on the dashboard.
/// </summary>
public partial class RoomCardViewModel : ObservableObject
{
    private readonly IHueBridgeService _bridgeService;
    private readonly RoomModel _room;

    public Guid RoomId => _room.Id;

    [ObservableProperty]
    private string _roomName;

    [ObservableProperty]
    private bool _isOn;

    [ObservableProperty]
    private double _brightness;

    [ObservableProperty]
    private HueColor? _dominantColor;

    [ObservableProperty]
    private string _roomIcon;

    [ObservableProperty]
    private int _lightCount;

    [ObservableProperty]
    private bool _isAdjusting;

    /// <summary>
    /// Event raised when the room card is tapped (for navigation).
    /// </summary>
    public event EventHandler<Guid>? RoomTapped;

    public int BrightnessPercent => (int)(Brightness * 100);
    public string BrightnessDisplayText => $"{BrightnessPercent}%";

    /// <summary>
    /// Gets all unique light colors in the room as RGB values for gradient display.
    /// </summary>
    public List<(byte R, byte G, byte B)> LightColors
    {
        get
        {
            if (!IsOn) return new List<(byte R, byte G, byte B)>();

            var colors = _room.Lights
                .Where(l => l.IsOn && l.CurrentColor != null)
                .Select(l => l.CurrentColor!.ToRgb(1.0))
                .Distinct()
                .ToList();

            return colors;
        }
    }

    /// <summary>
    /// Gets the background color RGB values when the room is on.
    /// Returns null if no color is available or room is off.
    /// </summary>
    public (byte R, byte G, byte B)? BackgroundColorRgb
    {
        get
        {
            var colors = LightColors;
            if (colors.Count == 0) return null;
            return colors[0];
        }
    }

    /// <summary>
    /// Returns true if black text should be used on the current background.
    /// Uses the average luminance of all light colors.
    /// </summary>
    public bool UseBlackText
    {
        get
        {
            var colors = LightColors;
            if (colors.Count == 0) return false;

            // Calculate average luminance
            double totalLuminance = 0;
            foreach (var (r, g, b) in colors)
            {
                // Simple luminance calculation
                totalLuminance += (0.299 * r + 0.587 * g + 0.114 * b) / 255.0;
            }
            double avgLuminance = totalLuminance / colors.Count;

            return avgLuminance > 0.5;
        }
    }

    public RoomCardViewModel(RoomModel room, IHueBridgeService bridgeService)
    {
        _room = room;
        _bridgeService = bridgeService;

        _roomName = room.Name;
        _isOn = room.IsOn;
        _brightness = room.Brightness;
        _dominantColor = room.DominantColor;
        _lightCount = room.Lights.Count;
        _roomIcon = GetIconForArchetype(room.Archetype);
    }

    partial void OnIsOnChanged(bool value)
    {
        // Send command to bridge when toggle changes
        _ = _bridgeService.SetRoomOnAsync(RoomId, value);

        // Update computed color properties
        OnPropertyChanged(nameof(BackgroundColorRgb));
        OnPropertyChanged(nameof(UseBlackText));
    }

    partial void OnBrightnessChanged(double value)
    {
        // Update computed color properties when brightness changes
        OnPropertyChanged(nameof(BackgroundColorRgb));
        OnPropertyChanged(nameof(UseBlackText));
    }

    partial void OnDominantColorChanged(HueColor? value)
    {
        // Update computed color properties when color changes
        OnPropertyChanged(nameof(BackgroundColorRgb));
        OnPropertyChanged(nameof(UseBlackText));
    }

    [RelayCommand]
    private async Task SetBrightnessAsync(double brightness)
    {
        Brightness = Math.Clamp(brightness, 0.0, 1.0);
        OnPropertyChanged(nameof(BrightnessPercent));
        OnPropertyChanged(nameof(BrightnessDisplayText));

        await _bridgeService.SetRoomBrightnessAsync(RoomId, Brightness);

        // Update on state based on brightness
        if (Brightness > 0 && !IsOn)
        {
            _isOn = true;
            OnPropertyChanged(nameof(IsOn));
        }
        else if (Brightness == 0 && IsOn)
        {
            _isOn = false;
            OnPropertyChanged(nameof(IsOn));
        }
    }

    [RelayCommand]
    private void TapRoom()
    {
        RoomTapped?.Invoke(this, RoomId);
    }

    /// <summary>
    /// Called when a light state change event is received.
    /// </summary>
    public void OnLightStateChanged(LightStateChangedEventArgs e)
    {
        // Check if any light in this room changed
        var light = _room.Lights.FirstOrDefault(l => l.Id == e.LightId);
        if (light == null) return;

        // Update light state
        if (e.IsOn.HasValue)
        {
            light.IsOn = e.IsOn.Value;
        }
        if (e.Brightness.HasValue)
        {
            light.Brightness = e.Brightness.Value;
        }
        if (e.Color != null)
        {
            light.CurrentColor = e.Color;
        }

        // Recalculate room state
        IsOn = _room.Lights.Any(l => l.IsOn);
        Brightness = _room.Lights.Where(l => l.IsOn).DefaultIfEmpty()
            .Average(l => l?.Brightness ?? 0);

        var coloredLight = _room.Lights.FirstOrDefault(l => l.IsOn && l.CurrentColor != null);
        DominantColor = coloredLight?.CurrentColor;

        OnPropertyChanged(nameof(BrightnessPercent));
        OnPropertyChanged(nameof(BrightnessDisplayText));
        OnPropertyChanged(nameof(BackgroundColorRgb));
        OnPropertyChanged(nameof(UseBlackText));
    }

    private static string GetIconForArchetype(RoomArchetype archetype)
    {
        return archetype switch
        {
            RoomArchetype.LivingRoom => "\uE7F4",    // Couch
            RoomArchetype.Kitchen => "\uED56",       // Kitchen
            RoomArchetype.Dining => "\uE77C",        // Table
            RoomArchetype.Bedroom => "\uE708",       // Bed
            RoomArchetype.Bathroom => "\uE9F1",      // Bath
            RoomArchetype.Office => "\uE8D8",        // Work
            RoomArchetype.Hallway => "\uE8A0",       // Hall
            RoomArchetype.Garage => "\uE804",        // Car
            RoomArchetype.Garden => "\uE8BE",        // Tree
            _ => "\uE781"                            // Default light bulb
        };
    }
}
