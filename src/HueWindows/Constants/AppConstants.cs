namespace HueWindows.Constants;

/// <summary>
/// Centralized constants for the application.
/// </summary>
public static class AppConstants
{
    /// <summary>
    /// Animation-related constants.
    /// </summary>
    public static class Animation
    {
        /// <summary>Standard animation duration in milliseconds.</summary>
        public const int StandardDurationMs = 300;

        /// <summary>Fast animation duration in milliseconds.</summary>
        public const int FastDurationMs = 150;

        /// <summary>Slow animation duration in milliseconds.</summary>
        public const int SlowDurationMs = 500;

        /// <summary>Scale factor for pulse feedback animations.</summary>
        public const double PulseScaleFactor = 1.05;

        /// <summary>Stagger delay between list item entrance animations in milliseconds.</summary>
        public const int EntranceStaggerDelayMs = 25;
    }

    /// <summary>
    /// Brightness drag gesture constants.
    /// </summary>
    public static class BrightnessDrag
    {
        /// <summary>Minimum pixels to move before drag is recognized.</summary>
        public const double DragThreshold = 10;

        /// <summary>Pixels to drag per 1% brightness change.</summary>
        public const double PixelsPerPercent = 3;

        /// <summary>Minimum milliseconds between brightness updates.</summary>
        public const int ThrottleMs = 100;
    }

    /// <summary>
    /// Color and opacity constants.
    /// </summary>
    public static class Colors
    {
        /// <summary>Opacity for inactive/disabled icons (0-255).</summary>
        public const byte InactiveIconAlpha = 128;

        /// <summary>Opacity for inactive brightness bar (0-255).</summary>
        public const byte InactiveBrightnessBarAlpha = 96;

        /// <summary>Selection highlight color RGB components (yellow/gold).</summary>
        public const byte SelectionHighlightR = 255;
        public const byte SelectionHighlightG = 200;
        public const byte SelectionHighlightB = 0;
    }

    /// <summary>
    /// Window and layout constants.
    /// </summary>
    public static class Layout
    {
        /// <summary>Default window width.</summary>
        public const int DefaultWindowWidth = 1200;

        /// <summary>Default window height.</summary>
        public const int DefaultWindowHeight = 800;

        /// <summary>Max width for setup page content.</summary>
        public const int SetupMaxWidth = 600;

        /// <summary>Max width for settings page content.</summary>
        public const int SettingsMaxWidth = 1000;
    }
}
