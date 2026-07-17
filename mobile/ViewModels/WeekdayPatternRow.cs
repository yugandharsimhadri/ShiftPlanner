using CommunityToolkit.Mvvm.ComponentModel;

namespace ShiftPlanner.Mobile.ViewModels;

/// <summary>One weekday's shift choice in Apply Pattern's 7-row grid.</summary>
public sealed partial class WeekdayPatternRow : ObservableObject
{
    public required DayOfWeek Day { get; init; }
    public required string DayLabel { get; init; }

    [ObservableProperty]
    private string? selectedShiftCode;
}
