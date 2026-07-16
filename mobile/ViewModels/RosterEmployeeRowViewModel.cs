namespace ShiftPlanner.Mobile.ViewModels;

/// <summary>One team member's row in the Roster tab's day view.</summary>
public sealed class RosterEmployeeRowViewModel
{
    public required int TeamMemberId { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
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
