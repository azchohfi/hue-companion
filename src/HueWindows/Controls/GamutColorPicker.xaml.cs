using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using HueWindows.Core.Models;
using System;
using System.Numerics;
using Windows.UI;

namespace HueWindows.Controls;

public sealed partial class GamutColorPicker : UserControl
{
    // Dependency properties
    public static readonly DependencyProperty SelectedColorProperty =
        DependencyProperty.Register(nameof(SelectedColor), typeof(HueColor),
            typeof(GamutColorPicker), new PropertyMetadata(HueColor.White, OnSelectedColorChanged));

    public static readonly DependencyProperty GamutProperty =
        DependencyProperty.Register(nameof(Gamut), typeof(ColorGamut),
            typeof(GamutColorPicker), new PropertyMetadata(ColorGamut.GamutC, OnGamutChanged));

    public HueColor SelectedColor
    {
        get => (HueColor)GetValue(SelectedColorProperty);
        set => SetValue(SelectedColorProperty, value);
    }

    public ColorGamut Gamut
    {
        get => (ColorGamut)GetValue(GamutProperty);
        set => SetValue(GamutProperty, value);
    }

    public event EventHandler<HueColor>? ColorChanged;

    // Rendering constants
    private const float OuterRadius = 72f;    // Outer edge of hue ring
    private const float InnerRadius = 50f;    // Inner edge of hue ring (saturation area)
    private const float SelectorRadius = 6f;

    // State
    private double _selectedHue = 0;          // 0-360
    private double _selectedSaturation = 1.0; // 0-1
    private bool _isDragging = false;
    private bool _isDraggingHue = false;

    // Cached max saturation per hue (360 values)
    private double[] _maxSaturationCache = new double[360];

    public GamutColorPicker()
    {
        this.InitializeComponent();
        RecalculateMaxSaturation();
    }

    private static void OnSelectedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var picker = (GamutColorPicker)d;
        picker.UpdateFromSelectedColor();
        picker.ColorCanvas?.Invalidate();
    }

    private static void OnGamutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var picker = (GamutColorPicker)d;
        picker.RecalculateMaxSaturation();
        picker.ColorCanvas?.Invalidate();
    }

    private void RecalculateMaxSaturation()
    {
        for (int hue = 0; hue < 360; hue++)
        {
            _maxSaturationCache[hue] = Gamut.GetMaxSaturationForHue(hue);
        }
    }

    private void UpdateFromSelectedColor()
    {
        // Convert HueColor (xy) to HSV for display
        var (r, g, b) = SelectedColor.ToRgb(1.0);
        RgbToHsv(r / 255.0, g / 255.0, b / 255.0, out var h, out var s, out _);
        _selectedHue = h;
        _selectedSaturation = s;
    }

    private void ColorCanvas_CreateResources(CanvasControl sender, Microsoft.Graphics.Canvas.UI.CanvasCreateResourcesEventArgs args)
    {
        // Resources created on demand in Draw
    }

    private void ColorCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        var ds = args.DrawingSession;
        var cx = (float)(sender.ActualWidth / 2);
        var cy = (float)(sender.ActualHeight / 2);

        ds.Clear(Colors.Transparent);

        // Draw hue ring
        DrawHueRing(ds, cx, cy);

        // Draw saturation area with gamut mask
        DrawSaturationArea(ds, cx, cy);

        // Draw selection indicator
        DrawSelectionIndicator(ds, cx, cy);
    }

    private void DrawHueRing(CanvasDrawingSession ds, float cx, float cy)
    {
        const int segments = 60;
        for (int i = 0; i < segments; i++)
        {
            float startAngle = i * (360f / segments) - 90; // -90 to start at top
            float sweepAngle = 360f / segments + 0.5f;     // Slight overlap

            using var geometry = CreateArcGeometry(ds, cx, cy, OuterRadius, InnerRadius, startAngle, sweepAngle);

            // Color for this segment
            var hue = i * (360.0 / segments);
            var color = HsvToColor(hue, 1.0, 1.0);

            ds.FillGeometry(geometry, color);
        }
    }

    private void DrawSaturationArea(CanvasDrawingSession ds, float cx, float cy)
    {
        const int segments = 60;
        const int satSteps = 8;

        for (int i = 0; i < segments; i++)
        {
            float startAngle = i * (360f / segments) - 90;
            var hue = i * (360.0 / segments);
            var maxSat = GetMaxSaturation(hue);

            for (int s = 0; s < satSteps; s++)
            {
                float outerSatRadius = InnerRadius * (1 - (float)s / satSteps);
                float innerSatRadius = InnerRadius * (1 - (float)(s + 1) / satSteps);
                if (innerSatRadius < 2) innerSatRadius = 0;

                float saturation = (float)s / satSteps;
                var isInGamut = saturation <= maxSat;

                var color = HsvToColor(hue, saturation, 1.0);
                if (!isInGamut)
                {
                    // Gray out colors outside gamut
                    color = Color.FromArgb(100, color.R, color.G, color.B);
                }

                using var geometry = CreateArcGeometry(ds, cx, cy, outerSatRadius, innerSatRadius, startAngle, 360f / segments + 0.5f);
                ds.FillGeometry(geometry, color);
            }
        }

        // Draw center (white)
        ds.FillCircle(cx, cy, InnerRadius / satSteps, Colors.White);
    }

    private void DrawSelectionIndicator(CanvasDrawingSession ds, float cx, float cy)
    {
        // Calculate position based on hue and saturation
        double angleRad = (_selectedHue - 90) * Math.PI / 180;
        var maxSat = GetMaxSaturation(_selectedHue);
        var effectiveSat = Math.Min(_selectedSaturation, maxSat);

        float radius = (float)(InnerRadius * effectiveSat);
        float x = cx + radius * (float)Math.Cos(angleRad);
        float y = cy + radius * (float)Math.Sin(angleRad);

        // White circle with black outline
        ds.FillCircle(x, y, SelectorRadius, Colors.White);
        ds.DrawCircle(x, y, SelectorRadius, Colors.Black, 2);

        // Inner color indicator
        var color = HsvToColor(_selectedHue, effectiveSat, 1.0);
        ds.FillCircle(x, y, SelectorRadius - 2, color);
    }

    private CanvasGeometry CreateArcGeometry(CanvasDrawingSession ds, float cx, float cy,
        float outerRadius, float innerRadius, float startAngle, float sweepAngle)
    {
        using var builder = new CanvasPathBuilder(ds);

        float startRad = startAngle * (float)Math.PI / 180;
        float endRad = (startAngle + sweepAngle) * (float)Math.PI / 180;

        // Outer arc
        float outerStartX = cx + outerRadius * (float)Math.Cos(startRad);
        float outerStartY = cy + outerRadius * (float)Math.Sin(startRad);
        float outerEndX = cx + outerRadius * (float)Math.Cos(endRad);
        float outerEndY = cy + outerRadius * (float)Math.Sin(endRad);

        // Inner arc
        float innerStartX = cx + innerRadius * (float)Math.Cos(startRad);
        float innerStartY = cy + innerRadius * (float)Math.Sin(startRad);
        float innerEndX = cx + innerRadius * (float)Math.Cos(endRad);
        float innerEndY = cy + innerRadius * (float)Math.Sin(endRad);

        builder.BeginFigure(outerStartX, outerStartY);
        builder.AddArc(new Vector2(outerEndX, outerEndY), outerRadius, outerRadius, 0,
            CanvasSweepDirection.Clockwise, CanvasArcSize.Small);
        builder.AddLine(innerEndX, innerEndY);
        builder.AddArc(new Vector2(innerStartX, innerStartY), innerRadius, innerRadius, 0,
            CanvasSweepDirection.CounterClockwise, CanvasArcSize.Small);
        builder.EndFigure(CanvasFigureLoop.Closed);

        return CanvasGeometry.CreatePath(builder);
    }

    // Pointer handling
    private void ColorCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(ColorCanvas);
        _isDragging = true;
        ((UIElement)sender).CapturePointer(e.Pointer);

        HandlePointerInput(point.Position.X, point.Position.Y);
    }

    private void ColorCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging) return;
        var point = e.GetCurrentPoint(ColorCanvas);
        HandlePointerInput(point.Position.X, point.Position.Y);
    }

    private void ColorCanvas_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _isDragging = false;
        ((UIElement)sender).ReleasePointerCapture(e.Pointer);
    }

    private void HandlePointerInput(double x, double y)
    {
        var cx = ColorCanvas.ActualWidth / 2;
        var cy = ColorCanvas.ActualHeight / 2;

        double dx = x - cx;
        double dy = y - cy;
        double distance = Math.Sqrt(dx * dx + dy * dy);
        double angle = Math.Atan2(dy, dx) * 180 / Math.PI + 90;
        if (angle < 0) angle += 360;

        // Determine if in hue ring or saturation area
        if (distance >= InnerRadius && distance <= OuterRadius)
        {
            // Hue ring
            _selectedHue = angle;
            _isDraggingHue = true;
        }
        else if (distance < InnerRadius)
        {
            // Saturation area
            _selectedHue = angle;
            _selectedSaturation = distance / InnerRadius;
            _isDraggingHue = false;
        }

        // Clamp saturation to gamut
        var maxSat = GetMaxSaturation(_selectedHue);
        _selectedSaturation = Math.Min(_selectedSaturation, maxSat);

        // Update SelectedColor
        UpdateSelectedColor();
        ColorCanvas.Invalidate();
    }

    private void UpdateSelectedColor()
    {
        // HSV to HueColor (xy)
        var color = HsvToColor(_selectedHue, _selectedSaturation, 1.0);
        SelectedColor = HueColor.FromRgb(color.R, color.G, color.B);
        ColorChanged?.Invoke(this, SelectedColor);
    }

    private void BrightnessSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        // Brightness is separate from color in Hue API
        // This slider value would be used when sending to lights
        ColorChanged?.Invoke(this, SelectedColor);
    }

    public double Brightness => BrightnessSlider.Value / 100.0;

    private double GetMaxSaturation(double hue)
    {
        int index = (int)Math.Round(hue) % 360;
        if (index < 0) index += 360;
        return _maxSaturationCache[index];
    }

    // Color conversion helpers
    private static Color HsvToColor(double h, double s, double v)
    {
        double c = v * s;
        double x = c * (1 - Math.Abs((h / 60.0) % 2 - 1));
        double m = v - c;

        double r, g, b;
        if (h < 60) { r = c; g = x; b = 0; }
        else if (h < 120) { r = x; g = c; b = 0; }
        else if (h < 180) { r = 0; g = c; b = x; }
        else if (h < 240) { r = 0; g = x; b = c; }
        else if (h < 300) { r = x; g = 0; b = c; }
        else { r = c; g = 0; b = x; }

        return Color.FromArgb(255,
            (byte)((r + m) * 255),
            (byte)((g + m) * 255),
            (byte)((b + m) * 255));
    }

    private static void RgbToHsv(double r, double g, double b, out double h, out double s, out double v)
    {
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;

        v = max;
        s = max == 0 ? 0 : delta / max;

        if (delta == 0)
        {
            h = 0;
        }
        else if (max == r)
        {
            h = 60 * (((g - b) / delta) % 6);
        }
        else if (max == g)
        {
            h = 60 * (((b - r) / delta) + 2);
        }
        else
        {
            h = 60 * (((r - g) / delta) + 4);
        }

        if (h < 0) h += 360;
    }
}
