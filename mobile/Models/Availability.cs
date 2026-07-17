namespace ShiftPlanner.Mobile.Models;

/// <summary>GET /api/teams/current/availability and PATCH .../members/me/availability.
/// A member's self-reported "free right now" status — independent of the planned
/// roster, auto-expires server-side per the person's configured window.</summary>
public sealed class TeamMemberAvailability
{
    public int TeamMemberId { get; set; }
    public Guid PersonId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? TrackName { get; set; }
    public bool IsAvailable { get; set; }
    public DateTimeOffset? AvailableSince { get; set; }
    public string? Timezone { get; set; }
}

/// <summary>Body for PATCH /api/teams/current/members/me/availability.</summary>
public sealed class UpdateAvailabilityBody
{
    public bool IsAvailable { get; set; }
}
