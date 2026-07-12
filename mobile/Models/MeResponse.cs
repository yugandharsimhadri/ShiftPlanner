namespace ShiftPlanner.Mobile.Models;

/// <summary>The signed-in person's own membership on the current team, as returned by
/// GET /api/teams/current/members/me.</summary>
public sealed class MeResponse
{
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public Guid? EmployeeId { get; set; }
    public string? EmployeeCode { get; set; }
}
