namespace ShiftPlanner.Mobile.Models;

/// <summary>Body for POST /api/roster/bulk-entry — assigns the same shift to every
/// (member, date) combination in the cross-product.</summary>
public sealed class BulkRosterEntryBody
{
    public List<int> TeamMemberIds { get; set; } = new();
    public List<DateOnly> Dates { get; set; } = new();
    public string? ShiftCode { get; set; }
}

/// <summary>Body for POST /api/roster/apply-pattern — a per-weekday shift pattern
/// applied across a whole month for the given members. Dictionary keys serialize as
/// enum names ("Monday", "Tuesday", ...).</summary>
public sealed class ApplyPatternBody
{
    public int Year { get; set; }
    public int Month { get; set; }
    public List<int> TeamMemberIds { get; set; } = new();
    public Dictionary<DayOfWeek, string?> WeeklyPattern { get; set; } = new();
    public bool SkipInactive { get; set; } = true;
}

/// <summary>Response shared by bulk-entry and apply-pattern.</summary>
public sealed class BulkEntryResult
{
    public int UpdatedCount { get; set; }
    public List<string> Errors { get; set; } = new();
}

/// <summary>GET /api/roster/publish-status, POST /publish, POST /unpublish.</summary>
public sealed class RosterPublishStatus
{
    public bool IsPublished { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
}

/// <summary>GET /api/roster/history?year=&amp;month= — a lightweight audit log of
/// roster-cell changes for the visible month.</summary>
public sealed class RosterEntryHistoryRow
{
    public int Id { get; set; }
    public int TeamMemberId { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public string? OldShiftCode { get; set; }
    public string? NewShiftCode { get; set; }
    public string ChangedByUserId { get; set; } = string.Empty;
    public DateTimeOffset ChangedAt { get; set; }
    public string Source { get; set; } = string.Empty;
}
