namespace ShiftPlanner.Mobile.Services;

/// <summary>Every REST route the app calls, in one place.</summary>
public static class ApiRoutes
{
    /// <summary>POST here with { email, password } to get a bearer token (ASP.NET Core Identity's MapIdentityApi, mounted at /api).</summary>
    public const string Login = "api/login";

    /// <summary>POST here with { email, password } to create a new account.</summary>
    public const string Register = "api/register";

    /// <summary>GET the signed-in account's teams, with their role in each.</summary>
    public const string TeamsMine = "api/teams/mine";

    /// <summary>POST { name } to create a team — the caller becomes its Admin.</summary>
    public const string Teams = "api/teams";

    /// <summary>GET the caller's own role and TeamMember record on the current team.</summary>
    public const string MembersMe = "api/teams/current/members/me";

    /// <summary>GET ?year=&amp;month= — the whole team's roster for that month (requires X-Team-Id).</summary>
    public const string Roster = "api/roster";

    /// <summary>PUT here to assign/change/clear one team member's shift on one date.</summary>
    public const string RosterEntry = "api/roster/entry";

    /// <summary>POST here to copy a month's roster forward onto another month.</summary>
    public const string RosterCopyForward = "api/roster/copy-forward";

    /// <summary>GET/POST — team members (merges what used to be separate Employees and
    /// Membership). PUT/DELETE {id} for one, PATCH {id}/role to change access.</summary>
    public const string Members = "api/teams/current/members";

    /// <summary>GET — a suggested next team member code, e.g. "EMP-004".</summary>
    public const string MembersNextCode = "api/teams/current/members/next-code";

    /// <summary>GET/POST — team's tracks (with nested subtracks). PUT/DELETE {id} for one track.</summary>
    public const string Tracks = "api/tracks";

    /// <summary>POST — add a subtrack under a track. DELETE {id} to remove one.</summary>
    public const string Subtracks = "api/subtracks";

    /// <summary>GET/POST — team's job-role master list. DELETE {id} to remove one.</summary>
    public const string JobRoles = "api/job-roles";

    /// <summary>GET/POST — team's location master list. DELETE {id} to remove one.</summary>
    public const string Locations = "api/locations";

    /// <summary>GET/POST — team's shift types. PUT/DELETE {id} for one shift type.</summary>
    public const string ShiftTypes = "api/shift-types";

    /// <summary>GET ?year=&amp;month= — the month's roster as an .xlsx file.</summary>
    public const string ExportExcel = "api/export/excel";

    /// <summary>GET ?year=&amp;month= — the month's roster as a .csv file.</summary>
    public const string ExportCsv = "api/export/csv";
}
