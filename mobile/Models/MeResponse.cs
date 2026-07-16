namespace ShiftPlanner.Mobile.Models;

/// <summary>The signed-in person's own TeamMember record on the current team, as returned
/// by GET /api/teams/current/members/me.</summary>
public sealed class MeResponse
{
    public Guid PersonId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;

    /// <summary>"Viewer", "Editor", or "Admin".</summary>
    public string Role { get; set; } = string.Empty;
    public bool IsTeamLead { get; set; }
    public bool IsCoLead { get; set; }
}
