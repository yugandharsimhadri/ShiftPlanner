namespace ShiftPlanner.Mobile.Models;

/// <summary>GET /api/teams/current/managers/search?phone= — candidates eligible to
/// become a manager of the current team (anyone on a team this Admin also administers).</summary>
public sealed class PersonSearchResult
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
}

/// <summary>GET/POST /api/teams/current/managers — who currently has read-only
/// cross-team oversight of this team's live availability.</summary>
public sealed class ManagerAssignment
{
    public int Id { get; set; }
    public Guid PersonId { get; set; }
    public string PersonName { get; set; } = string.Empty;
    public string PersonPhone { get; set; } = string.Empty;
    public int TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;
}

/// <summary>Body for POST /api/teams/current/managers.</summary>
public sealed class GrantManagerBody
{
    public Guid PersonId { get; set; }
}

/// <summary>GET /api/manager/teams — every team the signed-in person manages, not
/// team-scoped (ignores X-Team-Id).</summary>
public sealed class ManagerTeam
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

/// <summary>GET /api/manager/availability — one entry per managed team.</summary>
public sealed class ManagerTeamAvailability
{
    public int TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public List<TeamMemberAvailability> Members { get; set; } = new();
}
