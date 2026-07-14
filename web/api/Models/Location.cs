namespace ShiftPlanner.Api.Models;

// A city/office a team member works out of. Team-scoped master list, seeded with major
// Indian cities per team and extendable by admins — same pattern as Track.
public class Location
{
    public int Id { get; set; }
    public int TeamId { get; set; }
    public string Name { get; set; } = string.Empty;
}
