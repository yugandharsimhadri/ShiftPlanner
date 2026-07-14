namespace ShiftPlanner.Api.Models;

// A job title/role, e.g. "Cashier" or "Team Lead". Team-scoped master list, seeded with a
// handful of common roles per team and extendable by admins — same pattern as Track.
public class JobRole
{
    public int Id { get; set; }
    public int TeamId { get; set; }
    public string Name { get; set; } = string.Empty;
}
