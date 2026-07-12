namespace ShiftPlanner.Api.Models;

public enum EmploymentType
{
    FullTime,
    PartTime
}

public enum EmployeeStatus
{
    Active,
    Inactive
}

public enum RosterEntrySource
{
    Manual,
    Copied
}

public enum TeamRole
{
    Viewer,
    Editor,
    Admin
}

public enum CompOffStatus
{
    // Earned (worked a default-off day) but not yet taken against a make-up day.
    Pending,
    // Consumed against a specific make-up day off.
    Used
}
