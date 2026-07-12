namespace ShiftPlanner.Api.Models;

public class ShiftType
{
    // Surrogate PK — Code is only unique per team now, not globally, so it can no
    // longer be the primary key (see Team.cs / multi-tenancy).
    public int Id { get; set; }
    public int TeamId { get; set; }

    // e.g. "M", "E", "N", "OFF", "LV" — unique within the team (see AppDbContext).
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public TimeOnly? Start { get; set; }
    public TimeOnly? End { get; set; }
    public string Color { get; set; } = "#4453AD";
    public bool IsOvernight { get; set; }

    // True for actual worked shifts (Morning/Evening/Night); false for absence codes
    // like "Off" or "Leave". Drives comp-off auto-earn: assigning a work shift on a
    // default-off day earns a comp-off, assigning a non-work code never does.
    public bool IsWorkShift { get; set; } = true;
}
