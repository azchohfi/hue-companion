using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace HueWindows.Converters;

/// <summary>
/// Converts a boolean value to an opacity value (1.0 or 0.4).
/// </summary>
public class BoolToOpacityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is true ? 1.0 : 0.4;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a boolean value to Visibility (true = Visible, false = Collapsed).
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is true ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Inverts a boolean and converts to Visibility (true = Collapsed, false = Visible).
/// </summary>
public class InvertBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is true ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a count to Visibility (count > 0 = Visible, else Collapsed).
/// </summary>
public class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is int count)
        {
            return count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a string to bool (not null or empty = true).
/// </summary>
public class StringNotEmptyToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return !string.IsNullOrEmpty(value as string);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Inverts a boolean value.
/// </summary>
public class InvertBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is true ? false : true;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return value is true ? false : true;
    }
}

/// <summary>
/// Converts a hex color string (#RRGGBB) to a SolidColorBrush.
/// Returns transparent if the string is empty or invalid.
/// </summary>
public class HexToSolidColorBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string hex && !string.IsNullOrEmpty(hex) && hex.Length == 7 && hex[0] == '#')
        {
            try
            {
                var r = System.Convert.ToByte(hex.Substring(1, 2), 16);
                var g = System.Convert.ToByte(hex.Substring(3, 2), 16);
                var b = System.Convert.ToByte(hex.Substring(5, 2), 16);
                return new SolidColorBrush(Color.FromArgb(255, r, g, b));
            }
            catch
            {
                // Fall through to return transparent
            }
        }
        return new SolidColorBrush(Colors.Transparent);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a hex color string to Visibility.
/// Returns Visible if the string is a valid hex color, Collapsed otherwise.
/// </summary>
public class HexToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string hex && !string.IsNullOrEmpty(hex) && hex.Length == 7 && hex[0] == '#')
        {
            return Visibility.Visible;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a hex color string to a subtle gradient background brush.
/// Creates a diagonal gradient from the color at low opacity to transparent.
/// </summary>
public class HexToGradientBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string hex && !string.IsNullOrEmpty(hex) && hex.Length == 7 && hex[0] == '#')
        {
            try
            {
                var r = System.Convert.ToByte(hex.Substring(1, 2), 16);
                var g = System.Convert.ToByte(hex.Substring(3, 2), 16);
                var b = System.Convert.ToByte(hex.Substring(5, 2), 16);

                var gradient = new LinearGradientBrush
                {
                    StartPoint = new Windows.Foundation.Point(0, 0),
                    EndPoint = new Windows.Foundation.Point(1, 1)
                };
                gradient.GradientStops.Add(new GradientStop { Color = Color.FromArgb(40, r, g, b), Offset = 0 });
                gradient.GradientStops.Add(new GradientStop { Color = Color.FromArgb(15, r, g, b), Offset = 1 });
                return gradient;
            }
            catch
            {
                // Fall through to return default
            }
        }
        return new SolidColorBrush(Color.FromArgb(30, 255, 255, 255));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a hex color string to a gradient border brush for active indicators.
/// </summary>
public class HexToGradientBorderConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string hex && !string.IsNullOrEmpty(hex) && hex.Length == 7 && hex[0] == '#')
        {
            try
            {
                var r = System.Convert.ToByte(hex.Substring(1, 2), 16);
                var g = System.Convert.ToByte(hex.Substring(3, 2), 16);
                var b = System.Convert.ToByte(hex.Substring(5, 2), 16);

                return new SolidColorBrush(Color.FromArgb(255, r, g, b));
            }
            catch
            {
                // Fall through to return default
            }
        }
        return new SolidColorBrush(Colors.White);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
