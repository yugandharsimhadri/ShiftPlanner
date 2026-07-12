using System.Text.Json.Serialization;

namespace ShiftPlanner.Api.Models;

public class Track
{
    public int Id { get; set; }
    public int TeamId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? LeadName { get; set; }
    public string Color { get; set; } = "#4453AD";

    public List<Subtrack> Subtracks { get; set; } = new();

    // Back-reference only, not needed in API output; excluded to avoid duplicate-reference
    // false positives from ReferenceHandler.IgnoreCycles (see Program.cs).
    [JsonIgnore]
    public List<Employee> Employees { get; set; } = new();
}
