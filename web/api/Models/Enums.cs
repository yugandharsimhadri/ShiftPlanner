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

public enum LeaveStatus
{
    Pending,
    Approved,
    Rejected,
    Cancelled
}

public enum SwapStatus
{
    // Offered, nobody has claimed it yet.
    Open,
    // Claimed by another member, awaiting Editor/Admin approval.
    Claimed,
    // Approved — the roster entry has been reassigned.
    Approved,
    Rejected,
    Cancelled
}
