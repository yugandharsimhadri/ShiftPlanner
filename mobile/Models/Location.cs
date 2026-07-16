namespace ShiftPlanner.Mobile.Models;

/// <summary>A city/office a team member can be based out of — GET/POST /api/locations.</summary>
public sealed class Location
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
