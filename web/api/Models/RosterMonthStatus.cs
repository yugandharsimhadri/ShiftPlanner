namespace ShiftPlanner.Api.Models;

// Draft-vs-published state for one team's one month. Absence of a row for a given
// (TeamId, Year, Month) means "draft" — a row only gets created the first time someone
// publishes that month. Editors/Admins always see the full roster regardless of this;
// it only gates what Viewers see (RosterEndpoints withholds Entries for them until
// published).
public class RosterMonthStatus
{
    public int Id { get; set; }
    public int TeamId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }

    public bool IsPublished { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public string? PublishedByUserId { get; set; }
}
