namespace ShiftPlanner.Mobile.ViewModels;

/// <summary>One row on the Employees tab's list.</summary>
public sealed class EmployeeListItemViewModel
{
    public required Guid Id { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public string TrackName { get; init; } = string.Empty;
    public required string Status { get; init; }
    public bool IsActive => Status == "Active";
    public bool IsInactive => !IsActive;
}
