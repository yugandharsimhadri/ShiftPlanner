using System.Text.Json.Serialization;

namespace ShiftPlanner.Api.Models;

// A team member's self-service request for time off across a date range. Separate from
// CompOffEntry (that's a specific worked-a-default-off-day/took-a-make-up-day ledger) —
// this is general leave/PTO/sick-leave tracking, which didn't exist before. Approved
// requests are surfaced on the roster (see RosterEndpoints) so a blank cell can say
// "Leave" instead of looking identical to "nobody's assigned this yet."
public class LeaveRequest
{
    public int Id { get; set; }
    public int TeamId { get; set; }

    public int TeamMemberId { get; set; }

    [JsonIgnore]
    public TeamMember? TeamMember { get; set; }

    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string? Reason { get; set; }

    public LeaveStatus Status { get; set; } = LeaveStatus.Pending;

    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? DecidedByUserId { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public string? DecisionNote { get; set; }
}
