namespace ShiftPlanner.Mobile.ViewModels;

/// <summary>One row on the Team Members tab's list — merges what used to be the separate
/// Employees list and Team (login/role) list into a single view of a TeamMember row.</summary>
public sealed class TeamMemberListItemViewModel
{
    public required int Id { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public string TrackName { get; init; } = string.Empty;
    public string JobRoleName { get; init; } = string.Empty;
    public string LocationName { get; init; } = string.Empty;
    public required string Status { get; init; }
    public required string AccessRole { get; init; }
    public bool HasLogin { get; init; }
    public bool IsActive => Status == "Active";
    public bool IsInactive => !IsActive;
    public string LoginLabel => HasLogin ? "Has login" : "No login yet";
}
