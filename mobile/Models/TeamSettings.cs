namespace ShiftPlanner.Mobile.Models;

/// <summary>GET /api/teams/current/settings. Mobile only lets you edit the auto-approve
/// toggles and Lead/Co-Lead (via the team-members endpoints) — Name/Org/OffDays/CompOff
/// stay Web-only for now, but are still read here so a save round-trips them unchanged
/// instead of blanking them out (the backend PUT expects the full shape every time).</summary>
public sealed class TeamSettings
{
    public string Name { get; set; } = string.Empty;
    public string? OrgName { get; set; }
    public int? TeamStrength { get; set; }
    public string? ShiftsCovered { get; set; }
    public List<string> DefaultOffDays { get; set; } = new();
    public bool CompOffsEnabled { get; set; }
    public int ActiveMemberCount { get; set; }
    public bool AutoApproveLeaveRequests { get; set; } = true;
    public bool AutoApproveShiftSwaps { get; set; } = true;
}

/// <summary>Body for PUT /api/teams/current/settings.</summary>
public sealed class UpdateTeamSettingsRequest
{
    public string Name { get; set; } = string.Empty;
    public string? OrgName { get; set; }
    public int? TeamStrength { get; set; }
    public string? ShiftsCovered { get; set; }
    public List<string> DefaultOffDays { get; set; } = new();
    public bool CompOffsEnabled { get; set; }
    public bool AutoApproveLeaveRequests { get; set; }
    public bool AutoApproveShiftSwaps { get; set; }
}
