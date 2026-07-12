using System.Text.Json.Serialization;

namespace ShiftPlanner.Api.Models;

public class Subtrack
{
    public int Id { get; set; }
    public int TeamId { get; set; }
    public int TrackId { get; set; }

    // Back-reference only; excluded from JSON to avoid duplicate-reference false positives
    // from ReferenceHandler.IgnoreCycles (see Program.cs) — callers already have TrackId.
    [JsonIgnore]
    public Track? Track { get; set; }
    public string Name { get; set; } = string.Empty;
}
