namespace ShiftPlanner.Mobile.Models;

/// <summary>One row from GET /api/teams/current/members — the single merged concept that
/// replaced the old separate "Employee" (roster record) and "Membership" (login access).
/// A TeamMember row is both at once now.</summary>
public sealed class TeamMember
{
    public int Id { get; set; }
    public Guid PersonId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool HasLogin { get; set; }
    public string Code { get; set; } = string.Empty;
    public int? TrackId { get; set; }
    public string? TrackName { get; set; }
    public int? SubtrackId { get; set; }
    public string? SubtrackName { get; set; }
    public int? JobRoleId { get; set; }
    public string? JobRoleName { get; set; }
    public int? LocationId { get; set; }
    public string? LocationName { get; set; }

    /// <summary>"FullTime" or "PartTime".</summary>
    public string EmploymentType { get; set; } = "FullTime";
    public DateOnly JoinDate { get; set; }

    /// <summary>"Active" or "Inactive".</summary>
    public string Status { get; set; } = "Active";
    public string? Notes { get; set; }

    /// <summary>"Viewer", "Editor", or "Admin" — what this person can do on this team.</summary>
    public string AccessRole { get; set; } = "Viewer";
    public bool IsTeamLead { get; set; }
    public bool IsCoLead { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Body for POST /api/teams/current/members. TeamIds is always just the current
/// team for Mobile's add form — Web's multi-team assignment isn't exposed here.</summary>
public sealed class CreateTeamMemberRequest
{
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Notes { get; set; }
    public string Code { get; set; } = string.Empty;
    public int? TrackId { get; set; }
    public int? SubtrackId { get; set; }
    public int? JobRoleId { get; set; }
    public int? LocationId { get; set; }
    public string EmploymentType { get; set; } = "FullTime";
    public DateOnly JoinDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public string Status { get; set; } = "Active";
    public string AccessRole { get; set; } = "Viewer";
    public List<int> TeamIds { get; set; } = new();
}

/// <summary>Body for PUT /api/teams/current/members/{id}.</summary>
public sealed class UpdateTeamMemberRequest
{
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Notes { get; set; }
    public string Code { get; set; } = string.Empty;
    public int? TrackId { get; set; }
    public int? SubtrackId { get; set; }
    public int? JobRoleId { get; set; }
    public int? LocationId { get; set; }
    public string EmploymentType { get; set; } = "FullTime";
    public DateOnly JoinDate { get; set; }
    public string Status { get; set; } = "Active";
    public string AccessRole { get; set; } = "Viewer";
}

/// <summary>Body for PATCH /api/teams/current/members/{id}/role.</summary>
public sealed class UpdateMemberRoleRequest
{
    public string AccessRole { get; set; } = "Viewer";
}
