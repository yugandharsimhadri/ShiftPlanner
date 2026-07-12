namespace ShiftPlanner.Mobile.ViewModels;

/// <summary>One employee's row in the Roster tab's day view.</summary>
public sealed class RosterEmployeeRowViewModel
{
    public required Guid EmployeeId { get; init; }
    public required string EmployeeCode { get; init; }
    public required string EmployeeName { get; init; }
    public string? ShiftCode { get; init; }
    public string ShiftLabel { get; init; } = "Off";
    public bool HasShift { get; init; }
    public Color ChipForeground { get; init; } = Colors.Transparent;
    public Color ChipBackground { get; init; } = Colors.Transparent;
    public bool IsMe { get; init; }
}

/// <summary>A track's worth of rows, for the Roster CollectionView's grouping.</summary>
public sealed class RosterTrackGroup : List<RosterEmployeeRowViewModel>
{
    public string TrackName { get; }

    public RosterTrackGroup(string trackName, IEnumerable<RosterEmployeeRowViewModel> items) : base(items)
    {
        TrackName = trackName;
    }
}
