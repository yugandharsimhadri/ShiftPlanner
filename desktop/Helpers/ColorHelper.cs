using System.Windows.Media;

namespace ShiftPlanner.Desktop.Helpers;

public static class ColorHelper
{
    private static readonly Dictionary<string, SolidColorBrush> Cache = new();

    public static SolidColorBrush Brush(string? hex)
    {
        var key = string.IsNullOrWhiteSpace(hex) ? "#EEF1F0" : hex;
        if (Cache.TryGetValue(key, out var cached)) return cached;

        var color = (Color)ColorConverter.ConvertFromString(key)!;
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        Cache[key] = brush;
        return brush;
    }
}
