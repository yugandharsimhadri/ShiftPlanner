using System.Text.Json.Serialization;

namespace ShiftPlanner.Api.Models;

// No LeadName here on purpose — Lead/Co-Lead is a single team-wide designation
// (TeamMember.IsTeamLead/IsCoLead, configured from Team Settings), not a per-track
// free-text field. A track used to carry its own lead name, which meant "the team's
// leadership" was scattered across however many tracks existed instead of being one
// clear answer.
public class Track
{
    public int Id { get; set; }
    public int TeamId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#4453AD";

    public List<Subtrack> Subtracks { get; set; } = new();

    // Back-reference only, not needed in API output; excluded to avoid duplicate-reference
    // false positives from ReferenceHandler.IgnoreCycles (see Program.cs).
    [JsonIgnore]
    public List<TeamMember> TeamMembers { get; set; } = new();
}
