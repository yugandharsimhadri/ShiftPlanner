using System.Text.Json.Serialization;

namespace ShiftPlanner.Api.Models;

// A one-directional "can someone take my shift" offer — the actual manual-roster pain
// point ("can someone cover my Saturday") — not a mutual two-way trade, which is a much
// bigger v2 feature. OfferedBy gives away one of their own upcoming assigned shifts,
// either to a specific TargetTeamMember or open to the whole team; another member claims
// it; an Editor/Admin approval is what actually moves the RosterEntry, keeping roster-edit
// authority with Editor+ even though the offer/claim itself is self-service.
public class ShiftSwapRequest
{
    public int Id { get; set; }
    public int TeamId { get; set; }

    public int OfferedByTeamMemberId { get; set; }

    [JsonIgnore]
    public TeamMember? OfferedByTeamMember { get; set; }

    public DateOnly Date { get; set; }
    public string ShiftCode { get; set; } = string.Empty;

    // Null means "open to any team member" rather than aimed at one specific person.
    public int? TargetTeamMemberId { get; set; }

    [JsonIgnore]
    public TeamMember? TargetTeamMember { get; set; }

    public int? ClaimedByTeamMemberId { get; set; }

    [JsonIgnore]
    public TeamMember? ClaimedByTeamMember { get; set; }

    public SwapStatus Status { get; set; } = SwapStatus.Open;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RespondedAt { get; set; }
    public string? DecidedByUserId { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
}
