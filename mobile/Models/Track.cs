namespace ShiftPlanner.Mobile.Models;

// No LeadName here on purpose — Lead/Co-Lead is a single team-wide designation
// (set from Team Settings), not a per-track free-text field.
public sealed class Track
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#2F7D6B";
    public List<Subtrack> Subtracks { get; set; } = new();
}

public sealed class Subtrack
{
    public int Id { get; set; }
    public int TrackId { get; set; }
    public string Name { get; set; } = string.Empty;
}

/// <summary>Body for POST/PUT /api/tracks.</summary>
public sealed class TrackInput
{
    public int? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#2F7D6B";
}

/// <summary>Body for POST/PUT /api/subtracks.</summary>
public sealed class SubtrackInput
{
    public int? Id { get; set; }
    public int TrackId { get; set; }
    public string Name { get; set; } = string.Empty;
}
