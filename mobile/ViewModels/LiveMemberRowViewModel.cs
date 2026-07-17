namespace ShiftPlanner.Mobile.ViewModels;

/// <summary>One team member's row in the Live tab's availability list.</summary>
public sealed class LiveMemberRowViewModel
{
    public required int TeamMemberId { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public string? TrackName { get; init; }
    public bool IsAvailable { get; init; }
    public string StatusLabel { get; init; } = "Not available";
    public Color StatusColor { get; init; } = Colors.Transparent;
    public Color StatusBackground { get; init; } = Colors.Transparent;
}
