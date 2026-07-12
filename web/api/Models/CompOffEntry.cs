using System.Text.Json.Serialization;

namespace ShiftPlanner.Api.Models;

// One earned/used comp-off, e.g. "worked Saturday Jul 4th, took Wednesday Jul 8th off in
// exchange." Kept as its own ledger (rather than a flag on RosterEntry) so a comp-off can
// be tracked from the day it's earned to the day it's used, and so the utilization report
// can answer "who has pending comp-offs owed" without re-deriving it from roster history.
public class CompOffEntry
{
    public int Id { get; set; }
    public int TeamId { get; set; }

    public int TeamMemberId { get; set; }

    [JsonIgnore]
    public TeamMember? TeamMember { get; set; }

    // The default-off day they actually worked.
    public DateOnly EarnedDate { get; set; }

    public CompOffStatus Status { get; set; } = CompOffStatus.Pending;

    // The make-up day taken off in exchange — set only once Status is Used.
    public DateOnly? UsedDate { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
