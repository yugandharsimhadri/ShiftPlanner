namespace ShiftPlanner.Mobile.Models;

/// <summary>One row of GET /api/reports/utilization?start=&amp;end= — how much each
/// active member worked in the range, plus their current comp-off standing.</summary>
public sealed class UtilizationRow
{
    public int TeamMemberId { get; set; }
    public string MemberCode { get; set; } = string.Empty;
    public string MemberName { get; set; } = string.Empty;
    public string? TrackName { get; set; }
    public int TotalShiftsWorked { get; set; }
    public int WeekendShiftsWorked { get; set; }
    public int CompOffsEarned { get; set; }
    public int CompOffsUsed { get; set; }
    public int CompOffsPending { get; set; }
}
