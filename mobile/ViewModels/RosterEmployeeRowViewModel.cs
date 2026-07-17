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

    /// <summary>True when this member has an approved leave request covering this date —
    /// used both for the chip's no-shift context label and to warn before assigning a
    /// shift on top of it.</summary>
    public bool IsOnLeave { get; init; }

    /// <summary>The roster entry id and whether it's already acknowledged — null entry
    /// id means there's nothing to acknowledge (no shift assigned that day).</summary>
    public int? EntryId { get; init; }
    public bool CanAcknowledge { get; init; }
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
