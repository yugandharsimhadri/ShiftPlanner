using System.Text.Json.Serialization;

namespace ShiftPlanner.Api.Models;

// One person's presence on one team — the single source of truth for both "who has
// access to this team's roster" and "who is on this team's roster," since those used
// to be two separate concepts (TeamMembership + Employee) that were almost always the
// same people anyway. A person on multiple teams gets one TeamMember row per team.
public class TeamMember
{
    public int Id { get; set; }

    public int TeamId { get; set; }

    [JsonIgnore]
    public Team? Team { get; set; }

    public Guid PersonId { get; set; }
    public Person? Person { get; set; }

    // The employer's own reference code (e.g. their payroll/HR system's employee
    // number). Editable, unique per team only — not globally.
    public string Code { get; set; } = string.Empty;

    public int? TrackId { get; set; }
    public Track? Track { get; set; }
    public int? SubtrackId { get; set; }
    public Subtrack? Subtrack { get; set; }

    // Job title, e.g. "Cashier" — distinct from AccessRole below. Picked from the team's
    // JobRole master list rather than free text, so it doesn't drift into typo variants.
    public int? JobRoleId { get; set; }
    public JobRole? JobRole { get; set; }

    // Site/office/city they work out of on this team, e.g. "Bangalore". Picked from the
    // team's Location master list for the same reason.
    public int? LocationId { get; set; }
    public Location? Location { get; set; }
    public EmploymentType EmploymentType { get; set; }
    public DateOnly JoinDate { get; set; }

    // Employment status on this team (Active/Inactive) — independent of whether they
    // have a login at all (see Person.UserId).
    public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;
    public string? Notes { get; set; }

    // What they can do on this team's roster — Viewer/Editor/Admin. Meaningless until
    // they actually have a login (Person.UserId), but set from the start so it's ready
    // the moment they do.
    public TeamRole AccessRole { get; set; }

    // Labels on top of Admin, not separate permission tiers. Exactly one TeamMember per
    // team has IsTeamLead; at most one has IsCoLead. Enforced in TeamsEndpoints.
    public bool IsTeamLead { get; set; }
    public bool IsCoLead { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
