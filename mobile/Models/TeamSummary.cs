namespace ShiftPlanner.Mobile.Models;

/// <summary>One team the signed-in account belongs to, as returned by GET /api/teams/mine.</summary>
public sealed class TeamSummary
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>"Admin", "Editor", or "Viewer".</summary>
    public string Role { get; set; } = string.Empty;
}
