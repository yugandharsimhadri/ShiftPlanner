namespace ShiftPlanner.Mobile.Models;

/// <summary>GET/POST /api/leave-requests. Status is one of "Pending"/"Approved"/
/// "Rejected"/"Cancelled" (most teams auto-approve, so Pending is the exception, not
/// the norm — see Team.AutoApproveLeaveRequests).</summary>
public sealed class LeaveRequest
{
    public int Id { get; set; }
    public int TeamMemberId { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public string MemberCode { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string? Reason { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTimeOffset RequestedAt { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public string? DecisionNote { get; set; }
}

/// <summary>Body for POST /api/leave-requests.</summary>
public sealed class CreateLeaveRequestBody
{
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string? Reason { get; set; }
}

/// <summary>Body for POST /api/leave-requests/{id}/reject.</summary>
public sealed class DecideLeaveRequestBody
{
    public string? DecisionNote { get; set; }
}
