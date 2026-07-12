using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ShiftPlanner.Api.Data;
using ShiftPlanner.Api.Models;

namespace ShiftPlanner.Api.Services;

// Runs before any tenant-scoped endpoint. Requires an authenticated user, an
// "X-Team-Id" header naming a team, and an active membership in that team —
// and if minRole is set, that the membership's role meets it. Also claims any
// pending (email-only) invites for the caller's email before checking, which is
// how "the same email was added to two teams" resolves into two memberships on
// whichever account later logs in with that email.
public sealed class RequireTeamFilter : IEndpointFilter
{
    private readonly TeamRole? _minRole;

    public RequireTeamFilter(TeamRole? minRole = null) => _minRole = minRole;

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var db = http.RequestServices.GetRequiredService<AppDbContext>();

        var userId = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Results.Unauthorized();

        var email = await db.Users.Where(u => u.Id == userId).Select(u => u.Email).FirstOrDefaultAsync();
        if (string.IsNullOrEmpty(email)) return Results.Unauthorized();

        await PendingInviteClaimer.ClaimAsync(db, userId, email);

        if (!http.Request.Headers.TryGetValue("X-Team-Id", out var teamIdHeader) ||
            !int.TryParse(teamIdHeader, out var teamId))
        {
            return Results.BadRequest(new { message = "Missing or invalid X-Team-Id header." });
        }

        var membership = await db.TeamMemberships.FirstOrDefaultAsync(m =>
            m.TeamId == teamId && m.UserId == userId && m.Status == MembershipStatus.Active);

        if (membership is null)
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        if (_minRole is { } required && RoleRank(membership.Role) < RoleRank(required))
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        http.SetTeamContext(new TeamContext
        {
            UserId = userId,
            Email = email,
            TeamId = teamId,
            Role = membership.Role
        });

        return await next(context);
    }

    private static int RoleRank(TeamRole role) => role switch
    {
        TeamRole.Viewer => 0,
        TeamRole.Editor => 1,
        TeamRole.Admin => 2,
        _ => -1
    };
}

public static class RequireTeamFilterExtensions
{
    // Any active member (Viewer and up) — for reads.
    public static RouteHandlerBuilder RequireTeamMember(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter(new RequireTeamFilter());

    // Editor or Admin — for writes.
    public static RouteHandlerBuilder RequireTeamEditor(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter(new RequireTeamFilter(TeamRole.Editor));

    // Admin only — for team/member management.
    public static RouteHandlerBuilder RequireTeamAdmin(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter(new RequireTeamFilter(TeamRole.Admin));
}
