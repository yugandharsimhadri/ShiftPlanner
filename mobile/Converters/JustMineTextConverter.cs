using System.Globalization;

namespace ShiftPlanner.Mobile.Converters;

/// <summary>Roster's All/Just-me toggle button label.</summary>
public sealed class JustMineTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "Showing: Just me — tap for All" : "Showing: All — tap for Just me";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
