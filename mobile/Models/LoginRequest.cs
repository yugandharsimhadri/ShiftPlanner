namespace ShiftPlanner.Mobile.Models;

/// <summary>
/// Body for POST /login. Matches the shape expected by ASP.NET Core Identity's
/// MapIdentityApi login endpoint on the Web app.
/// </summary>
public sealed class LoginRequest
{
    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}
