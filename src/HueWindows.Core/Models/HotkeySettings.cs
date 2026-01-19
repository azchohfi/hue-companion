using System.Text.Json.Serialization;

namespace HueWindows.Core.Models;

/// <summary>
/// Settings for the global hotkey feature.
/// </summary>
public class HotkeySettings
{
    /// <summary>
    /// Whether the global hotkey is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// The modifier keys for the hotkey.
    /// </summary>
    public HotkeyModifiers Modifiers { get; set; } = HotkeyModifiers.Win | HotkeyModifiers.Shift;

    /// <summary>
    /// The virtual key code for the hotkey.
    /// </summary>
    public VirtualKey Key { get; set; } = VirtualKey.H;

    /// <summary>
    /// Gets a human-readable string representation of the hotkey.
    /// </summary>
    [JsonIgnore]
    public string DisplayString
    {
        get
        {
            var parts = new List<string>();

            if (Modifiers.HasFlag(HotkeyModifiers.Win))
                parts.Add("Win");
            if (Modifiers.HasFlag(HotkeyModifiers.Ctrl))
                parts.Add("Ctrl");
            if (Modifiers.HasFlag(HotkeyModifiers.Alt))
                parts.Add("Alt");
            if (Modifiers.HasFlag(HotkeyModifiers.Shift))
                parts.Add("Shift");

            parts.Add(Key.ToString());

            return string.Join(" + ", parts);
        }
    }
}

/// <summary>
/// Modifier keys for hotkeys.
/// </summary>
[Flags]
public enum HotkeyModifiers
{
    /// <summary>No modifiers.</summary>
    None = 0,
    /// <summary>Alt key.</summary>
    Alt = 0x0001,
    /// <summary>Ctrl key.</summary>
    Ctrl = 0x0002,
    /// <summary>Shift key.</summary>
    Shift = 0x0004,
    /// <summary>Windows key.</summary>
    Win = 0x0008
}

/// <summary>
/// Virtual key codes for common keys.
/// </summary>
public enum VirtualKey
{
    // Letters
    A = 0x41, B = 0x42, C = 0x43, D = 0x44, E = 0x45,
    F = 0x46, G = 0x47, H = 0x48, I = 0x49, J = 0x4A,
    K = 0x4B, L = 0x4C, M = 0x4D, N = 0x4E, O = 0x4F,
    P = 0x50, Q = 0x51, R = 0x52, S = 0x53, T = 0x54,
    U = 0x55, V = 0x56, W = 0x57, X = 0x58, Y = 0x59,
    Z = 0x5A,

    // Numbers
    D0 = 0x30, D1 = 0x31, D2 = 0x32, D3 = 0x33, D4 = 0x34,
    D5 = 0x35, D6 = 0x36, D7 = 0x37, D8 = 0x38, D9 = 0x39,

    // Function keys
    F1 = 0x70, F2 = 0x71, F3 = 0x72, F4 = 0x73,
    F5 = 0x74, F6 = 0x75, F7 = 0x76, F8 = 0x77,
    F9 = 0x78, F10 = 0x79, F11 = 0x7A, F12 = 0x7B,

    // Special keys
    Space = 0x20,
    Enter = 0x0D,
    Tab = 0x09,
    Escape = 0x1B,
    Backspace = 0x08,
    Delete = 0x2E,
    Insert = 0x2D,
    Home = 0x24,
    End = 0x23,
    PageUp = 0x21,
    PageDown = 0x22,

    // Arrow keys
    Left = 0x25,
    Up = 0x26,
    Right = 0x27,
    Down = 0x28,

    // Punctuation
    OemTilde = 0xC0,      // `~
    OemMinus = 0xBD,      // -_
    OemPlus = 0xBB,       // =+
    OemOpenBrackets = 0xDB,  // [{
    OemCloseBrackets = 0xDD, // ]}
    OemPipe = 0xDC,       // \|
    OemSemicolon = 0xBA,  // ;:
    OemQuotes = 0xDE,     // '"
    OemComma = 0xBC,      // ,<
    OemPeriod = 0xBE,     // .>
    OemQuestion = 0xBF    // /?
}
