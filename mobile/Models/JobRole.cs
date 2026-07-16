namespace ShiftPlanner.Mobile.Models;

/// <summary>A job title/role a team member can be assigned — GET/POST /api/job-roles.</summary>
public sealed class JobRole
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
