namespace ShiftPlanner.Mobile.Models;

/// <summary>GET/POST /api/shift-swaps. A one-directional "someone else takes my shift"
/// offer, not a mutual trade. Status is one of "Open"/"Claimed"/"Approved"/"Rejected"/
/// "Cancelled" (most teams auto-approve on claim — see Team.AutoApproveShiftSwaps).</summary>
public sealed class ShiftSwapRequest
{
    public int Id { get; set; }
    public int OfferedByTeamMemberId { get; set; }
    public string OfferedByName { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public string ShiftCode { get; set; } = string.Empty;
    public int? TargetTeamMemberId { get; set; }
    public string? TargetName { get; set; }
    public int? ClaimedByTeamMemberId { get; set; }
    public string? ClaimedByName { get; set; }
    public string Status { get; set; } = "Open";
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Body for POST /api/shift-swaps.</summary>
public sealed class CreateShiftSwapBody
{
    public DateOnly Date { get; set; }
    public string ShiftCode { get; set; } = string.Empty;
    public int? TargetTeamMemberId { get; set; }
}
