using System.Text.Json.Serialization;

namespace ShiftPlanner.Api.Models;

public class TeamMembership
{
    public int Id { get; set; }

    public int TeamId { get; set; }

    [JsonIgnore]
    public Team? Team { get; set; }

    // Null until someone logs in with a matching email — see RequireTeamFilter,
    // which claims pending invites by email on every authenticated request.
    public string? UserId { get; set; }

    // Always set. This is how a person with the same email in two teams gets
    // linked to both: each team's admin adds them by email independently, and
    // whichever account later authenticates with that email claims the row.
    public string Email { get; set; } = string.Empty;

    public TeamRole Role { get; set; }
    public MembershipStatus Status { get; set; } = MembershipStatus.Invited;

    // Optional link to a roster Employee record in the same team, so a login
    // can be tied to "this is Priya Nair" rather than just an email address.
    public Guid? EmployeeId { get; set; }

    // Labels on top of the Admin role, not separate permission tiers — a Lead
    // or Co-Lead is always an Admin underneath, so no extra role-check logic
    // is needed anywhere else. Exactly one membership per team has IsTeamLead
    // (the creator, until transferred); at most one has IsCoLead. Enforced in
    // TeamsEndpoints, not at the database level.
    public bool IsTeamLead { get; set; }
    public bool IsCoLead { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
