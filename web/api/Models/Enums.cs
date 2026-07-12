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

public enum MembershipStatus
{
    // Added by an admin via email, but nobody has logged in with that email yet.
    Invited,
    Active
}
