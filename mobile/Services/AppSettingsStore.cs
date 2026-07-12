namespace ShiftPlanner.Mobile.Services;

/// <summary>
/// Small, non-secret app settings persisted with MAUI's <see cref="Preferences"/> API.
/// This is a self-hosted/demo tool, so the server address is something the user types
/// in themselves (e.g. http://192.168.1.50:5000) rather than something baked into the app.
/// </summary>
public static class AppSettingsStore
{
    private const string ApiBaseUrlKey = "shiftplanner.api_base_url";
    private const string EmployeeCodeKey = "shiftplanner.employee_code";
    private const string UserEmailKey = "shiftplanner.user_email";
    private const string CurrentTeamIdKey = "shiftplanner.current_team_id";
    private const string CurrentTeamNameKey = "shiftplanner.current_team_name";
    private const string CurrentTeamRoleKey = "shiftplanner.current_team_role";

    private const string DefaultApiBaseUrl = "http://localhost:5000";

    /// <summary>Base address of the ShiftPlanner Web API, e.g. "http://192.168.1.50:5000".</summary>
    public static string ApiBaseUrl
    {
        get => Preferences.Default.Get(ApiBaseUrlKey, DefaultApiBaseUrl);
        set => Preferences.Default.Set(ApiBaseUrlKey, (value ?? string.Empty).Trim());
    }

    /// <summary>
    /// The signed-in person's own employee code on this team (e.g. "EMP-004"), so "My Shifts"
    /// knows which roster entries are theirs. There's no server-side link between a login and
    /// a roster row, so this is opt-in — until set, My Shifts shows an empty state pointing
    /// here instead of guessing.
    /// </summary>
    public static string? EmployeeCode
    {
        get
        {
            var value = Preferences.Default.Get(EmployeeCodeKey, string.Empty);
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        set => Preferences.Default.Set(EmployeeCodeKey, (value ?? string.Empty).Trim());
    }

    /// <summary>Email address used for the last successful login; shown on the Profile page.</summary>
    public static string? UserEmail
    {
        get
        {
            var value = Preferences.Default.Get(UserEmailKey, string.Empty);
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        set => Preferences.Default.Set(UserEmailKey, value ?? string.Empty);
    }

    /// <summary>
    /// The team every team-scoped request is sent for (as the X-Team-Id header). Null means
    /// no team has been picked yet — the app should route to the team picker.
    /// </summary>
    public static int? CurrentTeamId
    {
        get
        {
            var value = Preferences.Default.Get(CurrentTeamIdKey, 0);
            return value > 0 ? value : null;
        }
        set => Preferences.Default.Set(CurrentTeamIdKey, value ?? 0);
    }

    /// <summary>Cached alongside CurrentTeamId purely so the UI has something to show
    /// immediately, without waiting on a network round trip.</summary>
    public static string? CurrentTeamName
    {
        get
        {
            var value = Preferences.Default.Get(CurrentTeamNameKey, string.Empty);
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        set => Preferences.Default.Set(CurrentTeamNameKey, value ?? string.Empty);
    }

    /// <summary>The signed-in person's role on CurrentTeamId ("Admin" / "Editor" / "Viewer").</summary>
    public static string? CurrentTeamRole
    {
        get
        {
            var value = Preferences.Default.Get(CurrentTeamRoleKey, string.Empty);
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        set => Preferences.Default.Set(CurrentTeamRoleKey, value ?? string.Empty);
    }

    /// <summary>True if the signed-in person can create/edit/delete on the current team
    /// (Editor or Admin) — mirrors the server's RequireTeamEditor() check.</summary>
    public static bool CurrentTeamCanEdit => CurrentTeamRole is "Editor" or "Admin";

    /// <summary>True only for Admin — team/member management, mirrors RequireTeamAdmin().</summary>
    public static bool CurrentTeamIsAdmin => CurrentTeamRole == "Admin";

    public static void SetCurrentTeam(int id, string name, string role)
    {
        CurrentTeamId = id;
        CurrentTeamName = name;
        CurrentTeamRole = role;
    }

    /// <summary>Clears per-session data on logout. The server address is left alone.</summary>
    public static void ClearSession()
    {
        Preferences.Default.Remove(UserEmailKey);
        Preferences.Default.Remove(CurrentTeamIdKey);
        Preferences.Default.Remove(CurrentTeamNameKey);
        Preferences.Default.Remove(CurrentTeamRoleKey);
    }
}
