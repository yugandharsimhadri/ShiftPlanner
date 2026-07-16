namespace ShiftPlanner.Api.Models;

// Grants a Person read-only oversight of one team's live availability dashboard, without
// making them a TeamMember (or an Admin) on that team. Deliberately separate from
// AccessRole — someone can manage the "who's free right now" view across several teams
// without gaining any roster-edit rights on teams that aren't theirs.
public class ManagerAssignment
{
    public int Id { get; set; }

    public Guid PersonId { get; set; }
    public Person? Person { get; set; }

    public int TeamId { get; set; }
    public Team? Team { get; set; }

    public string GrantedByUserId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
