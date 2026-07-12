namespace ShiftPlanner.Mobile.Models;

/// <summary>One row from GET /api/teams/current/members.</summary>
public sealed class Membership
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;

    /// <summary>"Viewer", "Editor", or "Admin".</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>"Invited" or "Active".</summary>
    public string Status { get; set; } = string.Empty;
    public Guid? EmployeeId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Body for POST /api/teams/current/members.</summary>
public sealed class AddMemberRequest
{
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = "Viewer";
}

/// <summary>Body for PATCH /api/teams/current/members/{id}.</summary>
public sealed class UpdateMemberRoleRequest
{
    public string Role { get; set; } = "Viewer";
}

/// <summary>Body for PATCH /api/teams/current/members/{id}/employee.</summary>
public sealed class LinkEmployeeRequest
{
    public Guid? EmployeeId { get; set; }
}
