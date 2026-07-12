using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ShiftPlanner.Api.Data;
using ShiftPlanner.Api.Models;

namespace ShiftPlanner.Api.Services;

// Runs before any tenant-scoped endpoint. Requires an authenticated user, an
// "X-Team-Id" header naming a team, and a TeamMember row in that team whose Person
// is linked to this login — and if minRole is set, that the member's AccessRole
// meets it. Also claims any pending (not-yet-logged-in) People for the caller's
// email/phone before checking, which is how "the same email or phone was added to
// two teams" resolves into two TeamMember rows on whichever account later logs in.
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

        var user = await db.Users.Where(u => u.Id == userId).Select(u => new { u.Email, u.PhoneNumber }).FirstOrDefaultAsync();
        if (user is null) return Results.Unauthorized();

        await PendingInviteClaimer.ClaimAsync(db, userId, user.Email, user.PhoneNumber);

        if (!http.Request.Headers.TryGetValue("X-Team-Id", out var teamIdHeader) ||
            !int.TryParse(teamIdHeader, out var teamId))
        {
            return Results.BadRequest(new { message = "Missing or invalid X-Team-Id header." });
        }

        var member = await db.TeamMembers
            .Include(m => m.Person)
            .FirstOrDefaultAsync(m => m.TeamId == teamId && m.Person!.UserId == userId);

        if (member is null)
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        if (_minRole is { } required && RoleRank(member.AccessRole) < RoleRank(required))
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        http.SetTeamContext(new TeamContext
        {
            UserId = userId,
            Email = user.Email ?? string.Empty,
            TeamId = teamId,
            Role = member.AccessRole
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
