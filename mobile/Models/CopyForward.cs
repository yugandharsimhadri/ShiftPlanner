namespace ShiftPlanner.Mobile.Models;

/// <summary>Body for POST /api/roster/copy-forward.</summary>
public sealed class CopyForwardRequestBody
{
    public int SourceYear { get; set; }
    public int SourceMonth { get; set; }
    public int TargetYear { get; set; }
    public int TargetMonth { get; set; }

    /// <summary>"weekday" or "exact-date".</summary>
    public string Pattern { get; set; } = "weekday";
    public bool SkipInactive { get; set; } = true;
}

public sealed class CopyForwardFlag
{
    public int TeamMemberId { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public sealed class CopyForwardResult
{
    public int CopiedCount { get; set; }
    public List<CopyForwardFlag> Flagged { get; set; } = new();
}

/// <summary>Body for PUT /api/roster/entry.</summary>
public sealed class RosterEntryUpsertRequest
{
    public int TeamMemberId { get; set; }
    public DateOnly Date { get; set; }
    public string? ShiftCode { get; set; }
    public string? Note { get; set; }
}
